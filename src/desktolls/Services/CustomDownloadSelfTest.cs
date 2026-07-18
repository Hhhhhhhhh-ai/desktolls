using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DeskTolls.Services;

internal sealed record CustomDownloadSelfTestResult(
    bool MultiThreadPassed,
    bool SingleThreadFallbackPassed,
    bool AutomaticFileNamePassed,
    bool CancellationCleanupPassed,
    int RangeRequestCount,
    string? Error);

internal static class CustomDownloadSelfTest
{
    internal static async Task<CustomDownloadSelfTestResult> RunAsync()
    {
        var data = new byte[2 * 1024 * 1024 + 733];
        for (var index = 0; index < data.Length; index++)
        {
            data[index] = (byte)((index * 31 + 17) % 251);
        }

        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"desktolls-download-self-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);

        try
        {
            await using var server = new LocalDownloadServer(data);
            using var service = new CustomDownloadService();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var rangeResult = await service.DownloadAsync(
                new CustomDownloadRequest(
                    server.GetUrl("range.bin").ToString(),
                    testDirectory,
                    "range.bin",
                    4,
                    true),
                null,
                timeout.Token);
            var rangeBytes = await File.ReadAllBytesAsync(rangeResult.FilePath, timeout.Token);
            var multiThreadPassed = rangeResult is { UsedMultiThread: true, SegmentCount: > 1 }
                && data.AsSpan().SequenceEqual(rangeBytes);

            var singleResult = await service.DownloadAsync(
                new CustomDownloadRequest(
                    server.GetUrl("single.bin").ToString(),
                    testDirectory,
                    "single.bin",
                    4,
                    true),
                null,
                timeout.Token);
            var singleBytes = await File.ReadAllBytesAsync(singleResult.FilePath, timeout.Token);
            var singleThreadPassed = !singleResult.UsedMultiThread
                && data.AsSpan().SequenceEqual(singleBytes);

            var inspection = await service.InspectAsync(
                server.GetUrl("auto-name.bin").ToString(),
                timeout.Token);
            var automaticNameResult = await service.DownloadAsync(
                new CustomDownloadRequest(
                    server.GetUrl("auto-name.bin").ToString(),
                    testDirectory,
                    "typed-wrong-suffix.wrong",
                    4,
                    true,
                    true),
                null,
                timeout.Token);
            var automaticFileNamePassed =
                inspection.SuggestedFileName == "VSCodeUserSetup-x64-1.129.0.exe"
                && Path.GetFileName(automaticNameResult.FilePath)
                    == "VSCodeUserSetup-x64-1.129.0.exe";

            var cancellationPath = Path.Combine(testDirectory, "cancel.bin");
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(180));
            var canceled = false;
            try
            {
                await service.DownloadAsync(
                    new CustomDownloadRequest(
                        server.GetUrl("slow.bin").ToString(),
                        testDirectory,
                        "cancel.bin",
                        4,
                        true),
                    null,
                    cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }

            var cancellationCleanupPassed = canceled
                && !File.Exists(cancellationPath)
                && !File.Exists(cancellationPath + ".desktolls.part");

            return new CustomDownloadSelfTestResult(
                multiThreadPassed,
                singleThreadPassed,
                automaticFileNamePassed,
                cancellationCleanupPassed,
                server.RangeRequestCount,
                null);
        }
        catch (Exception exception)
        {
            return new CustomDownloadSelfTestResult(false, false, false, false, 0, exception.Message);
        }
        finally
        {
            try
            {
                Directory.Delete(testDirectory, true);
            }
            catch
            {
                // Temporary self-test data is harmless if another process still has a handle open.
            }
        }
    }

    private sealed class LocalDownloadServer : IAsyncDisposable
    {
        private readonly byte[] _data;
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _acceptLoop;
        private int _rangeRequestCount;
        private int _injectedFailure;

        public LocalDownloadServer(byte[] data)
        {
            _data = data;
            _listener.Start();
            _acceptLoop = AcceptLoopAsync();
        }

        public int RangeRequestCount => Volatile.Read(ref _rangeRequestCount);

        public Uri GetUrl(string name)
        {
            var endpoint = (IPEndPoint)_listener.LocalEndpoint;
            return new Uri($"http://127.0.0.1:{endpoint.Port}/{name}");
        }

        private async Task AcceptLoopAsync()
        {
            try
            {
                while (!_cancellation.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(_cancellation.Token);
                    _ = HandleClientAsync(client);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException) when (_cancellation.IsCancellationRequested)
            {
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    await using var stream = client.GetStream();
                    var headerText = await ReadHeadersAsync(stream, _cancellation.Token);
                    var lines = headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length == 0)
                    {
                        return;
                    }

                    var requestParts = lines[0].Split(' ');
                    var path = requestParts.Length >= 2 ? requestParts[1] : "/";
                    var supportsRanges = path.Contains("range.bin", StringComparison.OrdinalIgnoreCase)
                        || path.Contains("auto-name.bin", StringComparison.OrdinalIgnoreCase);
                    var rangeHeader = lines.FirstOrDefault(line =>
                        line.StartsWith("Range:", StringComparison.OrdinalIgnoreCase));

                    long start = 0;
                    long end = _data.Length - 1;
                    var partial = supportsRanges
                        && TryParseRange(rangeHeader, _data.Length, out start, out end);
                    if (partial)
                    {
                        Interlocked.Increment(ref _rangeRequestCount);
                    }

                    if (partial
                        && path.Contains("range.bin", StringComparison.OrdinalIgnoreCase)
                        && end - start + 1 > 512
                        && Interlocked.CompareExchange(ref _injectedFailure, 1, 0) == 0)
                    {
                        var failureHeader = Encoding.ASCII.GetBytes(
                            "HTTP/1.1 503 Service Unavailable\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(failureHeader, _cancellation.Token);
                        return;
                    }

                    var length = checked((int)(end - start + 1));
                    var metadataHeaders = path.Contains(
                        "auto-name.bin",
                        StringComparison.OrdinalIgnoreCase)
                        ? "Content-Disposition: attachment; filename=\"VSCodeUserSetup-x64-1.129.0.exe\"\r\nContent-Type: application/vnd.microsoft.portable-executable\r\n"
                        : "Content-Type: application/octet-stream\r\n";
                    var responseHeader = partial
                        ? $"HTTP/1.1 206 Partial Content\r\nContent-Length: {length}\r\nContent-Range: bytes {start}-{end}/{_data.Length}\r\nAccept-Ranges: bytes\r\n{metadataHeaders}Connection: close\r\n\r\n"
                        : $"HTTP/1.1 200 OK\r\nContent-Length: {_data.Length}\r\n{metadataHeaders}Connection: close\r\n\r\n";
                    var responseHeaderBytes = Encoding.ASCII.GetBytes(responseHeader);
                    await stream.WriteAsync(responseHeaderBytes, _cancellation.Token);
                    if (path.Contains("slow.bin", StringComparison.OrdinalIgnoreCase))
                    {
                        const int chunkSize = 32 * 1024;
                        var offset = 0;
                        while (offset < _data.Length)
                        {
                            var count = Math.Min(chunkSize, _data.Length - offset);
                            await stream.WriteAsync(
                                _data.AsMemory(offset, count),
                                _cancellation.Token);
                            offset += count;
                            await Task.Delay(20, _cancellation.Token);
                        }
                    }
                    else
                    {
                        await stream.WriteAsync(
                            _data.AsMemory((int)start, partial ? length : _data.Length),
                            _cancellation.Token);
                    }
                }
                catch (Exception exception) when (
                    exception is IOException
                    or SocketException
                    or OperationCanceledException)
                {
                    // The probe intentionally closes a response after reading only its headers.
                }
            }
        }

        private static async Task<string> ReadHeadersAsync(
            NetworkStream stream,
            CancellationToken cancellationToken)
        {
            var bytes = new List<byte>(1024);
            var singleByte = new byte[1];

            while (bytes.Count < 32 * 1024)
            {
                var read = await stream.ReadAsync(singleByte, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                bytes.Add(singleByte[0]);
                var count = bytes.Count;
                if (count >= 4
                    && bytes[count - 4] == '\r'
                    && bytes[count - 3] == '\n'
                    && bytes[count - 2] == '\r'
                    && bytes[count - 1] == '\n')
                {
                    break;
                }
            }

            return Encoding.ASCII.GetString(bytes.ToArray());
        }

        private static bool TryParseRange(
            string? rangeHeader,
            int dataLength,
            out long start,
            out long end)
        {
            start = 0;
            end = dataLength - 1;
            if (string.IsNullOrWhiteSpace(rangeHeader))
            {
                return false;
            }

            var value = rangeHeader[(rangeHeader.IndexOf(':') + 1)..].Trim();
            if (!value.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var parts = value[6..].Split('-', 2);
            if (parts.Length != 2
                || !long.TryParse(parts[0], out start)
                || !long.TryParse(parts[1], out end)
                || start < 0
                || end < start
                || end >= dataLength)
            {
                start = 0;
                end = dataLength - 1;
                return false;
            }

            return true;
        }

        public async ValueTask DisposeAsync()
        {
            _cancellation.Cancel();
            _listener.Stop();
            try
            {
                await _acceptLoop;
            }
            catch
            {
            }

            _cancellation.Dispose();
        }
    }
}

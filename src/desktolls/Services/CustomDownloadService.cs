using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Win32.SafeHandles;

namespace DeskTolls.Services;

internal sealed record CustomDownloadRequest(
    string Url,
    string DestinationFolder,
    string FileName,
    int ThreadCount,
    bool Overwrite,
    bool AutoDetectFileName = false,
    string? ApprovedOverwritePath = null);

internal sealed record DownloadFileInspection(
    string SuggestedFileName,
    long? TotalBytes,
    string? ContentType,
    bool SupportsRanges);

internal sealed class DownloadDestinationExistsException(string filePath)
    : IOException($"文件已存在：{filePath}")
{
    public string FilePath { get; } = filePath;
}

internal sealed record CustomDownloadProgress(
    long BytesReceived,
    long? TotalBytes,
    double BytesPerSecond,
    string Stage,
    bool IsMultiThread,
    int SegmentCount);

internal sealed record CustomDownloadResult(
    string FilePath,
    long FileSize,
    bool UsedMultiThread,
    int SegmentCount);

internal sealed record DownloadSegment(long Start, long End)
{
    public long Length => End - Start + 1;
}

internal sealed class CustomDownloadService : IDisposable
{
    private const int MaximumAttempts = 3;
    private const long MinimumParallelFileSize = 1024 * 1024;
    private const long MinimumSegmentSize = 512 * 1024;
    private const int BufferSize = 128 * 1024;

    private readonly HttpClient _client;

    public CustomDownloadService()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(20),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };

        _client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    public async Task<CustomDownloadResult> DownloadAsync(
        CustomDownloadRequest request,
        IProgress<CustomDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var uri = ValidateUrl(request.Url);
        var reporter = new DownloadProgressReporter(progress);
        reporter.Report("正在连接服务器", false, 1, true);

        var probe = await ProbeWithRetriesAsync(uri, cancellationToken).ConfigureAwait(false);
        var fileName = request.AutoDetectFileName
            ? ResolveFileName(uri, probe, request.FileName)
            : request.FileName;
        var destinationPath = GetDestinationPath(request.DestinationFolder, fileName);

        var overwriteAllowed = request.Overwrite
            && (request.ApprovedOverwritePath is null
                || string.Equals(
                    Path.GetFullPath(request.ApprovedOverwritePath),
                    destinationPath,
                    StringComparison.OrdinalIgnoreCase));
        if (File.Exists(destinationPath) && !overwriteAllowed)
        {
            throw new DownloadDestinationExistsException(destinationPath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        EnsureEnoughDiskSpace(destinationPath, probe.TotalLength);

        var temporaryPath = destinationPath + ".desktolls.part";
        TryDelete(temporaryPath);

        var usedMultiThread = false;
        var segmentCount = 1;

        try
        {
            var segments = probe.SupportsRanges && probe.TotalLength >= MinimumParallelFileSize
                ? CreateSegments(probe.TotalLength.Value, request.ThreadCount)
                : [];

            if (segments.Count > 1)
            {
                reporter.Reset(probe.TotalLength, "正在分段下载", true, segments.Count);
                var rangeSucceeded = await DownloadRangesAsync(
                        uri,
                        temporaryPath,
                        probe.TotalLength!.Value,
                        segments,
                        reporter,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (rangeSucceeded)
                {
                    usedMultiThread = true;
                    segmentCount = segments.Count;
                }
                else
                {
                    reporter.Reset(probe.TotalLength, "服务器不支持分段，已切换单线程", false, 1);
                    TryDelete(temporaryPath);
                    await DownloadSingleAsync(
                            uri,
                            temporaryPath,
                            probe.TotalLength,
                            reporter,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            else
            {
                reporter.Reset(probe.TotalLength, "正在下载", false, 1);
                await DownloadSingleAsync(
                        uri,
                        temporaryPath,
                        probe.TotalLength,
                        reporter,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var fileSize = new FileInfo(temporaryPath).Length;
            if (probe.TotalLength is > 0 && fileSize != probe.TotalLength.Value)
            {
                throw new IOException(
                    $"下载文件大小不一致：预期 {probe.TotalLength.Value} 字节，实际 {fileSize} 字节。");
            }

            File.Move(temporaryPath, destinationPath, overwriteAllowed);
            reporter.Report("下载完成", usedMultiThread, segmentCount, true);

            return new CustomDownloadResult(
                destinationPath,
                fileSize,
                usedMultiThread,
                segmentCount);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    internal async Task<DownloadFileInspection> InspectAsync(
        string url,
        CancellationToken cancellationToken)
    {
        var uri = ValidateUrl(url);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));

        var probe = await ProbeWithRetriesAsync(uri, timeout.Token).ConfigureAwait(false);
        return new DownloadFileInspection(
            ResolveFileName(uri, probe, SuggestFileName(url)),
            probe.TotalLength,
            probe.ContentType,
            probe.SupportsRanges);
    }

    internal static Uri ValidateUrl(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("下载地址必须是完整的 HTTP 或 HTTPS 地址。");
        }

        return uri;
    }

    internal static string GetDestinationPath(string folder, string fileName)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            throw new ArgumentException("请选择保存文件夹。");
        }

        var trimmedName = fileName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new ArgumentException("请输入文件名。");
        }

        if (trimmedName != Path.GetFileName(trimmedName)
            || trimmedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("文件名不能包含路径或无效字符。");
        }

        var fullFolder = Path.GetFullPath(folder.Trim());
        return Path.Combine(fullFolder, trimmedName);
    }

    internal static string SuggestFileName(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        var candidate = Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath));
        if (string.IsNullOrWhiteSpace(candidate)
            || candidate.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return "download";
        }

        return candidate;
    }

    internal static IReadOnlyList<DownloadSegment> CreateSegments(long length, int requestedCount)
    {
        if (length <= 0)
        {
            return [];
        }

        var countBySize = Math.Max(1, (int)Math.Ceiling(length / (double)MinimumSegmentSize));
        var count = Math.Min(Math.Clamp(requestedCount, 1, 8), countBySize);
        var baseLength = length / count;
        var remainder = length % count;
        var segments = new List<DownloadSegment>(count);
        long start = 0;

        for (var index = 0; index < count; index++)
        {
            var segmentLength = baseLength + (index < remainder ? 1 : 0);
            var end = start + segmentLength - 1;
            segments.Add(new DownloadSegment(start, end));
            start = end + 1;
        }

        return segments;
    }

    private async Task<ProbeResult> ProbeWithRetriesAsync(Uri uri, CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            try
            {
                return await ProbeAsync(uri, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                attempt < MaximumAttempts
                && !cancellationToken.IsCancellationRequested
                && IsRetryable(exception))
            {
                lastException = exception;
                await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        throw new IOException("无法连接下载地址。", lastException);
    }

    private async Task<ProbeResult> ProbeAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(uri);
        request.Headers.Range = new RangeHeaderValue(0, 511);

        using var response = await _client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.PartialContent)
        {
            var contentRange = response.Content.Headers.ContentRange;
            if (contentRange is { From: 0, To: >= 0, Length: > 0 })
            {
                return await CreateProbeResultAsync(
                        uri,
                        response,
                        contentRange.Length,
                        true,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (response.IsSuccessStatusCode)
        {
            return await CreateProbeResultAsync(
                    uri,
                    response,
                    response.Content.Headers.ContentLength,
                    false,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (response.StatusCode is HttpStatusCode.BadRequest
            or HttpStatusCode.Forbidden
            or HttpStatusCode.MethodNotAllowed
            or HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            return await ProbeWithoutRangeAsync(uri, cancellationToken).ConfigureAwait(false);
        }

        throw CreateStatusException(response.StatusCode);
    }

    private async Task<ProbeResult> ProbeWithoutRangeAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(uri);
        using var response = await _client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateStatusException(response.StatusCode);
        }

        return await CreateProbeResultAsync(
                uri,
                response,
                response.Content.Headers.ContentLength,
                false,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<ProbeResult> CreateProbeResultAsync(
        Uri originalUri,
        HttpResponseMessage response,
        long? totalLength,
        bool supportsRanges,
        CancellationToken cancellationToken)
    {
        var prefix = await ReadPrefixAsync(response.Content, cancellationToken).ConfigureAwait(false);
        var contentDisposition = response.Content.Headers.ContentDisposition;
        var headerFileName = contentDisposition?.FileNameStar ?? contentDisposition?.FileName;

        return new ProbeResult(
            totalLength,
            supportsRanges,
            NormalizeFileName(headerFileName),
            response.RequestMessage?.RequestUri ?? originalUri,
            response.Content.Headers.ContentType?.MediaType,
            prefix);
    }

    private static async Task<byte[]> ReadPrefixAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        var buffer = new byte[512];
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(
                    buffer.AsMemory(totalRead, buffer.Length - totalRead),
                    cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead == buffer.Length ? buffer : buffer[..totalRead];
    }

    private static string ResolveFileName(Uri originalUri, ProbeResult probe, string fallback)
    {
        var headerName = NormalizeFileName(probe.HeaderFileName);
        var finalUrlName = GetUrlFileName(probe.FinalUri);
        var originalUrlName = GetUrlFileName(originalUri);
        var fallbackName = NormalizeFileName(fallback);
        var baseCandidate = headerName
            ?? finalUrlName
            ?? originalUrlName
            ?? fallbackName
            ?? "download";

        var detectedExtension = GetExtension(headerName)
            ?? GetExtensionFromContentType(probe.ContentType)
            ?? DetectFileExtension(probe.Prefix)
            ?? GetExtension(finalUrlName)
            ?? GetExtension(originalUrlName)
            ?? GetExtension(fallbackName);

        if (string.IsNullOrWhiteSpace(detectedExtension))
        {
            return baseCandidate;
        }

        var baseName = Path.GetFileNameWithoutExtension(baseCandidate);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "download";
        }

        return NormalizeFileName(baseName + detectedExtension) ?? "download" + detectedExtension;
    }

    private static string? GetUrlFileName(Uri? uri)
    {
        if (uri is null)
        {
            return null;
        }

        try
        {
            return NormalizeFileName(Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath)));
        }
        catch (UriFormatException)
        {
            return null;
        }
    }

    private static string? NormalizeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var candidate = value.Trim().Trim('"');
        var encodingMarker = candidate.IndexOf("''", StringComparison.Ordinal);
        if (encodingMarker > 0)
        {
            candidate = candidate[(encodingMarker + 2)..];
        }

        if (candidate.Contains('%'))
        {
            try
            {
                candidate = Uri.UnescapeDataString(candidate);
            }
            catch (UriFormatException)
            {
            }
        }

        candidate = candidate.Replace('\\', '/');
        candidate = candidate[(candidate.LastIndexOf('/') + 1)..];
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            candidate = candidate.Replace(invalidCharacter, '_');
        }

        candidate = candidate.Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        if (candidate.Length <= 180)
        {
            return candidate;
        }

        var extension = Path.GetExtension(candidate);
        var maximumBaseLength = Math.Max(1, 180 - extension.Length);
        return candidate[..maximumBaseLength] + extension;
    }

    private static string? GetExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var extension = Path.GetExtension(fileName);
        return string.IsNullOrWhiteSpace(extension) || extension.Length > 16
            ? null
            : extension.ToLowerInvariant();
    }

    internal static string? GetExtensionFromContentType(string? contentType)
    {
        return contentType?.Trim().ToLowerInvariant() switch
        {
            "application/vnd.microsoft.portable-executable" or "application/x-msdownload" => ".exe",
            "application/x-msi" or "application/x-ms-installer" => ".msi",
            "application/msix" or "application/vnd.ms-appx" => ".msix",
            "application/pdf" => ".pdf",
            "application/zip" or "application/x-zip-compressed" => ".zip",
            "application/x-7z-compressed" => ".7z",
            "application/vnd.rar" or "application/x-rar-compressed" => ".rar",
            "application/gzip" or "application/x-gzip" => ".gz",
            "application/x-tar" => ".tar",
            "application/x-bittorrent" => ".torrent",
            "application/vnd.android.package-archive" => ".apk",
            "application/java-archive" => ".jar",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ".pptx",
            "application/msword" => ".doc",
            "application/vnd.ms-excel" => ".xls",
            "application/vnd.ms-powerpoint" => ".ppt",
            "application/json" => ".json",
            "application/xml" or "text/xml" => ".xml",
            "text/plain" => ".txt",
            "text/csv" => ".csv",
            "text/html" => ".html",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/bmp" => ".bmp",
            "image/svg+xml" => ".svg",
            "audio/mpeg" => ".mp3",
            "audio/flac" => ".flac",
            "audio/wav" or "audio/x-wav" => ".wav",
            "video/mp4" => ".mp4",
            "video/webm" => ".webm",
            _ => null,
        };
    }

    internal static string? DetectFileExtension(ReadOnlySpan<byte> prefix)
    {
        if (HasSignature(prefix, 0x25, 0x50, 0x44, 0x46)) return ".pdf";
        if (HasSignature(prefix, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)) return ".png";
        if (HasSignature(prefix, 0xFF, 0xD8, 0xFF)) return ".jpg";
        if (HasSignature(prefix, 0x47, 0x49, 0x46, 0x38)) return ".gif";
        if (HasSignature(prefix, 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C)) return ".7z";
        if (HasSignature(prefix, 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07)) return ".rar";
        if (HasSignature(prefix, 0x50, 0x4B, 0x03, 0x04)
            || HasSignature(prefix, 0x50, 0x4B, 0x05, 0x06)
            || HasSignature(prefix, 0x50, 0x4B, 0x07, 0x08)) return ".zip";
        if (HasSignature(prefix, 0x1F, 0x8B)) return ".gz";
        if (HasSignature(prefix, 0x42, 0x5A, 0x68)) return ".bz2";
        if (HasSignature(prefix, 0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00)) return ".xz";
        if (HasSignature(prefix, 0x66, 0x4C, 0x61, 0x43)) return ".flac";
        if (HasSignature(prefix, 0x49, 0x44, 0x33)) return ".mp3";
        if (HasSignature(prefix, 0x4D, 0x5A)) return DetectPortableExecutableExtension(prefix);
        if (prefix.Length >= 12
            && prefix[..4].SequenceEqual("RIFF"u8)
            && prefix.Slice(8, 4).SequenceEqual("WEBP"u8)) return ".webp";
        if (prefix.Length >= 12
            && prefix[..4].SequenceEqual("RIFF"u8)
            && prefix.Slice(8, 4).SequenceEqual("WAVE"u8)) return ".wav";
        if (prefix.Length >= 12 && prefix.Slice(4, 4).SequenceEqual("ftyp"u8)) return ".mp4";
        return null;
    }

    private static string DetectPortableExecutableExtension(ReadOnlySpan<byte> prefix)
    {
        if (prefix.Length < 64)
        {
            return ".exe";
        }

        var peOffset = prefix[0x3C]
            | prefix[0x3D] << 8
            | prefix[0x3E] << 16
            | prefix[0x3F] << 24;
        if (peOffset < 0
            || peOffset + 24 > prefix.Length
            || !prefix.Slice(peOffset, 4).SequenceEqual("PE\0\0"u8))
        {
            return ".exe";
        }

        var characteristics = prefix[peOffset + 22] | prefix[peOffset + 23] << 8;
        if ((characteristics & 0x2000) != 0) return ".dll";
        if ((characteristics & 0x1000) != 0) return ".sys";
        return ".exe";
    }

    private static bool HasSignature(ReadOnlySpan<byte> value, params byte[] signature)
    {
        return value.StartsWith(signature);
    }

    private async Task<bool> DownloadRangesAsync(
        Uri uri,
        string temporaryPath,
        long totalLength,
        IReadOnlyList<DownloadSegment> segments,
        DownloadProgressReporter reporter,
        CancellationToken cancellationToken)
    {
        await using var output = new FileStream(
            temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.RandomAccess);
        output.SetLength(totalLength);

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var rangeRejected = 0;
        Exception? firstFailure = null;

        var tasks = segments.Select(async segment =>
        {
            try
            {
                await DownloadSegmentWithRetriesAsync(
                        uri,
                        output.SafeFileHandle,
                        segment,
                        totalLength,
                        reporter,
                        linkedCancellation.Token)
                    .ConfigureAwait(false);
            }
            catch (RangeNotSupportedException exception)
            {
                Interlocked.Exchange(ref rangeRejected, 1);
                Interlocked.CompareExchange(ref firstFailure, exception, null);
                linkedCancellation.Cancel();
                throw;
            }
            catch (Exception exception)
            {
                Interlocked.CompareExchange(ref firstFailure, exception, null);
                linkedCancellation.Cancel();
                throw;
            }
        }).ToArray();

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Volatile.Read(ref rangeRejected) == 1)
            {
                return false;
            }

            throw new IOException("分段下载失败，已达到最大重试次数。", firstFailure);
        }
    }

    private async Task DownloadSegmentWithRetriesAsync(
        Uri uri,
        SafeFileHandle outputHandle,
        DownloadSegment segment,
        long totalLength,
        DownloadProgressReporter reporter,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            long attemptBytes = 0;
            var copyState = new SegmentCopyState();
            try
            {
                using var request = CreateRequest(uri);
                request.Headers.Range = new RangeHeaderValue(segment.Start, segment.End);

                using var response = await _client.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw CreateStatusException(response.StatusCode);
                }

                var range = response.Content.Headers.ContentRange;
                if (response.StatusCode != HttpStatusCode.PartialContent
                    || range?.From != segment.Start
                    || range.To != segment.End
                    || range.Length != totalLength
                    || (response.Content.Headers.ContentLength is long responseLength
                        && responseLength != segment.Length))
                {
                    throw new RangeNotSupportedException();
                }

                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false);
                attemptBytes = await CopySegmentAsync(
                        input,
                        outputHandle,
                        segment,
                        copyState,
                        reporter,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (attemptBytes != segment.Length)
                {
                    throw new EndOfStreamException(
                        $"分段数据不完整：预期 {segment.Length} 字节，实际 {attemptBytes} 字节。");
                }

                return;
            }
            catch (RangeNotSupportedException)
            {
                reporter.RemoveBytes(Math.Max(attemptBytes, copyState.Bytes));
                throw;
            }
            catch (Exception exception) when (
                attempt < MaximumAttempts
                && !cancellationToken.IsCancellationRequested
                && IsRetryable(exception))
            {
                reporter.RemoveBytes(Math.Max(attemptBytes, copyState.Bytes));
                lastException = exception;
                reporter.ReportStage("网络波动，正在重试分段");
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        throw new IOException("分段下载重试失败。", lastException);
    }

    private static async Task<long> CopySegmentAsync(
        Stream input,
        SafeFileHandle outputHandle,
        DownloadSegment segment,
        SegmentCopyState copyState,
        DownloadProgressReporter reporter,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        long downloaded = 0;

        try
        {
            while (downloaded < segment.Length)
            {
                var readLength = (int)Math.Min(buffer.Length, segment.Length - downloaded);
                var read = await input.ReadAsync(
                        buffer.AsMemory(0, readLength),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                await RandomAccess.WriteAsync(
                        outputHandle,
                        buffer.AsMemory(0, read),
                        segment.Start + downloaded,
                        cancellationToken)
                    .ConfigureAwait(false);
                downloaded += read;
                copyState.Bytes = downloaded;
                reporter.AddBytes(read);
            }

            return downloaded;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task DownloadSingleAsync(
        Uri uri,
        string temporaryPath,
        long? probedLength,
        DownloadProgressReporter reporter,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            try
            {
                reporter.Reset(probedLength, attempt == 1 ? "正在下载" : "正在重新下载", false, 1);

                using var request = CreateRequest(uri);
                using var response = await _client.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw CreateStatusException(response.StatusCode);
                }

                var responseLength = response.Content.Headers.ContentLength;
                reporter.SetTotal(responseLength ?? probedLength);

                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false);
                await using var output = new FileStream(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read,
                    BufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
                long downloaded = 0;
                try
                {
                    int read;
                    while ((read = await input.ReadAsync(
                               buffer.AsMemory(0, buffer.Length),
                               cancellationToken)
                           .ConfigureAwait(false)) > 0)
                    {
                        await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                            .ConfigureAwait(false);
                        downloaded += read;
                        reporter.AddBytes(read);
                    }

                    await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                if (responseLength is >= 0 && downloaded != responseLength.Value)
                {
                    throw new EndOfStreamException(
                        $"服务器提前结束传输：预期 {responseLength.Value} 字节，实际 {downloaded} 字节。");
                }

                return;
            }
            catch (Exception exception) when (
                attempt < MaximumAttempts
                && !cancellationToken.IsCancellationRequested
                && IsRetryable(exception))
            {
                lastException = exception;
                reporter.ReportStage("网络波动，正在重试");
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        throw new IOException("下载失败，已达到最大重试次数。", lastException);
    }

    private static HttpRequestMessage CreateRequest(Uri uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 desktolls/1.3.1");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
        return request;
    }

    private static HttpRequestException CreateStatusException(HttpStatusCode statusCode)
    {
        return new HttpRequestException(
            $"服务器返回 {(int)statusCode} ({statusCode})。部分需要登录、Cookie 或防盗链的网站不能直接下载。",
            null,
            statusCode);
    }

    private static bool IsRetryable(Exception exception)
    {
        return exception is HttpRequestException
            or IOException
            or TimeoutException
            or TaskCanceledException;
    }

    private static void EnsureEnoughDiskSpace(string destinationPath, long? totalLength)
    {
        if (totalLength is not > 0)
        {
            return;
        }

        var root = Path.GetPathRoot(destinationPath);
        if (string.IsNullOrWhiteSpace(root)
            || root.Length < 2
            || root[1] != ':')
        {
            return;
        }

        var drive = new DriveInfo(root);
        var required = totalLength.Value + 5L * 1024 * 1024;
        if (drive.AvailableFreeSpace < required)
        {
            throw new IOException(
                $"{drive.Name} 空间不足：至少需要 {FormatBytes(required)}，当前可用 {FormatBytes(drive.AvailableFreeSpace)}。");
        }
    }

    private static string FormatBytes(long bytes)
    {
        return bytes >= 1024 * 1024 * 1024
            ? $"{bytes / 1024d / 1024d / 1024d:0.00} GB"
            : $"{bytes / 1024d / 1024d:0.0} MB";
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // The original download exception is more useful than cleanup failures.
        }
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private sealed record ProbeResult(
        long? TotalLength,
        bool SupportsRanges,
        string? HeaderFileName,
        Uri FinalUri,
        string? ContentType,
        byte[] Prefix);

    private sealed class RangeNotSupportedException : Exception;

    private sealed class SegmentCopyState
    {
        public long Bytes { get; set; }
    }

    private sealed class DownloadProgressReporter(IProgress<CustomDownloadProgress>? progress)
    {
        private readonly object _sync = new();
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private long _bytesReceived;
        private long? _totalBytes;
        private long _lastReportMilliseconds = -1000;
        private long _lastReportBytes;
        private double _speed;
        private string _stage = "正在下载";
        private bool _isMultiThread;
        private int _segmentCount = 1;

        public void AddBytes(int count)
        {
            Interlocked.Add(ref _bytesReceived, count);
            ReportCurrent(false);
        }

        public void RemoveBytes(long count)
        {
            if (count > 0)
            {
                Interlocked.Add(ref _bytesReceived, -count);
            }
        }

        public void SetTotal(long? totalBytes)
        {
            lock (_sync)
            {
                _totalBytes = totalBytes;
            }
        }

        public void Reset(
            long? totalBytes,
            string stage,
            bool isMultiThread,
            int segmentCount)
        {
            Interlocked.Exchange(ref _bytesReceived, 0);
            lock (_sync)
            {
                _totalBytes = totalBytes;
                _stopwatch.Restart();
                _lastReportMilliseconds = -1000;
                _lastReportBytes = 0;
                _speed = 0;
                _stage = stage;
                _isMultiThread = isMultiThread;
                _segmentCount = segmentCount;
            }

            ReportCurrent(true);
        }

        public void Report(
            string stage,
            bool isMultiThread,
            int segmentCount,
            bool force)
        {
            if (progress is null)
            {
                return;
            }

            lock (_sync)
            {
                _stage = stage;
                _isMultiThread = isMultiThread;
                _segmentCount = segmentCount;
            }

            ReportCurrent(force);
        }

        public void ReportStage(string stage)
        {
            lock (_sync)
            {
                _stage = stage;
            }

            ReportCurrent(true);
        }

        private void ReportCurrent(bool force)
        {
            if (progress is null)
            {
                return;
            }

            lock (_sync)
            {
                var elapsedMilliseconds = _stopwatch.ElapsedMilliseconds;
                if (!force && elapsedMilliseconds - _lastReportMilliseconds < 200)
                {
                    return;
                }

                var bytes = Interlocked.Read(ref _bytesReceived);
                var deltaMilliseconds = elapsedMilliseconds - _lastReportMilliseconds;
                if (_lastReportMilliseconds >= 0 && deltaMilliseconds > 0)
                {
                    var instantaneous = (bytes - _lastReportBytes) * 1000d / deltaMilliseconds;
                    _speed = _speed <= 0 ? instantaneous : _speed * 0.65 + instantaneous * 0.35;
                }

                _lastReportMilliseconds = elapsedMilliseconds;
                _lastReportBytes = bytes;
                progress.Report(new CustomDownloadProgress(
                    bytes,
                    _totalBytes,
                    Math.Max(0, _speed),
                    _stage,
                    _isMultiThread,
                    _segmentCount));
            }
        }
    }
}

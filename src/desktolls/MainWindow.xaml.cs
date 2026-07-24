using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskTolls.Models;
using DeskTolls.Services;
using DrawingFont = System.Drawing.Font;
using DrawingIcon = System.Drawing.Icon;
using Forms = System.Windows.Forms;

namespace DeskTolls;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore = new();
    private readonly AppSettings _settings;
    private readonly bool _isFirstRun;
    private readonly bool _startupLaunch;
    private readonly DesktopIconService _desktopIconService = new();
    private readonly ClassicContextMenuService _classicContextMenuService = new();
    private readonly TaskbarAutoHideService _taskbarAutoHideService = new();
    private readonly MouseHookService _mouseHookService = new();
    private readonly KeyboardShortcutService _keyboardShortcutService = new();
    private readonly SoundFeedbackService _soundFeedbackService = new();
    private readonly AutoClickService _autoClickService;
    private readonly MemoryOptimizationService _memoryOptimizationService;
    private readonly CustomDownloadService _customDownloadService = new();
    private readonly Forms.NotifyIcon _trayIcon = new();
    private readonly Forms.ToolStripMenuItem _trayStateItem = new();

    private HotkeyService? _hotkeyService;
    private HwndSource? _windowSource;
    private DrawingIcon? _trayDrawingIcon;
    private WindowsUpdatePolicyState _windowsUpdatePolicyState = new(false, false);
    private TaskbarAutoHideState _taskbarAutoHideState = new(false, true);
    private bool _initializing = true;
    private bool _allowExit;
    private bool _resourcesDisposed;
    private bool _desktopOperationRunning;
    private bool _classicMenuOperationRunning;
    private bool _windowsUpdatePolicyOperationRunning;
    private bool _exitOperationRunning;
    private bool _downloadRunning;
    private bool _shownTrayNotice;
    private CancellationTokenSource? _downloadCancellation;
    private CancellationTokenSource? _downloadNameDetectionCancellation;
    private string? _lastDownloadedFilePath;
    private int _downloadNameDetectionVersion;

    public static uint ShowRequestMessage { get; } = NativeMethods.RegisterWindowMessage(
        "desktolls-show-window-1c5244e7");

    public MainWindow(bool startupLaunch)
    {
        (_settings, _isFirstRun) = _settingsStore.Load();
        _startupLaunch = startupLaunch;
        _autoClickService = new AutoClickService(() => _settings.ClicksPerSecond);
        _memoryOptimizationService = new MemoryOptimizationService(
            () => _settings.MemoryOptimizationIntervalSeconds);

        InitializeComponent();
        InitializeControls();
        InitializeTrayIcon();

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;

        _mouseHookService.DesktopMiddlePressed += (_, _) =>
            _ = Dispatcher.InvokeAsync(ToggleDesktopIconsAsync);
        _keyboardShortcutService.CopyPressed += (_, _) =>
        {
            if (_settings.CopySoundEnabled)
            {
                _soundFeedbackService.TryPlayCopy(out _);
            }
        };
        _keyboardShortcutService.PastePressed += (_, _) =>
        {
            if (_settings.PasteSoundEnabled)
            {
                _soundFeedbackService.TryPlayPaste(out _);
            }
        };
        _autoClickService.StateChanged += UpdateAutoClickState;
        _memoryOptimizationService.Optimized += UpdateMemoryOptimizationResult;
    }

    public void PrepareForSystemShutdown()
    {
        _allowExit = true;
        DisposeResources();
    }

    private void InitializeControls()
    {
        DesktopFeatureToggle.IsChecked = _settings.DesktopToggleEnabled;
        ClassicMenuToggle.IsChecked = _settings.ClassicContextMenuEnabled;
        AutoClickFeatureToggle.IsChecked = _settings.AutoClickEnabled;
        CopySoundToggle.IsChecked = _settings.CopySoundEnabled;
        PasteSoundToggle.IsChecked = _settings.PasteSoundEnabled;
        TaskbarAutoHideToggle.IsChecked = _settings.TaskbarAutoHideEnabled;
        RestoreOnExitCheckBox.IsChecked = _settings.RestoreSystemSettingsOnExit;
        MemoryOptimizationToggle.IsChecked = _settings.MemoryOptimizationEnabled;
        StartupToggle.IsChecked = _settings.StartWithWindows;

        HotkeyCombo.ItemsSource = HotkeyOption.All;
        HotkeyCombo.SelectedItem = HotkeyOption.All.First(option =>
            option.VirtualKey == _settings.AutoClickHotkey);

        FrequencySlider.Value = _settings.ClicksPerSecond;
        FrequencyValueText.Text = $"{_settings.ClicksPerSecond} 次/秒";

        MemoryIntervalCombo.ItemsSource = MemoryOptimizationIntervalOption.All;
        MemoryIntervalCombo.SelectedItem = MemoryOptimizationIntervalOption.All.First(option =>
            option.Seconds == _settings.MemoryOptimizationIntervalSeconds);

        DownloadFolderTextBox.Text = _settings.DownloadFolder;
        DownloadThreadCombo.ItemsSource = DownloadThreadOption.All;
        DownloadThreadCombo.SelectedItem = DownloadThreadOption.All.First(option =>
            option.Count == _settings.DownloadThreadCount);
        AutoDetectDownloadFileNameCheckBox.IsChecked = _settings.AutoDetectDownloadFileName;
        DownloadFileNameTextBox.IsReadOnly = _settings.AutoDetectDownloadFileName;
        RefreshTaskbarAutoHideState(alignSetting: true);
        RefreshWindowsUpdatePolicyState();
        _initializing = false;
    }

    private void InitializeTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        var openItem = new Forms.ToolStripMenuItem("打开 desktolls");
        openItem.Font = new DrawingFont(openItem.Font, System.Drawing.FontStyle.Bold);
        openItem.Click += (_, _) => Dispatcher.Invoke(ShowSettingsWindow);

        _trayStateItem.Enabled = false;
        _trayStateItem.Text = "功能正在运行";

        var exitItem = new Forms.ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => Dispatcher.Invoke(ExitApplication);

        menu.Items.Add(openItem);
        menu.Items.Add(_trayStateItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.Text = "desktolls";
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowSettingsWindow);
        SetTrayVisual(false);
        _trayIcon.Visible = true;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _windowSource = HwndSource.FromHwnd(handle);
        _windowSource.AddHook(WindowMessageHook);
        _hotkeyService = new HotkeyService(handle);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            StartupService.SetEnabled(_settings.StartWithWindows);
            ApplyDesktopFeatureState();
            ApplyAutoClickFeatureState(showError: !_startupLaunch);
            ApplyClipboardSoundState(showError: !_startupLaunch);
            ApplyMemoryOptimizationState();
            RefreshTaskbarAutoHideState(alignSetting: true);
            RefreshWindowsUpdatePolicyState();
            if (_isFirstRun && _settings.ClassicContextMenuEnabled)
            {
                await ApplyClassicContextMenuAsync(true);
            }
            else
            {
                var actualClassicMenuState = _classicContextMenuService.IsEnabled();
                _settings.ClassicContextMenuEnabled = actualClassicMenuState;
                ClassicMenuToggle.IsChecked = actualClassicMenuState;
                UpdateClassicMenuStatus(actualClassicMenuState);
                _settingsStore.Save(_settings);
            }

            if ((_startupLaunch || _isFirstRun)
                && _settings.DesktopToggleEnabled
                && _settings.HideIconsOnStartup)
            {
                await _desktopIconService.SetIconsVisibleAsync(false, 18);
            }

            UpdateDesktopStatus();
            UpdateAppStatus();
        }
        catch (Exception exception)
        {
            ShowError("初始化未全部完成", exception, !_startupLaunch);
        }
    }

    private IntPtr WindowMessageHook(
        IntPtr window,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (unchecked((uint)message) == ShowRequestMessage)
        {
            ShowSettingsWindow();
            handled = true;
            return IntPtr.Zero;
        }

        if (HotkeyService.IsHotkeyMessage(message, wParam) && _settings.AutoClickEnabled)
        {
            _autoClickService.Toggle();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void ApplyDesktopFeatureState()
    {
        if (_settings.DesktopToggleEnabled)
        {
            _mouseHookService.Start();
        }
        else
        {
            _mouseHookService.Stop();
        }

        UpdateDesktopStatus();
    }

    private void ApplyAutoClickFeatureState(bool showError)
    {
        if (_hotkeyService is null)
        {
            return;
        }

        if (!_settings.AutoClickEnabled)
        {
            _autoClickService.Stop();
            _hotkeyService.Unregister();
            UpdateAutoClickState(false);
            return;
        }

        try
        {
            _hotkeyService.Register(_settings.AutoClickHotkey);
            UpdateAutoClickState(false);
        }
        catch (Win32Exception exception)
        {
            _settings.AutoClickEnabled = false;
            AutoClickFeatureToggle.IsChecked = false;
            _settingsStore.Save(_settings);
            UpdateAutoClickState(false);
            ShowError("连点热键启用失败", exception, showError);
        }
    }

    private void ApplyMemoryOptimizationState()
    {
        if (_settings.MemoryOptimizationEnabled)
        {
            _memoryOptimizationService.Start();
            MemoryOptimizationStatusText.Text =
                $"已开启 · 每 {GetMemoryOptimizationIntervalName()}";
        }
        else
        {
            _memoryOptimizationService.Stop();
            MemoryOptimizationStatusText.Text = "自动优化已关闭";
        }
    }

    private void ApplyClipboardSoundState(bool showError)
    {
        if (!_settings.CopySoundEnabled && !_settings.PasteSoundEnabled)
        {
            _keyboardShortcutService.Stop();
            UpdateClipboardSoundStatus();
            return;
        }

        try
        {
            _keyboardShortcutService.Start();
            UpdateClipboardSoundStatus();
        }
        catch (Exception exception)
        {
            _settings.CopySoundEnabled = false;
            _settings.PasteSoundEnabled = false;
            CopySoundToggle.IsChecked = false;
            PasteSoundToggle.IsChecked = false;
            _settingsStore.Save(_settings);
            UpdateClipboardSoundStatus();
            ShowError("复制/粘贴提示音启用失败", exception, showError);
        }
    }

    private async Task ToggleDesktopIconsAsync()
    {
        if (_desktopOperationRunning || !_settings.DesktopToggleEnabled)
        {
            return;
        }

        _desktopOperationRunning = true;
        try
        {
            await _desktopIconService.ToggleAsync();
            UpdateDesktopStatus();
        }
        catch (Exception exception)
        {
            ShowError("桌面图标切换失败", exception, IsVisible);
        }
        finally
        {
            _desktopOperationRunning = false;
        }
    }

    private async Task ApplyClassicContextMenuAsync(bool enabled)
    {
        if (_classicMenuOperationRunning)
        {
            return;
        }

        _classicMenuOperationRunning = true;
        ClassicMenuToggle.IsEnabled = false;
        ClassicMenuStatusText.Text = "正在重启 Explorer";

        var desktopWasVisible = _desktopIconService.AreIconsVisible()
            ?? _desktopIconService.GetDesiredVisibilityFromRegistry();

        try
        {
            _classicContextMenuService.SetEnabled(enabled);
            _settings.ClassicContextMenuEnabled = enabled;
            _settingsStore.Save(_settings);

            await ExplorerService.RestartAsync();
            await _desktopIconService.SetIconsVisibleAsync(desktopWasVisible, 14);

            ClassicMenuToggle.IsChecked = enabled;
            UpdateClassicMenuStatus(enabled);
            UpdateDesktopStatus();
        }
        catch (Exception exception)
        {
            var actualState = _classicContextMenuService.IsEnabled();
            _settings.ClassicContextMenuEnabled = actualState;
            ClassicMenuToggle.IsChecked = actualState;
            UpdateClassicMenuStatus(actualState);
            _settingsStore.Save(_settings);
            ShowError("右键菜单切换失败", exception, IsVisible);
        }
        finally
        {
            ClassicMenuToggle.IsEnabled = true;
            _classicMenuOperationRunning = false;
        }
    }

    private void DesktopFeatureToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        _settings.DesktopToggleEnabled = DesktopFeatureToggle.IsChecked == true;
        try
        {
            ApplyDesktopFeatureState();
            _settingsStore.Save(_settings);
            UpdateAppStatus();
        }
        catch (Exception exception)
        {
            _settings.DesktopToggleEnabled = false;
            DesktopFeatureToggle.IsChecked = false;
            _settingsStore.Save(_settings);
            ShowError("桌面图标功能启用失败", exception, true);
        }
    }

    private void SettingsNavigation_Checked(object sender, RoutedEventArgs e)
    {
        if (IsInitialized && sender is FrameworkElement { Tag: string section })
        {
            ShowSettingsSection(section);
        }
    }

    private void ShowSettingsSection(string section)
    {
        var showSystem = string.Equals(section, "System", StringComparison.Ordinal);
        var showInput = string.Equals(section, "Input", StringComparison.Ordinal);
        var showDownload = string.Equals(section, "Download", StringComparison.Ordinal);

        DesktopSection.Visibility = showSystem ? Visibility.Visible : Visibility.Collapsed;
        TaskbarSection.Visibility = showSystem ? Visibility.Visible : Visibility.Collapsed;
        ClassicMenuSection.Visibility = showSystem ? Visibility.Visible : Visibility.Collapsed;
        WindowsUpdateSection.Visibility = showSystem ? Visibility.Visible : Visibility.Collapsed;

        MemorySection.Visibility = showInput ? Visibility.Visible : Visibility.Collapsed;
        AutoClickSection.Visibility = showInput ? Visibility.Visible : Visibility.Collapsed;
        ClipboardSoundSection.Visibility = showInput ? Visibility.Visible : Visibility.Collapsed;

        DownloadSection.Visibility = showDownload ? Visibility.Visible : Visibility.Collapsed;
        SettingsSectionTitle.Text = showSystem
            ? "系统与桌面"
            : showInput
                ? "输入与性能"
                : "文件下载";
        SettingsScrollViewer.ScrollToTop();
    }

    private void StartupToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        var previousValue = _settings.StartWithWindows;
        _settings.StartWithWindows = StartupToggle.IsChecked == true;
        try
        {
            StartupService.SetEnabled(_settings.StartWithWindows);
            _settingsStore.Save(_settings);
        }
        catch (Exception exception)
        {
            _settings.StartWithWindows = previousValue;
            StartupToggle.IsChecked = previousValue;
            ShowError("开机启动设置失败", exception, true);
        }
    }

    private void RestoreOnExitCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_initializing || _exitOperationRunning)
        {
            return;
        }

        _settings.RestoreSystemSettingsOnExit = RestoreOnExitCheckBox.IsChecked == true;
        _settingsStore.Save(_settings);
    }

    private async void ClassicMenuToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_initializing || _classicMenuOperationRunning)
        {
            return;
        }

        await ApplyClassicContextMenuAsync(ClassicMenuToggle.IsChecked == true);
        UpdateAppStatus();
    }

    private void AutoClickFeatureToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        _settings.AutoClickEnabled = AutoClickFeatureToggle.IsChecked == true;
        ApplyAutoClickFeatureState(showError: true);
        _settingsStore.Save(_settings);
        UpdateAppStatus();
    }

    private void MemoryOptimizationToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        _settings.MemoryOptimizationEnabled = MemoryOptimizationToggle.IsChecked == true;
        ApplyMemoryOptimizationState();
        _settingsStore.Save(_settings);
        UpdateAppStatus();
    }

    private void ClipboardSoundToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        _settings.CopySoundEnabled = CopySoundToggle.IsChecked == true;
        _settings.PasteSoundEnabled = PasteSoundToggle.IsChecked == true;
        ApplyClipboardSoundState(showError: true);
        _settingsStore.Save(_settings);
        UpdateAppStatus();
    }

    private void PreviewCopySound_Click(object sender, RoutedEventArgs e)
    {
        if (!_soundFeedbackService.TryPlayCopy(out var error) && error is not null)
        {
            ShowError("复制提示音播放失败", error, true);
        }
    }

    private void PreviewPasteSound_Click(object sender, RoutedEventArgs e)
    {
        if (!_soundFeedbackService.TryPlayPaste(out var error) && error is not null)
        {
            ShowError("粘贴提示音播放失败", error, true);
        }
    }

    private void TaskbarAutoHideToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        var enabled = TaskbarAutoHideToggle.IsChecked == true;
        try
        {
            _taskbarAutoHideState = _taskbarAutoHideService.SetEnabled(enabled);
            _settings.TaskbarAutoHideEnabled = _taskbarAutoHideState.AutoHideEnabled;
            _settingsStore.Save(_settings);
            UpdateTaskbarAutoHideStatus();
        }
        catch (Exception exception)
        {
            RefreshTaskbarAutoHideState(alignSetting: true);
            ShowError("任务栏自动隐藏切换失败", exception, true);
        }

        UpdateAppStatus();
    }

    private void RefreshTaskbarAutoHideState(bool alignSetting)
    {
        _taskbarAutoHideState = _taskbarAutoHideService.GetState();
        TaskbarAutoHideToggle.IsChecked = _taskbarAutoHideState.AutoHideEnabled;

        if (alignSetting && _settings.TaskbarAutoHideEnabled != _taskbarAutoHideState.AutoHideEnabled)
        {
            _settings.TaskbarAutoHideEnabled = _taskbarAutoHideState.AutoHideEnabled;
            _settingsStore.Save(_settings);
        }

        UpdateTaskbarAutoHideStatus();
    }

    private void UpdateTaskbarAutoHideStatus()
    {
        TaskbarAutoHideStatusText.Text = _taskbarAutoHideState.AutoHideEnabled
            ? "已开启 · 鼠标移到屏幕边缘可显示"
            : "已关闭 · 任务栏保持显示";
    }

    private async void WindowsUpdatePolicyToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_initializing || _windowsUpdatePolicyOperationRunning)
        {
            return;
        }

        var shouldDisable = WindowsUpdatePolicyToggle.IsChecked == true;
        _windowsUpdatePolicyOperationRunning = true;
        WindowsUpdatePolicyToggle.IsEnabled = false;
        WindowsUpdatePolicyStatusText.Text = "等待管理员授权";

        try
        {
            var exitCode = await WindowsUpdatePolicyService.SetAutomaticUpdatesDisabledAsync(shouldDisable);
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"管理员策略进程返回错误代码 {exitCode}。");
            }

            RefreshWindowsUpdatePolicyState();
            if (_windowsUpdatePolicyState.AutomaticUpdatesDisabled != shouldDisable)
            {
                throw new InvalidOperationException("Windows 更新策略写入后未能通过校验。");
            }
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            RefreshWindowsUpdatePolicyState();
            WindowsUpdatePolicyStatusText.Text += " · 已取消授权";
        }
        catch (Exception exception)
        {
            RefreshWindowsUpdatePolicyState();
            ShowError("Windows 更新策略切换失败", exception, true);
        }
        finally
        {
            WindowsUpdatePolicyToggle.IsEnabled = true;
            _windowsUpdatePolicyOperationRunning = false;
            UpdateAppStatus();
        }
    }

    private void MemoryIntervalCombo_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_initializing
            || MemoryIntervalCombo.SelectedItem is not MemoryOptimizationIntervalOption selectedOption)
        {
            return;
        }

        _settings.MemoryOptimizationIntervalSeconds = selectedOption.Seconds;
        if (_settings.MemoryOptimizationEnabled)
        {
            _memoryOptimizationService.Restart();
        }

        _settingsStore.Save(_settings);
        ApplyMemoryOptimizationState();
    }

    private async void OptimizeMemoryNow_Click(object sender, RoutedEventArgs e)
    {
        MemoryOptimizeNowButton.IsEnabled = false;
        MemoryOptimizationStatusText.Text = "正在优化当前进程";

        try
        {
            var result = await _memoryOptimizationService.OptimizeNowAsync();
            if (result is null)
            {
                MemoryOptimizationStatusText.Text = "已有优化任务正在运行";
            }
        }
        catch (Exception exception)
        {
            ShowError("内存优化失败", exception, IsVisible);
        }
        finally
        {
            MemoryOptimizeNowButton.IsEnabled = true;
        }
    }

    private void HotkeyCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_initializing || HotkeyCombo.SelectedItem is not HotkeyOption selectedOption)
        {
            return;
        }

        var previousKey = _settings.AutoClickHotkey;
        _settings.AutoClickHotkey = selectedOption.VirtualKey;

        try
        {
            if (_settings.AutoClickEnabled)
            {
                _hotkeyService?.Register(selectedOption.VirtualKey);
            }

            _settingsStore.Save(_settings);
            UpdateAutoClickState(_autoClickService.IsClicking);
        }
        catch (Win32Exception exception)
        {
            _settings.AutoClickHotkey = previousKey;
            _initializing = true;
            HotkeyCombo.SelectedItem = HotkeyOption.All.First(option => option.VirtualKey == previousKey);
            _initializing = false;
            ShowError("热键修改失败", exception, true);
        }
    }

    private void FrequencySlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (_initializing)
        {
            return;
        }

        _settings.ClicksPerSecond = (int)Math.Round(FrequencySlider.Value);
        FrequencyValueText.Text = $"{_settings.ClicksPerSecond} 次/秒";
        _settingsStore.Save(_settings);
    }

    private void DecreaseFrequency_Click(object sender, RoutedEventArgs e)
    {
        FrequencySlider.Value = Math.Max(FrequencySlider.Minimum, FrequencySlider.Value - 1);
    }

    private void IncreaseFrequency_Click(object sender, RoutedEventArgs e)
    {
        FrequencySlider.Value = Math.Min(FrequencySlider.Maximum, FrequencySlider.Value + 1);
    }

    private void DownloadUrlTextBox_LostKeyboardFocus(
        object sender,
        System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if (AutoDetectDownloadFileNameCheckBox.IsChecked == true)
        {
            ScheduleDownloadNameDetection(immediate: true);
            return;
        }

        if (!string.IsNullOrWhiteSpace(DownloadFileNameTextBox.Text))
        {
            return;
        }

        DownloadFileNameTextBox.Text = CustomDownloadService.SuggestFileName(
            DownloadUrlTextBox.Text);
    }

    private void DownloadInput_TextChanged(
        object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateDownloadStartButtonState();

        if (!_initializing
            && !_downloadRunning
            && ReferenceEquals(sender, DownloadUrlTextBox)
            && AutoDetectDownloadFileNameCheckBox?.IsChecked == true)
        {
            var localSuggestion = CustomDownloadService.SuggestFileName(DownloadUrlTextBox.Text);
            if (!string.IsNullOrWhiteSpace(localSuggestion))
            {
                DownloadFileNameTextBox.Text = localSuggestion;
            }

            ScheduleDownloadNameDetection(immediate: false);
        }
    }

    private void AutoDetectDownloadFileNameCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        _settings.AutoDetectDownloadFileName =
            AutoDetectDownloadFileNameCheckBox.IsChecked == true;
        DownloadFileNameTextBox.IsReadOnly = _settings.AutoDetectDownloadFileName;
        _settingsStore.Save(_settings);

        if (_settings.AutoDetectDownloadFileName)
        {
            var localSuggestion = CustomDownloadService.SuggestFileName(
                DownloadUrlTextBox.Text);
            if (!string.IsNullOrWhiteSpace(localSuggestion))
            {
                DownloadFileNameTextBox.Text = localSuggestion;
            }

            ScheduleDownloadNameDetection(immediate: true);
        }
        else
        {
            CancelDownloadNameDetection();
            DownloadStatusText.Text = "可手动填写文件名";
        }

        UpdateDownloadStartButtonState();
    }

    private void ScheduleDownloadNameDetection(bool immediate)
    {
        CancelDownloadNameDetection();

        string url;
        try
        {
            url = CustomDownloadService.ValidateUrl(DownloadUrlTextBox.Text).ToString();
        }
        catch
        {
            if (!_downloadRunning)
            {
                DownloadStatusText.Text = "等待下载地址";
            }

            return;
        }

        var cancellation = new CancellationTokenSource();
        _downloadNameDetectionCancellation = cancellation;
        var version = ++_downloadNameDetectionVersion;
        _ = DetectDownloadNameAsync(url, version, immediate, cancellation);
    }

    private async Task DetectDownloadNameAsync(
        string url,
        int version,
        bool immediate,
        CancellationTokenSource cancellation)
    {
        try
        {
            if (!immediate)
            {
                await Task.Delay(650, cancellation.Token);
            }

            if (_downloadRunning)
            {
                return;
            }

            DownloadStatusText.Text = "正在识别文件名";
            var inspection = await _customDownloadService.InspectAsync(url, cancellation.Token);
            if (cancellation.IsCancellationRequested
                || version != _downloadNameDetectionVersion
                || _downloadRunning
                || !string.Equals(
                    CustomDownloadService.ValidateUrl(DownloadUrlTextBox.Text).ToString(),
                    url,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            DownloadFileNameTextBox.Text = inspection.SuggestedFileName;
            DownloadStatusText.Text = inspection.TotalBytes is > 0
                ? $"已识别文件名 · {FormatDownloadBytes(inspection.TotalBytes.Value)}"
                : "已识别文件名";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch
        {
            if (!cancellation.IsCancellationRequested
                && version == _downloadNameDetectionVersion
                && !_downloadRunning)
            {
                DownloadStatusText.Text = "远程识别失败 · 已按网址命名";
            }
        }
        finally
        {
            if (ReferenceEquals(_downloadNameDetectionCancellation, cancellation))
            {
                _downloadNameDetectionCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void CancelDownloadNameDetection()
    {
        _downloadNameDetectionVersion++;
        _downloadNameDetectionCancellation?.Cancel();
        _downloadNameDetectionCancellation = null;
    }

    private void BrowseDownloadFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "选择下载文件的保存位置",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            InitialDirectory = Directory.Exists(DownloadFolderTextBox.Text)
                ? DownloadFolderTextBox.Text
                : _settings.DownloadFolder,
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
        {
            return;
        }

        DownloadFolderTextBox.Text = dialog.SelectedPath;
        _settings.DownloadFolder = dialog.SelectedPath;
        _settingsStore.Save(_settings);
    }

    private void DownloadThreadCombo_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_initializing
            || DownloadThreadCombo.SelectedItem is not DownloadThreadOption selectedOption)
        {
            return;
        }

        _settings.DownloadThreadCount = selectedOption.Count;
        _settingsStore.Save(_settings);
    }

    private async void StartDownload_Click(object sender, RoutedEventArgs e)
    {
        if (_downloadRunning)
        {
            return;
        }

        CustomDownloadRequest request;
        try
        {
            _ = CustomDownloadService.ValidateUrl(DownloadUrlTextBox.Text);

            var autoDetectFileName = AutoDetectDownloadFileNameCheckBox.IsChecked == true;

            if (string.IsNullOrWhiteSpace(DownloadFileNameTextBox.Text))
            {
                DownloadFileNameTextBox.Text = CustomDownloadService.SuggestFileName(
                    DownloadUrlTextBox.Text);
            }

            var destinationPath = CustomDownloadService.GetDestinationPath(
                DownloadFolderTextBox.Text,
                DownloadFileNameTextBox.Text);

            var threadCount = DownloadThreadCombo.SelectedItem is DownloadThreadOption option
                ? option.Count
                : 4;

            request = new CustomDownloadRequest(
                DownloadUrlTextBox.Text.Trim(),
                Path.GetDirectoryName(destinationPath)!,
                Path.GetFileName(destinationPath),
                threadCount,
                false,
                autoDetectFileName);

            _settings.DownloadFolder = request.DestinationFolder;
            _settings.DownloadThreadCount = request.ThreadCount;
            _settings.AutoDetectDownloadFileName = request.AutoDetectFileName;
            _settingsStore.Save(_settings);
        }
        catch (Exception exception)
        {
            ShowError("下载设置有误", exception, true);
            return;
        }

        CancelDownloadNameDetection();
        _downloadCancellation = new CancellationTokenSource();
        _downloadRunning = true;
        _lastDownloadedFilePath = null;
        SetDownloadControlsRunning(true);
        DownloadProgressBar.IsIndeterminate = true;
        DownloadProgressBar.Value = 0;
        DownloadProgressText.Text = "正在连接";
        DownloadSpeedText.Text = "0 KB/s";
        DownloadStatusText.Text = "正在连接服务器";
        UpdateAppStatus();

        var downloadProgress = new Progress<CustomDownloadProgress>(UpdateDownloadProgress);

        try
        {
            CustomDownloadResult result;
            while (true)
            {
                try
                {
                    result = await _customDownloadService.DownloadAsync(
                        request,
                        downloadProgress,
                        _downloadCancellation.Token);
                    break;
                }
                catch (DownloadDestinationExistsException exception)
                {
                    DownloadProgressBar.IsIndeterminate = false;
                    var overwrite = System.Windows.MessageBox.Show(
                        this,
                        $"文件已经存在：\n{exception.FilePath}\n\n是否覆盖？",
                        "确认覆盖",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (overwrite != MessageBoxResult.Yes)
                    {
                        DownloadStatusText.Text = "已取消覆盖";
                        return;
                    }

                    request = request with
                    {
                        Overwrite = true,
                        ApprovedOverwritePath = exception.FilePath,
                    };
                    DownloadProgressBar.IsIndeterminate = true;
                }
            }

            _lastDownloadedFilePath = result.FilePath;
            if (request.AutoDetectFileName)
            {
                DownloadFileNameTextBox.Text = Path.GetFileName(result.FilePath);
            }
            DownloadProgressBar.IsIndeterminate = false;
            DownloadProgressBar.Value = 100;
            DownloadProgressText.Text = $"100% · {FormatDownloadBytes(result.FileSize)}";
            DownloadSpeedText.Text = "已完成";
            DownloadStatusText.Text = result.UsedMultiThread
                ? $"下载完成 · {result.SegmentCount} 个分段"
                : "下载完成 · 单线程";

            if (!IsVisible)
            {
                _trayIcon.BalloonTipTitle = "下载完成";
                _trayIcon.BalloonTipText = Path.GetFileName(result.FilePath);
                _trayIcon.ShowBalloonTip(3000);
            }
        }
        catch (OperationCanceledException)
        {
            DownloadProgressBar.IsIndeterminate = false;
            DownloadStatusText.Text = "下载已取消";
            DownloadSpeedText.Text = "0 KB/s";
        }
        catch (Exception exception)
        {
            DownloadProgressBar.IsIndeterminate = false;
            DownloadStatusText.Text = "下载失败";
            DownloadSpeedText.Text = "0 KB/s";
            ShowError("文件下载失败", exception, IsVisible);
        }
        finally
        {
            _downloadRunning = false;
            _downloadCancellation.Dispose();
            _downloadCancellation = null;
            SetDownloadControlsRunning(false);
            UpdateAppStatus();
        }
    }

    private void CancelDownload_Click(object sender, RoutedEventArgs e)
    {
        if (!_downloadRunning)
        {
            return;
        }

        CancelDownloadButton.IsEnabled = false;
        DownloadStatusText.Text = "正在取消下载";
        _downloadCancellation?.Cancel();
    }

    private void OpenDownloadFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = _lastDownloadedFilePath is not null
                ? Path.GetDirectoryName(_lastDownloadedFilePath)!
                : Path.GetFullPath(DownloadFolderTextBox.Text.Trim());
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            ShowError("无法打开文件夹", exception, true);
        }
    }

    private void UpdateDownloadProgress(CustomDownloadProgress progress)
    {
        if (_resourcesDisposed || !_downloadRunning)
        {
            return;
        }

        DownloadProgressBar.IsIndeterminate = progress.TotalBytes is not > 0;
        if (progress.TotalBytes is > 0)
        {
            var percentage = Math.Clamp(
                progress.BytesReceived * 100d / progress.TotalBytes.Value,
                0,
                100);
            DownloadProgressBar.Value = percentage;
            DownloadProgressText.Text =
                $"{percentage:0.0}% · {FormatDownloadBytes(progress.BytesReceived)} / {FormatDownloadBytes(progress.TotalBytes.Value)}";
        }
        else
        {
            DownloadProgressText.Text = FormatDownloadBytes(progress.BytesReceived);
        }

        DownloadSpeedText.Text = $"{FormatDownloadSpeed(progress.BytesPerSecond)}";
        DownloadStatusText.Text = progress.IsMultiThread
            ? $"{progress.Stage} · {progress.SegmentCount} 个分段"
            : $"{progress.Stage} · 单线程";
    }

    private void SetDownloadControlsRunning(bool running)
    {
        DownloadUrlTextBox.IsEnabled = !running;
        DownloadFolderTextBox.IsEnabled = !running;
        DownloadFileNameTextBox.IsEnabled = !running;
        DownloadFileNameTextBox.IsReadOnly =
            AutoDetectDownloadFileNameCheckBox.IsChecked == true;
        AutoDetectDownloadFileNameCheckBox.IsEnabled = !running;
        DownloadThreadCombo.IsEnabled = !running;
        BrowseDownloadFolderButton.IsEnabled = !running;
        CancelDownloadButton.IsEnabled = running;
        UpdateDownloadStartButtonState();
    }

    private void UpdateDownloadStartButtonState()
    {
        if (StartDownloadButton is null
            || DownloadUrlTextBox is null
            || DownloadFolderTextBox is null
            || DownloadFileNameTextBox is null)
        {
            return;
        }

        if (_downloadRunning)
        {
            StartDownloadButton.IsEnabled = false;
            return;
        }

        try
        {
            _ = CustomDownloadService.ValidateUrl(DownloadUrlTextBox.Text);
            _ = CustomDownloadService.GetDestinationPath(
                DownloadFolderTextBox.Text,
                AutoDetectDownloadFileNameCheckBox?.IsChecked == true
                    && string.IsNullOrWhiteSpace(DownloadFileNameTextBox.Text)
                    ? "download"
                    : DownloadFileNameTextBox.Text);
            StartDownloadButton.IsEnabled = true;
        }
        catch
        {
            StartDownloadButton.IsEnabled = false;
        }
    }

    private void UpdateDesktopStatus()
    {
        if (!_settings.DesktopToggleEnabled)
        {
            DesktopStatusText.Text = "已关闭";
            return;
        }

        DesktopStatusText.Text = _desktopIconService.AreIconsVisible() switch
        {
            true => "图标当前：显示",
            false => "图标当前：隐藏",
            null => "等待 Explorer",
        };
    }

    private void UpdateClassicMenuStatus(bool enabled)
    {
        ClassicMenuStatusText.Text = enabled ? "已启用" : "已关闭";
    }

    private void RefreshWindowsUpdatePolicyState()
    {
        try
        {
            _windowsUpdatePolicyState = WindowsUpdatePolicyService.GetState();
            WindowsUpdatePolicyToggle.IsChecked =
                _windowsUpdatePolicyState.AutomaticUpdatesDisabled;
            WindowsUpdatePolicyStatusText.Text =
                _windowsUpdatePolicyState.AutomaticUpdatesDisabled
                    ? _windowsUpdatePolicyState.ManagedByDesktolls
                        ? "已禁止自动更新 · 仍可手动检查"
                        : "已被现有系统策略禁止"
                    : "自动更新当前开启";
        }
        catch (Exception exception)
        {
            WindowsUpdatePolicyStatusText.Text = $"策略读取失败 · {exception.Message}";
        }
    }

    private void UpdateAutoClickState(bool active)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => UpdateAutoClickState(active));
            return;
        }

        var selectedHotkey = HotkeyOption.All.First(option =>
            option.VirtualKey == _settings.AutoClickHotkey).Name;

        AutoClickStatusText.Text = !_settings.AutoClickEnabled
            ? "已关闭"
            : active
                ? $"连点中 · {selectedHotkey} 停止"
                : $"已停止 · {selectedHotkey} 启动";
        AutoClickStatusText.Foreground = active
            ? (System.Windows.Media.Brush)FindResource("WarningBrush")
            : (System.Windows.Media.Brush)FindResource("MutedTextBrush");

        _trayStateItem.Text = active ? "鼠标连点运行中" : "功能正在运行";
        SetTrayVisual(active);
        UpdateAppStatus();
    }

    private void UpdateClipboardSoundStatus()
    {
        ClipboardSoundStatusText.Text = (_settings.CopySoundEnabled, _settings.PasteSoundEnabled) switch
        {
            (true, true) => "复制和粘贴提示音已开启",
            (true, false) => "仅复制提示音已开启",
            (false, true) => "仅粘贴提示音已开启",
            _ => "已关闭",
        };
    }

    private void UpdateMemoryOptimizationResult(MemoryOptimizationResult result)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.InvokeAsync(() => UpdateMemoryOptimizationResult(result));
            return;
        }

        if (_resourcesDisposed)
        {
            return;
        }

        if (!result.Success)
        {
            MemoryOptimizationStatusText.Text = $"优化失败 · Win32 {result.Win32Error}";
            return;
        }

        if (_settings.DesktopToggleEnabled)
        {
            try
            {
                _mouseHookService.Restart();
            }
            catch (Exception exception)
            {
                ShowError("桌面中键钩子恢复失败", exception, IsVisible);
            }
        }

        if (_settings.CopySoundEnabled || _settings.PasteSoundEnabled)
        {
            try
            {
                _keyboardShortcutService.Restart();
            }
            catch (Exception exception)
            {
                ShowError("复制/粘贴键盘钩子恢复失败", exception, IsVisible);
            }
        }

        MemoryOptimizationStatusText.Text =
            $"工作集 {FormatMemory(result.WorkingSetAfter)} · 本次减少 {FormatMemory(result.WorkingSetReduction)}";
    }

    private void UpdateAppStatus()
    {
        if (_autoClickService.IsClicking)
        {
            AppStateText.Text = "鼠标连点运行中";
            AppStateText.Foreground = (System.Windows.Media.Brush)FindResource("WarningBrush");
            return;
        }

        if (_downloadRunning)
        {
            AppStateText.Text = "正在下载文件";
            AppStateText.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
            _trayStateItem.Text = "文件下载中";
            return;
        }

        var enabledFeatureCount = new[]
        {
            _settings.DesktopToggleEnabled,
            _settings.ClassicContextMenuEnabled,
            _settings.AutoClickEnabled,
            _settings.CopySoundEnabled || _settings.PasteSoundEnabled,
            _taskbarAutoHideState.AutoHideEnabled,
            _settings.MemoryOptimizationEnabled,
            _windowsUpdatePolicyState.AutomaticUpdatesDisabled,
        }.Count(enabled => enabled);

        AppStateText.Text = enabledFeatureCount == 0
            ? "所有功能均已关闭"
            : $"{enabledFeatureCount} 项功能已启用";
        AppStateText.Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush");
        _trayStateItem.Text = "功能正在运行";
    }

    private void SetTrayVisual(bool active)
    {
        var newIcon = TrayIconFactory.Create(active);
        var previousIcon = _trayDrawingIcon;

        _trayDrawingIcon = newIcon;
        _trayIcon.Icon = newIcon;
        _trayIcon.Text = active ? "desktolls - 鼠标连点中" : "desktolls";
        Icon = Imaging.CreateBitmapSourceFromHIcon(
            newIcon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());

        previousIcon?.Dispose();
    }

    private void ShowSettingsWindow()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
        NativeMethods.SetForegroundWindow(new WindowInteropHelper(this).Handle);
    }

    private void HideToTray_Click(object sender, RoutedEventArgs e)
    {
        HideToTray();
    }

    private void HideToTray()
    {
        Hide();
        if (_settings.MemoryOptimizationEnabled)
        {
            _ = _memoryOptimizationService.OptimizeNowAsync();
        }

        if (_shownTrayNotice)
        {
            return;
        }

        _shownTrayNotice = true;
        _trayIcon.BalloonTipTitle = "desktolls";
        _trayIcon.BalloonTipText = "应用仍在系统托盘运行";
        _trayIcon.ShowBalloonTip(1800);
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        ExitApplication();
    }

    private async void ExitApplication()
    {
        if (_exitOperationRunning)
        {
            return;
        }

        _exitOperationRunning = true;
        SetExitControlsRunning(true);

        try
        {
            if (_settings.RestoreSystemSettingsOnExit)
            {
                AppStateText.Text = "正在恢复系统设置";
                await RestoreSystemSettingsForExitAsync();
            }

            _allowExit = true;
            DisposeResources();
            Close();
            System.Windows.Application.Current.Shutdown();
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            ShowSettingsWindow();
            System.Windows.MessageBox.Show(
                this,
                "已取消管理员授权。为避免 Windows 更新策略残留，desktolls 将继续运行。",
                "desktolls",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            RefreshWindowsUpdatePolicyState();
            UpdateAppStatus();
        }
        catch (Exception exception)
        {
            ShowSettingsWindow();
            ShowError("退出前恢复系统设置失败", exception, true);
            try
            {
                RefreshSystemSettingControls();
            }
            catch
            {
                // Keep the original restoration error as the actionable result.
            }
            UpdateAppStatus();
        }
        finally
        {
            if (!_allowExit)
            {
                _exitOperationRunning = false;
                SetExitControlsRunning(false);
            }
        }
    }

    private async Task RestoreSystemSettingsForExitAsync()
    {
        RefreshWindowsUpdatePolicyState();
        if (_windowsUpdatePolicyState.ManagedByDesktolls)
        {
            AppStateText.Text = "正在恢复 Windows 更新策略";
            var exitCode = await WindowsUpdatePolicyService.SetAutomaticUpdatesDisabledAsync(false);
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"更新策略恢复进程返回错误代码 {exitCode}。");
            }

            _windowsUpdatePolicyState = WindowsUpdatePolicyService.GetState();
            if (_windowsUpdatePolicyState.ManagedByDesktolls)
            {
                throw new InvalidOperationException("desktolls 管理的 Windows 更新策略仍未恢复。");
            }
        }

        if (_classicContextMenuService.IsEnabled())
        {
            AppStateText.Text = "正在恢复 Windows 11 右键菜单";
            _classicContextMenuService.SetEnabled(false);
            await ExplorerService.RestartAsync();
        }

        AppStateText.Text = "正在显示任务栏与桌面图标";
        _taskbarAutoHideState = _taskbarAutoHideService.SetEnabled(false);
        if (!await _desktopIconService.SetIconsVisibleAsync(true, 18))
        {
            throw new InvalidOperationException("Explorer 未能恢复桌面图标显示。");
        }

        if (_classicContextMenuService.IsEnabled()
            || _taskbarAutoHideState.AutoHideEnabled
            || _desktopIconService.AreIconsVisible() != true)
        {
            throw new InvalidOperationException("退出设置恢复后未能通过完整状态校验。");
        }

        _settings.ClassicContextMenuEnabled = false;
        _settings.TaskbarAutoHideEnabled = false;
        _settingsStore.Save(_settings);
        RefreshSystemSettingControls();
    }

    private void RefreshSystemSettingControls()
    {
        var classicMenuEnabled = _classicContextMenuService.IsEnabled();
        _settings.ClassicContextMenuEnabled = classicMenuEnabled;
        ClassicMenuToggle.IsChecked = classicMenuEnabled;
        UpdateClassicMenuStatus(classicMenuEnabled);
        RefreshTaskbarAutoHideState(alignSetting: true);
        RefreshWindowsUpdatePolicyState();
        UpdateDesktopStatus();
        _settingsStore.Save(_settings);
    }

    private void SetExitControlsRunning(bool running)
    {
        ExitButton.IsEnabled = !running;
        HideToTrayButton.IsEnabled = !running;
        HeaderHideButton.IsEnabled = !running;
        RestoreOnExitCheckBox.IsEnabled = !running;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowExit)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    private void DisposeResources()
    {
        if (_resourcesDisposed)
        {
            return;
        }

        _resourcesDisposed = true;
        CancelDownloadNameDetection();
        _downloadCancellation?.Cancel();
        _settingsStore.Save(_settings);
        _memoryOptimizationService.Dispose();
        _autoClickService.Dispose();
        _mouseHookService.Dispose();
        _keyboardShortcutService.Dispose();
        _soundFeedbackService.Dispose();
        _hotkeyService?.Dispose();
        _customDownloadService.Dispose();

        if (_windowSource is not null)
        {
            _windowSource.RemoveHook(WindowMessageHook);
        }

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayDrawingIcon?.Dispose();
    }

    private string GetMemoryOptimizationIntervalName()
    {
        return MemoryOptimizationIntervalOption.All.First(option =>
            option.Seconds == _settings.MemoryOptimizationIntervalSeconds).Name;
    }

    private static string FormatMemory(long bytes)
    {
        return bytes >= 1024 * 1024
            ? $"{bytes / 1024d / 1024d:0.0} MB"
            : $"{bytes / 1024d:0} KB";
    }

    private static string FormatDownloadBytes(long bytes)
    {
        return bytes switch
        {
            >= 1024L * 1024 * 1024 => $"{bytes / 1024d / 1024d / 1024d:0.00} GB",
            >= 1024L * 1024 => $"{bytes / 1024d / 1024d:0.0} MB",
            >= 1024 => $"{bytes / 1024d:0.0} KB",
            _ => $"{bytes} B",
        };
    }

    private static string FormatDownloadSpeed(double bytesPerSecond)
    {
        return bytesPerSecond switch
        {
            >= 1024d * 1024 * 1024 => $"{bytesPerSecond / 1024d / 1024d / 1024d:0.00} GB/s",
            >= 1024d * 1024 => $"{bytesPerSecond / 1024d / 1024d:0.0} MB/s",
            >= 1024d => $"{bytesPerSecond / 1024d:0.0} KB/s",
            _ => $"{bytesPerSecond:0} B/s",
        };
    }

    private void ShowError(string title, Exception exception, bool showDialog)
    {
        if (showDialog)
        {
            System.Windows.MessageBox.Show(
                this,
                exception.Message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        _trayIcon.BalloonTipTitle = title;
        _trayIcon.BalloonTipText = exception.Message;
        _trayIcon.ShowBalloonTip(3500);
    }
}

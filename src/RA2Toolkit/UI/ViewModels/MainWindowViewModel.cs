using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Input;

internal sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private static readonly Uri ReleasesUri = new(
        "https://github.com/pitifulbug/ra2-toolkit/releases/latest");
    private static readonly Uri NewIssueUri = new(
        "https://github.com/pitifulbug/ra2-toolkit/issues/new");

    private readonly Action<OverlayCommand> dispatch;
    private readonly HotkeyStore hotkeyStore;
    private readonly IUpdateService updateService;
    private readonly bool ownsUpdateService;
    private readonly AsyncRelayCommand checkUpdatesCommand;
    private FeatureItemViewModel? capturingFeature;
    private DateTime lastCrateHotkeyAt = DateTime.MinValue;
    private string statusText = "等待启动游戏";
    private string updateText = string.Empty;
    private bool isUpdateChecking;
    private Uri? availableReleaseUri;

    internal MainWindowViewModel(
        Action<OverlayCommand> dispatch,
        HotkeyStore? hotkeyStore = null,
        IUpdateService? updateService = null)
    {
        this.dispatch = dispatch;
        this.hotkeyStore = hotkeyStore ?? new HotkeyStore();
        this.updateService = updateService ?? new GitHubUpdateService();
        ownsUpdateService = updateService is null;

        var items = FeatureCatalog.All.Select(definition =>
            new FeatureItemViewModel(definition, dispatch, BeginHotkeyCapture,
                this.hotkeyStore)).ToArray();
        FeatureGroups = Enum.GetValues<FeatureCategory>()
            .Select(category => new FeatureGroupViewModel(
                category,
                FeatureCatalog.GetCategoryTitle(category),
                items.Where(item => item.Definition.Category == category).ToArray()))
            .ToArray();

        this.hotkeyStore.Changed += RefreshHotkeys;
        ExitCommand = new RelayCommand(() => dispatch(OverlayCommand.ExitProgram));
        OpenFeedbackCommand = new RelayCommand(OpenFeedback);
        OpenReleaseCommand = new RelayCommand(OpenRelease, () => availableReleaseUri is not null);
        checkUpdatesCommand = new AsyncRelayCommand(CheckUpdatesAsync);
        CheckUpdatesCommand = checkUpdatesCommand;

        var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version();
        CurrentVersion = version;
        VersionText = $"RA2 Toolkit {version.ToString(3)}";
    }

    internal event Action? CaptureStateChanged;

    public IReadOnlyList<FeatureGroupViewModel> FeatureGroups { get; }
    public Version CurrentVersion { get; }
    public string VersionText { get; }
    public ICommand ExitCommand { get; }
    public ICommand OpenFeedbackCommand { get; }
    public ICommand OpenReleaseCommand { get; }
    public ICommand CheckUpdatesCommand { get; }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public string UpdateText
    {
        get => updateText;
        private set
        {
            if (!SetProperty(ref updateText, value))
                return;
            OnPropertyChanged(nameof(HasUpdateText));
        }
    }

    public bool HasUpdateText => !string.IsNullOrWhiteSpace(UpdateText);

    public bool IsUpdateChecking
    {
        get => isUpdateChecking;
        private set => SetProperty(ref isUpdateChecking, value);
    }

    public bool IsCapturingHotkey => capturingFeature is not null;

    public string CapturePrompt => capturingFeature is null
        ? string.Empty
        : $"正在设置“{capturingFeature.Title}”：请按组合键；Esc 取消，Delete 清除。";

    internal void ApplyState(OverlayState state)
    {
        foreach (var feature in FeatureGroups.SelectMany(group => group.Features))
            feature.ApplyState(state);
    }

    internal void ShowStatus(string message, bool isError = false)
    {
        StatusText = isError ? $"错误：{message}" : message;
    }

    internal bool TryCaptureHotkey(HotkeyGesture gesture, out string? error)
    {
        error = null;
        var feature = capturingFeature;
        if (feature is null)
            return false;
        var command = feature.Definition.HotkeyBindingCommand;
        if (hotkeyStore.FindConflict(command, gesture) is { } conflict)
        {
            error = $"{gesture.DisplayText} 已由“{FeatureCatalog.GetCommandTitle(conflict)}”占用。";
            return false;
        }

        hotkeyStore.Set(command, gesture);
        FinishHotkeyCapture();
        ShowStatus($"“{feature.Title}”已设为 {gesture.DisplayText}");
        return true;
    }

    internal void ClearCapturedHotkey()
    {
        var feature = capturingFeature;
        if (feature is null)
            return;
        _ = hotkeyStore.Remove(feature.Definition.HotkeyBindingCommand);
        FinishHotkeyCapture();
        ShowStatus($"已清除“{feature.Title}”的快捷键");
    }

    internal void CancelHotkeyCapture() => FinishHotkeyCapture();

    internal void HandleGlobalHotkey(HotkeyGesture gesture)
    {
        if (capturingFeature is not null)
            return;
        var binding = hotkeyStore.Bindings.FirstOrDefault(entry => entry.Value == gesture);
        if (binding.Equals(default(KeyValuePair<OverlayCommand, HotkeyGesture>)))
            return;
        var feature = FeatureCatalog.All.FirstOrDefault(
            candidate => candidate.HotkeyBindingCommand == binding.Key);
        if (feature is null)
            return;

        if (feature.HotkeyDoublePressCommand is { } doublePressCommand)
        {
            var now = DateTime.UtcNow;
            if (now - lastCrateHotkeyAt <= TimeSpan.FromMilliseconds(GetDoubleClickTime()))
            {
                lastCrateHotkeyAt = DateTime.MinValue;
                dispatch(doublePressCommand);
                return;
            }
            lastCrateHotkeyAt = now;
        }
        dispatch(feature.HotkeyPressCommand);
    }

    internal async Task CheckUpdatesNowAsync() =>
        await CheckUpdatesAsync(CancellationToken.None);

    private void BeginHotkeyCapture(FeatureItemViewModel feature)
    {
        capturingFeature?.SetCapturing(false);
        capturingFeature = feature;
        feature.SetCapturing(true);
        OnPropertyChanged(nameof(IsCapturingHotkey));
        OnPropertyChanged(nameof(CapturePrompt));
        CaptureStateChanged?.Invoke();
    }

    private void FinishHotkeyCapture()
    {
        var feature = capturingFeature;
        if (feature is null)
            return;
        capturingFeature = null;
        feature.SetCapturing(false);
        OnPropertyChanged(nameof(IsCapturingHotkey));
        OnPropertyChanged(nameof(CapturePrompt));
        CaptureStateChanged?.Invoke();
    }

    private void RefreshHotkeys()
    {
        foreach (var feature in FeatureGroups.SelectMany(group => group.Features))
            feature.RefreshHotkey();
    }

    private async Task CheckUpdatesAsync(CancellationToken cancellationToken)
    {
        IsUpdateChecking = true;
        UpdateText = "正在检查更新…";
        try
        {
            var result = await updateService.CheckAsync(CurrentVersion, cancellationToken);
            availableReleaseUri = result.UpdateAvailable ? result.ReleaseUri : null;
            UpdateText = result.UpdateAvailable
                ? $"发现新版本 {result.LatestVersion.ToString(3)}，点击查看"
                : string.Empty;
            ((RelayCommand)OpenReleaseCommand).NotifyCanExecuteChanged();
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or
                                      JsonException or InvalidDataException)
        {
            UpdateText = "更新检查失败，点击版本号重试";
        }
        finally
        {
            IsUpdateChecking = false;
        }
    }

    private void OpenRelease()
    {
        try
        {
            ExternalLinkService.Open(availableReleaseUri ?? ReleasesUri);
        }
        catch (Exception error)
        {
            ShowStatus($"未能打开发布页面：{error.Message}", true);
        }
    }

    private void OpenFeedback()
    {
        var title = Uri.EscapeDataString($"[问题反馈] {VersionText}");
        var body = Uri.EscapeDataString(
            $"请描述事件经过、复现步骤和正在使用的游戏版本。{Environment.NewLine}{Environment.NewLine}---{Environment.NewLine}{VersionText}");
        try
        {
            ExternalLinkService.Open(new Uri($"{NewIssueUri}?title={title}&body={body}"));
        }
        catch (Exception error)
        {
            ShowStatus($"未能打开问题反馈页面：{error.Message}", true);
        }
    }

    public void Dispose()
    {
        hotkeyStore.Changed -= RefreshHotkeys;
        checkUpdatesCommand.Cancel();
        if (ownsUpdateService && updateService is IDisposable disposable)
            disposable.Dispose();
    }

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();
}

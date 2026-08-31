using System.Windows.Input;

internal sealed class FeatureItemViewModel : ObservableObject
{
    private readonly Action<OverlayCommand> dispatch;
    private readonly Action<FeatureItemViewModel> beginCapture;
    private readonly HotkeyStore hotkeyStore;
    private bool isActive;
    private bool isAvailable = true;
    private bool isCapturing;
    private string hotkeyText = "快捷键：尚未设定";

    internal FeatureItemViewModel(
        FeatureDefinition definition,
        Action<OverlayCommand> dispatch,
        Action<FeatureItemViewModel> beginCapture,
        HotkeyStore hotkeyStore)
    {
        Definition = definition;
        this.dispatch = dispatch;
        this.beginCapture = beginCapture;
        this.hotkeyStore = hotkeyStore;
        PrimaryCommand = new RelayCommand(ExecutePrimary, () => IsAvailable);
        CaptureHotkeyCommand = new RelayCommand(() => beginCapture(this), () => IsAvailable);
        RefreshHotkey();
    }

    internal FeatureDefinition Definition { get; }
    public string Title => Definition.Title;
    public string Description => Definition.Description;
    public string CompactLabel => $"{Title}（{Description.TrimEnd('。')}）";
    public bool IsToggle => Definition.IsToggle;
    public ICommand PrimaryCommand { get; }
    public ICommand CaptureHotkeyCommand { get; }

    public bool IsActive
    {
        get => isActive;
        private set => SetProperty(ref isActive, value);
    }

    public bool IsAvailable
    {
        get => isAvailable;
        private set
        {
            if (!SetProperty(ref isAvailable, value))
                return;
            ((RelayCommand)PrimaryCommand).NotifyCanExecuteChanged();
            ((RelayCommand)CaptureHotkeyCommand).NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(AvailabilityText));
        }
    }

    public string AvailabilityText => IsAvailable ? string.Empty : "联机对局中不可用";

    public string HotkeyText
    {
        get => hotkeyText;
        private set => SetProperty(ref hotkeyText, value);
    }

    internal void ApplyState(OverlayState state)
    {
        IsActive = Definition.StateSelector?.Invoke(state) ?? false;
        IsAvailable = !Definition.RestrictedInMultiplayer || state.RestrictedFeaturesAvailable;
    }

    internal void RefreshHotkey()
    {
        if (isCapturing)
        {
            HotkeyText = "请按快捷键…";
            return;
        }
        HotkeyText = hotkeyStore.TryGet(Definition.HotkeyBindingCommand, out var binding)
            ? $"快捷键：{binding.DisplayText}"
            : "快捷键：尚未设定";
    }

    internal void SetCapturing(bool value)
    {
        isCapturing = value;
        RefreshHotkey();
    }

    private void ExecutePrimary()
    {
        var command = Definition.ToggleCommand ?? Definition.HotkeyPressCommand;
        dispatch(command);
    }
}

internal sealed class FeatureGroupViewModel
{
    internal FeatureGroupViewModel(
        FeatureCategory category,
        string title,
        IReadOnlyList<FeatureItemViewModel> features)
    {
        Category = category;
        Title = title;
        Features = features;
        ToggleFeatures = [.. features.Where(feature => feature.IsToggle)];
        ActionFeatures = [.. features.Where(feature => !feature.IsToggle)];
    }

    public FeatureCategory Category { get; }
    public string Title { get; }
    public IReadOnlyList<FeatureItemViewModel> Features { get; }
    public IReadOnlyList<FeatureItemViewModel> ToggleFeatures { get; }
    public IReadOnlyList<FeatureItemViewModel> ActionFeatures { get; }

    public string GroupTitle => Category switch
    {
        FeatureCategory.Resources => "资源",
        FeatureCategory.Construction => "基地建设",
        FeatureCategory.AutoConstruction => "围绕选中建筑自动建造",
        FeatureCategory.Combat => "战斗功能",
        FeatureCategory.MapAndCrates => "地图与资源采集",
        FeatureCategory.Game => "游戏控制与娱乐",
        FeatureCategory.Objects => "选中对象操作",
        _ => Title
    };

    public string Hint => Category == FeatureCategory.AutoConstruction
        ? "选中己方建筑后按快捷键；再次按任一建造快捷键可停止。"
        : string.Empty;
}

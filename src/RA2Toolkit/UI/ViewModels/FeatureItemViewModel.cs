using System.Globalization;
using System.Windows.Input;

internal sealed class FeatureItemViewModel : ObservableObject
{
    private readonly Action<OverlayCommandRequest> dispatch;
    private readonly Action<FeatureItemViewModel> beginCapture;
    private readonly HotkeyStore hotkeyStore;
    private bool isActive;
    private bool isAvailable;
    private bool isCapturing;
    private string numberText;
    private FeatureChoice? selectedChoice;
    private string optionText;
    private string validationText = string.Empty;
    private string hotkeyText = "快捷键：尚未设定";

    internal FeatureItemViewModel(
        FeatureDefinition definition,
        Action<OverlayCommandRequest> dispatch,
        Action<FeatureItemViewModel> beginCapture,
        HotkeyStore hotkeyStore)
    {
        Definition = definition;
        this.dispatch = dispatch;
        this.beginCapture = beginCapture;
        this.hotkeyStore = hotkeyStore;
        PrimaryCommand = new RelayCommand(ExecutePrimary, () => IsAvailable);
        CaptureHotkeyCommand = new RelayCommand(
            () => beginCapture(this), () => IsAvailable && SupportsHotkey);
        DecreaseNumberCommand = new RelayCommand(
            () => AdjustNumber(-1), () => IsAvailable && IsNumber);
        IncreaseNumberCommand = new RelayCommand(
            () => AdjustNumber(1), () => IsAvailable && IsNumber);
        PreviousChoiceCommand = new RelayCommand(
            () => MoveChoice(-1), () => IsAvailable && IsChoice);
        NextChoiceCommand = new RelayCommand(
            () => MoveChoice(1), () => IsAvailable && IsChoice);
        numberText = definition.DefaultNumber?.ToString(CultureInfo.CurrentCulture)
            ?? string.Empty;
        selectedChoice = definition.Choices?.FirstOrDefault(choice =>
            string.Equals(choice.Value, definition.DefaultOption,
                StringComparison.OrdinalIgnoreCase)) ?? definition.Choices?.FirstOrDefault();
        optionText = selectedChoice?.Value ?? definition.DefaultOption ?? string.Empty;
        RefreshHotkey();
    }

    internal FeatureDefinition Definition { get; }
    public string Title => Definition.Title;
    public string Description => Definition.Description;
    public string CompactLabel => $"{Title}（{Description.TrimEnd('。')}）";
    public bool IsToggle => Definition.IsToggle;
    public bool IsAction => Definition.IsAction;
    public bool IsNumber => Definition.IsNumber;
    public bool IsChoice => Definition.IsChoice;
    public bool SupportsHotkey => Definition.HotkeyBindingCommand is not null;
    public IReadOnlyList<FeatureChoice> Choices => Definition.Choices ?? [];
    public ICommand PrimaryCommand { get; }
    public ICommand CaptureHotkeyCommand { get; }
    public ICommand DecreaseNumberCommand { get; }
    public ICommand IncreaseNumberCommand { get; }
    public ICommand PreviousChoiceCommand { get; }
    public ICommand NextChoiceCommand { get; }

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
            ((RelayCommand)DecreaseNumberCommand).NotifyCanExecuteChanged();
            ((RelayCommand)IncreaseNumberCommand).NotifyCanExecuteChanged();
            ((RelayCommand)PreviousChoiceCommand).NotifyCanExecuteChanged();
            ((RelayCommand)NextChoiceCommand).NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(AvailabilityText));
        }
    }

    public string AvailabilityText => IsAvailable ? string.Empty : "联机对局中不可用";

    public string HotkeyText
    {
        get => hotkeyText;
        private set => SetProperty(ref hotkeyText, value);
    }

    public string NumberText
    {
        get => numberText;
        set
        {
            if (SetProperty(ref numberText, value))
                ValidationText = string.Empty;
        }
    }

    public FeatureChoice? SelectedChoice
    {
        get => selectedChoice;
        set
        {
            if (!SetProperty(ref selectedChoice, value))
                return;
            if (value is not null)
                OptionText = value.Value;
            ValidationText = string.Empty;
        }
    }

    public string OptionText
    {
        get => optionText;
        set
        {
            if (SetProperty(ref optionText, value))
                ValidationText = string.Empty;
        }
    }

    public string ValidationText
    {
        get => validationText;
        private set
        {
            if (!SetProperty(ref validationText, value))
                return;
            OnPropertyChanged(nameof(HasValidationError));
        }
    }

    public bool HasValidationError => !string.IsNullOrEmpty(ValidationText);

    internal void ApplyState(OverlayState state)
    {
        IsActive = Definition.StateSelector?.Invoke(state) ?? false;
        IsAvailable = state.Connected &&
            (!Definition.RestrictedInMultiplayer || state.RestrictedFeaturesAvailable);
    }

    internal void RefreshHotkey()
    {
        if (Definition.HotkeyBindingCommand is not { } hotkeyCommand)
        {
            HotkeyText = string.Empty;
            return;
        }
        if (isCapturing)
        {
            HotkeyText = "请按快捷键…";
            return;
        }
        HotkeyText = hotkeyStore.TryGet(hotkeyCommand, out var binding)
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
        ValidationText = string.Empty;
        switch (Definition.ControlKind)
        {
            case FeatureControlKind.Number:
                if (!TryGetNumber(out var number))
                    return;
                dispatch(new OverlayCommandRequest(Definition.PrimaryCommand, Number: number));
                break;
            case FeatureControlKind.Choice:
                var option = OptionText.Trim();
                if (option.Length is 0 or > 23 || option.Any(character =>
                        !(character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or
                          >= '0' and <= '9' or '_' or '-')))
                {
                    ValidationText = "请输入 1 到 23 位的英文、数字、下划线或连字符 ID。";
                    return;
                }
                dispatch(new OverlayCommandRequest(
                    Definition.PrimaryCommand, Option: option));
                break;
            default:
                dispatch(new OverlayCommandRequest(Definition.PrimaryCommand));
                break;
        }
    }

    private void AdjustNumber(int direction)
    {
        var current = decimal.TryParse(NumberText, NumberStyles.Number,
            CultureInfo.CurrentCulture, out var localized)
            ? localized
            : Definition.DefaultNumber ?? 0;
        var next = current + (direction * (Definition.NumberStep ?? 1));
        if (Definition.MinimumNumber is { } minimum)
            next = Math.Max(next, minimum);
        if (Definition.MaximumNumber is { } maximum)
            next = Math.Min(next, maximum);
        next = decimal.Round(next, Definition.NumberDecimalPlaces);
        NumberText = next.ToString(CultureInfo.CurrentCulture);
    }

    private void MoveChoice(int direction)
    {
        if (Choices.Count == 0)
            return;

        var currentIndex = -1;
        if (SelectedChoice is { } selected)
        {
            for (var index = 0; index < Choices.Count; index++)
            {
                if (!Equals(Choices[index], selected))
                    continue;
                currentIndex = index;
                break;
            }
        }
        var nextIndex = currentIndex < 0
            ? 0
            : (currentIndex + direction + Choices.Count) % Choices.Count;
        SelectedChoice = Choices[nextIndex];
    }

    private bool TryGetNumber(out decimal number)
    {
        if (!decimal.TryParse(NumberText, NumberStyles.Number, CultureInfo.CurrentCulture,
                out number) &&
            !decimal.TryParse(NumberText, NumberStyles.Number, CultureInfo.InvariantCulture,
                out number))
        {
            ValidationText = "请输入有效数字。";
            return false;
        }

        if (Definition.MinimumNumber is { } minimum && number < minimum ||
            Definition.MaximumNumber is { } maximum && number > maximum)
        {
            ValidationText = $"请输入 {Definition.MinimumNumber} 到 {Definition.MaximumNumber} 之间的数值。";
            return false;
        }

        if (number != decimal.Round(number, Definition.NumberDecimalPlaces))
        {
            ValidationText = Definition.NumberDecimalPlaces == 0
                ? "请输入整数。"
                : $"最多保留 {Definition.NumberDecimalPlaces} 位小数。";
            return false;
        }

        return true;
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
        ActionFeatures = [.. features.Where(feature => feature.IsAction)];
        NumberFeatures = [.. features.Where(feature => feature.IsNumber)];
        ChoiceFeatures = [.. features.Where(feature => feature.IsChoice)];
    }

    public FeatureCategory Category { get; }
    public string Title { get; }
    public IReadOnlyList<FeatureItemViewModel> Features { get; }
    public IReadOnlyList<FeatureItemViewModel> ToggleFeatures { get; }
    public IReadOnlyList<FeatureItemViewModel> ActionFeatures { get; }
    public IReadOnlyList<FeatureItemViewModel> NumberFeatures { get; }
    public IReadOnlyList<FeatureItemViewModel> ChoiceFeatures { get; }

    public string GroupTitle => Category switch
    {
        FeatureCategory.Player => "玩家功能",
        FeatureCategory.Units => "单位功能",
        FeatureCategory.Miscellaneous => "杂项功能",
        FeatureCategory.Enemy => "敌人功能",
        FeatureCategory.Fun => "趣味功能",
        FeatureCategory.Values => "数值调整",
        _ => Title
    };

    public string Hint => Category switch
    {
        FeatureCategory.Values => "数值会在点击“应用”后写入当前对局；退出或切换对局时恢复原值。",
        FeatureCategory.Fun => "空降兵类型使用 rulesmd.ini 中的单位 ID。",
        _ => string.Empty
    };
}

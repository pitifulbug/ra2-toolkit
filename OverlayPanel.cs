using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

internal enum OverlayCommand
{
    ToggleRevealMap,
    ToggleInfiniteMoney,
    ToggleCombatBoost,
    ToggleCratePicker,
    EnableSelectedCratePickers,
    DisableSelectedCratePickers,
    ToggleCrateRouteLines,
    ToggleMaximumPower,
    PromoteSelectedUnits,
    ArrangeSelectedFormation,
    ToggleInstantBuild,
    ToggleBuildAnywhere,
    ToggleAutoRepair,
    ToggleSuperWeaponNoCooldown,
    ExitProgram
}

internal readonly record struct OverlayState(
    bool RevealMap,
    bool InfiniteMoney,
    bool CombatBoost,
    bool CratePicker,
    bool CrateRouteLines,
    bool MaximumPower,
    bool InstantBuild,
    bool BuildAnywhere,
    bool AutoRepair,
    bool SuperWeaponNoCooldown,
    bool Multiplayer);

internal sealed partial class OverlayPanel : Form
{
    private const string LatestReleaseApiUrl =
        "https://api.github.com/repos/pitifulbug/ra2-toolkit/releases/latest";
    private const string ReleasesPageUrl =
        "https://github.com/pitifulbug/ra2-toolkit/releases/latest";
    private const string UpdateRetryTag = "retry-update-check";
    private readonly Dictionary<OverlayCommand, HotkeyBinding> hotkeys = [];
    private readonly HashSet<Keys> pressedKeys = [];
    private readonly string hotkeyFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RA2 Toolkit", "hotkeys.json");
    private Action<OverlayCommand> dispatch = _ => { };
    private KeyboardHook? keyboardHook;
    private OverlayCommand? captureCommand;
    private Button? captureButton;
    private DateTime lastCrateHotkeyAt = DateTime.MinValue;
    private bool updateCheckInProgress;
    private string updateStatusText = string.Empty;
    private string? updateStatusAction;
    private bool updateStatusIsLink;
    private bool operationStatusVisible;
    private int operationStatusSequence;
    private bool allowClose;

    public OverlayPanel()
    {
        InitializeComponent();
        InitializeRuntimeUi();
    }

    public OverlayPanel(Action<OverlayCommand> dispatch) : this()
    {
        this.dispatch = dispatch;
    }

    private Button[] HotkeyButtons =>
    [
        moneyHotkeyButton, powerHotkeyButton, instantBuildHotkeyButton,
        combatHotkeyButton, superWeaponHotkeyButton, promoteHotkeyButton,
        formationHotkeyButton,
        revealMapHotkeyButton, buildAnywhereHotkeyButton, crateHotkeyButton,
        autoRepairHotkeyButton
    ];

    private void InitializeRuntimeUi()
    {
        moneyCheckBox.Tag = OverlayCommand.ToggleInfiniteMoney;
        powerCheckBox.Tag = OverlayCommand.ToggleMaximumPower;
        instantBuildCheckBox.Tag = OverlayCommand.ToggleInstantBuild;
        combatCheckBox.Tag = OverlayCommand.ToggleCombatBoost;
        superWeaponCheckBox.Tag = OverlayCommand.ToggleSuperWeaponNoCooldown;
        revealMapCheckBox.Tag = OverlayCommand.ToggleRevealMap;
        buildAnywhereCheckBox.Tag = OverlayCommand.ToggleBuildAnywhere;
        crateCheckBox.Tag = OverlayCommand.ToggleCratePicker;
        crateRouteLinesCheckBox.Tag = OverlayCommand.ToggleCrateRouteLines;
        autoRepairCheckBox.Tag = OverlayCommand.ToggleAutoRepair;

        moneyHotkeyButton.Tag = OverlayCommand.ToggleInfiniteMoney;
        powerHotkeyButton.Tag = OverlayCommand.ToggleMaximumPower;
        instantBuildHotkeyButton.Tag = OverlayCommand.ToggleInstantBuild;
        combatHotkeyButton.Tag = OverlayCommand.ToggleCombatBoost;
        superWeaponHotkeyButton.Tag = OverlayCommand.ToggleSuperWeaponNoCooldown;
        promoteHotkeyButton.Tag = OverlayCommand.PromoteSelectedUnits;
        formationHotkeyButton.Tag = OverlayCommand.ArrangeSelectedFormation;
        revealMapHotkeyButton.Tag = OverlayCommand.ToggleRevealMap;
        buildAnywhereHotkeyButton.Tag = OverlayCommand.ToggleBuildAnywhere;
        crateHotkeyButton.Tag = OverlayCommand.ToggleCratePicker;
        autoRepairHotkeyButton.Tag = OverlayCommand.ToggleAutoRepair;

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            using var reportIcon = BugReportIcon.Create();
            bugReportButton.Image = reportIcon.ToBitmap();
        }
        catch
        {
            // Visual Studio 设计器宿主可能没有可提取的程序图标。
        }

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "未知";
        softwareVersionLabel.Text = $"当前版本：{version}";
        LoadHotkeys();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            keyboardHook = new KeyboardHook();
            keyboardHook.KeyChanged += HandleGlobalKey;
        }
        catch
        {
        }
        await CheckForUpdatesAsync();
    }

    public void UpdateState(OverlayState state)
    {
        RunOnUiThread(() =>
        {
            revealMapCheckBox.Checked = state.RevealMap;
            moneyCheckBox.Checked = state.InfiniteMoney;
            combatCheckBox.Checked = state.CombatBoost;
            crateCheckBox.Checked = state.CratePicker;
            crateRouteLinesCheckBox.Checked = state.CrateRouteLines;
            powerCheckBox.Checked = state.MaximumPower;
            instantBuildCheckBox.Checked = state.InstantBuild;
            buildAnywhereCheckBox.Checked = state.BuildAnywhere;
            autoRepairCheckBox.Checked = state.AutoRepair;
            superWeaponCheckBox.Checked = state.SuperWeaponNoCooldown;

            var unsafeFeaturesEnabled = !state.Multiplayer;
            foreach (var control in MultiplayerUnsafeControls)
                control.Enabled = unsafeFeaturesEnabled;
        });
    }

    public void ShowOperationStatus(string message, bool isError = false)
    {
        RunOnUiThread(() => _ = ShowOperationStatusAsync(message, isError));
    }

    private Control[] MultiplayerUnsafeControls =>
    [
        revealMapCheckBox, revealMapHotkeyButton,
        moneyCheckBox, moneyHotkeyButton,
        combatCheckBox, combatHotkeyButton,
        powerCheckBox, powerHotkeyButton,
        instantBuildCheckBox, instantBuildHotkeyButton,
        buildAnywhereCheckBox, buildAnywhereHotkeyButton,
        superWeaponCheckBox, superWeaponHotkeyButton,
        promoteButton, promoteHotkeyButton
    ];

    public void RequestClose()
    {
        RunOnUiThread(() =>
        {
            allowClose = true;
            Close();
        });
    }

    protected override void OnFormClosing(FormClosingEventArgs eventArgs)
    {
        if (!allowClose && eventArgs.CloseReason == CloseReason.UserClosing)
        {
            eventArgs.Cancel = true;
            Text = "RA2 Toolkit - 正在结束本次指挥…";
            Enabled = false;
            dispatch(OverlayCommand.ExitProgram);
        }
        base.OnFormClosing(eventArgs);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (keyboardHook is not null)
        {
            keyboardHook.KeyChanged -= HandleGlobalKey;
            keyboardHook.Dispose();
            keyboardHook = null;
        }
        base.OnFormClosed(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (captureCommand is null)
            return base.ProcessCmdKey(ref msg, keyData);

        var key = keyData & Keys.KeyCode;
        if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
            return true;
        if (key == Keys.Escape)
        {
            FinishHotkeyCapture();
            return true;
        }
        if (key is Keys.Delete or Keys.Back)
        {
            var command = captureCommand.Value;
            hotkeys.Remove(captureCommand.Value);
            SaveHotkeys();
            FinishHotkeyCapture();
            ShowOperationStatus($"已清除“{GetCommandDisplayName(command)}”的快捷键");
            return true;
        }

        var candidate = new HotkeyBinding(
            ((keyData & Keys.Control) != 0 ? HotkeyBinding.Control : 0u) |
            ((keyData & Keys.Shift) != 0 ? HotkeyBinding.Shift : 0u) |
            ((keyData & Keys.Alt) != 0 ? HotkeyBinding.Alt : 0u), key);
        var conflictingCommand = hotkeys
            .Where(entry => entry.Key != captureCommand.Value && entry.Value == candidate)
            .Select(entry => (OverlayCommand?)entry.Key)
            .FirstOrDefault();
        if (conflictingCommand is { } conflict)
        {
            MessageBox.Show(this,
                $"快捷键 {candidate.DisplayText} 已由“{GetCommandDisplayName(conflict)}”占用，请重新选择。",
                "快捷键冲突", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return true;
        }

        var capturedCommand = captureCommand.Value;
        hotkeys[capturedCommand] = candidate;
        SaveHotkeys();
        FinishHotkeyCapture();
        ShowOperationStatus(
            $"“{GetCommandDisplayName(capturedCommand)}”已设为 {candidate.DisplayText}");
        return true;
    }

    private void FeatureCheckBox_Click(object? sender, EventArgs e)
    {
        if (sender is Control control && control.Tag is OverlayCommand command)
            dispatch(command);
    }

    private void PromoteButton_Click(object? sender, EventArgs e) =>
        dispatch(OverlayCommand.PromoteSelectedUnits);

    private void FormationButton_Click(object? sender, EventArgs e) =>
        dispatch(OverlayCommand.ArrangeSelectedFormation);

    private void HotkeyButton_Click(object? sender, EventArgs e)
    {
        FinishHotkeyCapture();
        if (sender is not Button button || button.Tag is not OverlayCommand command)
            return;
        captureCommand = command;
        captureButton = button;
        button.Text = "等待指令：请按快捷键…";
        ActiveControl = button;
    }

    private void FinishHotkeyCapture()
    {
        captureCommand = null;
        captureButton = null;
        RefreshHotkeyButtons();
    }

    private void HandleGlobalKey(Keys key, bool isDown)
    {
        if (!isDown)
        {
            pressedKeys.Remove(key);
            return;
        }
        if (captureCommand is not null || !pressedKeys.Add(key))
            return;

        var modifiers = KeyboardHook.GetCurrentModifiers();
        foreach (var binding in hotkeys)
        {
            if (binding.Value.Key != key || binding.Value.Modifiers != modifiers)
                continue;
            if (binding.Key != OverlayCommand.ToggleCratePicker)
            {
                dispatch(binding.Key);
                continue;
            }

            var now = DateTime.UtcNow;
            var isDoublePress = now - lastCrateHotkeyAt <=
                                TimeSpan.FromMilliseconds(SystemInformation.DoubleClickTime);
            lastCrateHotkeyAt = isDoublePress ? DateTime.MinValue : now;
            dispatch(isDoublePress
                ? OverlayCommand.DisableSelectedCratePickers
                : OverlayCommand.EnableSelectedCratePickers);
        }
    }

    private void LoadHotkeys()
    {
        try
        {
            if (File.Exists(hotkeyFile))
            {
                var stored = JsonSerializer.Deserialize<Dictionary<string, HotkeyBinding>>(
                    File.ReadAllText(hotkeyFile));
                if (stored is not null)
                    foreach (var entry in stored)
                        if (Enum.TryParse<OverlayCommand>(entry.Key, out var command))
                            hotkeys[command] = entry.Value;
            }
        }
        catch
        {
        }
        RefreshHotkeyButtons();
    }

    private void SaveHotkeys()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(hotkeyFile)!);
            File.WriteAllText(hotkeyFile, JsonSerializer.Serialize(
                hotkeys.ToDictionary(entry => entry.Key.ToString(), entry => entry.Value),
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
        }
        RefreshHotkeyButtons();
    }

    private void RefreshHotkeyButtons()
    {
        foreach (var button in HotkeyButtons)
        {
            if (button == captureButton)
                continue;
            if (button.Tag is not OverlayCommand command)
                continue;
            button.Text = hotkeys.TryGetValue(command, out var binding)
                ? $"快捷键：{binding.DisplayText}"
                : "快捷键：尚未设定";
        }
    }

    private static string GetCommandDisplayName(OverlayCommand command) => command switch
    {
        OverlayCommand.ToggleRevealMap => "全境洞察",
        OverlayCommand.ToggleInfiniteMoney => "战略资金保障",
        OverlayCommand.ToggleCombatBoost => "绝对火力",
        OverlayCommand.ToggleCratePicker => "战利品搜寻",
        OverlayCommand.ToggleCrateRouteLines => "搜寻路线",
        OverlayCommand.ToggleMaximumPower => "电力永不熄灭",
        OverlayCommand.PromoteSelectedUnits => "百战精英",
        OverlayCommand.ArrangeSelectedFormation => "方阵集结",
        OverlayCommand.ToggleInstantBuild => "生产线全速运转",
        OverlayCommand.ToggleBuildAnywhere => "前线部署",
        OverlayCommand.ToggleAutoRepair => "战地维护",
        OverlayCommand.ToggleSuperWeaponNoCooldown => "终极武器随时待命",
        _ => "该功能"
    };

    private async Task CheckForUpdatesAsync()
    {
        if (updateCheckInProgress)
            return;
        updateCheckInProgress = true;
        SetUpdateStatus("正在联络更新服务器…");
        updateProgressBar.Visible = true;
        updateProgressBar.Style = ProgressBarStyle.Marquee;
        updateProgressBar.MarqueeAnimationSpeed = 28;
        try
        {
            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.All
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(12) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ra2-toolkit-update-checker/1.0.1");
            var (latest, downloadUrl) = await GetLatestReleaseAsync(client);
            var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version();
            if (latest > current)
            {
                SetUpdateStatus(
                    $"发现新版本 {latest.ToString(3)}，请尽快完成升级（点击下载）",
                    true, downloadUrl);
            }
            else
            {
                SetUpdateStatus(string.Empty);
            }
        }
        catch (Exception)
        {
            SetUpdateStatus("更新联络失败，请检查网络配置后重试", true, UpdateRetryTag);
        }
        finally
        {
            updateProgressBar.MarqueeAnimationSpeed = 0;
            updateProgressBar.Visible = false;
            updateCheckInProgress = false;
        }
    }

    private void SetUpdateStatus(string text, bool isLink = false, string? action = null)
    {
        updateStatusText = text;
        updateStatusIsLink = isLink;
        updateStatusAction = action;
        if (!operationStatusVisible)
            RenderUpdateStatus();
    }

    private void RenderUpdateStatus()
    {
        statusSpringLabel.ForeColor = SystemColors.ControlText;
        statusSpringLabel.IsLink = updateStatusIsLink;
        statusSpringLabel.Tag = updateStatusAction;
        statusSpringLabel.Text = updateStatusText;
    }

    private async Task ShowOperationStatusAsync(string message, bool isError)
    {
        operationStatusVisible = true;
        var sequence = ++operationStatusSequence;
        statusSpringLabel.IsLink = false;
        statusSpringLabel.Tag = null;
        statusSpringLabel.ForeColor = isError ? Color.Firebrick : Color.DarkGreen;
        statusSpringLabel.Text = message;
        await Task.Delay(TimeSpan.FromSeconds(4));
        if (IsDisposed || sequence != operationStatusSequence)
            return;
        operationStatusVisible = false;
        RenderUpdateStatus();
    }

    private static async Task<(Version Version, string DownloadUrl)> GetLatestReleaseAsync(
        HttpClient client)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            var tag = document.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v', 'V');
            var downloadUrl = document.RootElement.TryGetProperty("html_url", out var urlProperty)
                ? urlProperty.GetString()
                : null;
            if (!Version.TryParse(tag, out var version))
                throw new InvalidDataException("GitHub API 返回的版本号无效。");
            return (version, string.IsNullOrWhiteSpace(downloadUrl) ? ReleasesPageUrl : downloadUrl);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or
                                      JsonException or InvalidDataException)
        {
            using var response = await client.GetAsync(
                ReleasesPageUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var releaseUri = response.RequestMessage?.RequestUri
                ?? throw new InvalidDataException("GitHub Release 页面没有返回版本地址。");
            var tag = releaseUri.Segments.LastOrDefault()?.Trim('/').TrimStart('v', 'V');
            if (!Version.TryParse(tag, out var version))
                throw new InvalidDataException("GitHub Release 页面返回的版本号无效。");
            return (version, releaseUri.AbsoluteUri);
        }
    }

    private async void UpdateStatusLabel_Click(object? sender, EventArgs e)
    {
        if (statusSpringLabel.Tag is not string action)
            return;
        if (action == UpdateRetryTag)
        {
            await CheckForUpdatesAsync();
            return;
        }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(action)
        {
            UseShellExecute = true
        });
    }

    private void BugReportButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new BugReportDialog();
        dialog.ShowDialog(this);
    }

    private void RunOnUiThread(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
            return;
        if (InvokeRequired)
        {
            try { BeginInvoke(action); }
            catch (InvalidOperationException) { }
            return;
        }
        action();
    }
}

internal readonly record struct HotkeyBinding(uint Modifiers, Keys Key)
{
    internal const uint Alt = 0x0001;
    internal const uint Control = 0x0002;
    internal const uint Shift = 0x0004;

    public string DisplayText => string.Join("+", new[]
    {
        (Modifiers & Control) != 0 ? "Ctrl" : null,
        (Modifiers & Shift) != 0 ? "Shift" : null,
        (Modifiers & Alt) != 0 ? "Alt" : null,
        Key.ToString()
    }.Where(part => part is not null));
}

internal sealed class KeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private readonly HookProcedure procedure;
    private nint hook;

    internal KeyboardHook()
    {
        procedure = HookCallback;
        hook = SetWindowsHookEx(WhKeyboardLl, procedure, GetModuleHandle(null), 0);
        if (hook == 0)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    internal event Action<Keys, bool>? KeyChanged;

    internal static uint GetCurrentModifiers()
    {
        var modifiers = 0u;
        if ((GetAsyncKeyState((int)Keys.ControlKey) & 0x8000) != 0)
            modifiers |= HotkeyBinding.Control;
        if ((GetAsyncKeyState((int)Keys.ShiftKey) & 0x8000) != 0)
            modifiers |= HotkeyBinding.Shift;
        if ((GetAsyncKeyState((int)Keys.Menu) & 0x8000) != 0)
            modifiers |= HotkeyBinding.Alt;
        return modifiers;
    }

    private nint HookCallback(int code, nint message, nint data)
    {
        if (code >= 0)
        {
            var messageId = message.ToInt32();
            if (messageId is WmKeyDown or WmSysKeyDown or WmKeyUp or WmSysKeyUp)
            {
                var keyData = Marshal.PtrToStructure<LowLevelKeyboardInput>(data);
                try
                {
                    KeyChanged?.Invoke((Keys)keyData.VirtualKey,
                        messageId is WmKeyDown or WmSysKeyDown);
                }
                catch
                {
                    // 钩子回调必须始终把按键继续传给系统。
                }
            }
        }
        return CallNextHookEx(hook, code, message, data);
    }

    public void Dispose()
    {
        if (hook == 0)
            return;
        UnhookWindowsHookEx(hook);
        hook = 0;
        GC.SuppressFinalize(this);
    }

    private delegate nint HookProcedure(int code, nint message, nint data);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct LowLevelKeyboardInput
    {
        internal readonly uint VirtualKey;
        internal readonly uint ScanCode;
        internal readonly uint Flags;
        internal readonly uint Time;
        internal readonly nuint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int hookId, HookProcedure callback, nint module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
}

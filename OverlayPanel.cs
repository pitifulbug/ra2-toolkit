using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

internal enum OverlayCommand
{
    ToggleInfiniteMoney,
    ToggleCombatBoost,
    ToggleCratePicker,
    PlanPrismTower,
    PlanPatriotMissile,
    ClearBuildQueue,
    ExitProgram
}

internal readonly record struct OverlayState(
    bool InfiniteMoney,
    bool CombatBoost,
    bool CratePicker,
    bool PlanningPlacement,
    int PendingBuilds);

internal sealed class OverlayPanel : Form
{
    private readonly Action<OverlayCommand> dispatch;
    private readonly OverlayToggle moneyToggle;
    private readonly OverlayToggle combatToggle;
    private readonly OverlayToggle crateToggle;
    private readonly Label buildStatus;
    private readonly Label connectionStatus;
    private readonly RichTextBox logBox;
    private bool allowClose;

    public OverlayPanel(Action<OverlayCommand> dispatch)
    {
        this.dispatch = dispatch;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(242, 244, 248);
        ClientSize = new Size(520, 610);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimumSize = new Size(536, 649);
        ShowIcon = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        Text = "RA2 Toolkit 控制中心";

        var title = new Label
        {
            AutoSize = false,
            Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 37, 48),
            Location = new Point(24, 19),
            Size = new Size(300, 36),
            Text = "RA2 Toolkit 控制中心"
        };
        Controls.Add(title);

        connectionStatus = new Label
        {
            AutoSize = false,
            BackColor = Color.FromArgb(220, 244, 231),
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 123, 72),
            Location = new Point(365, 24),
            Size = new Size(130, 28),
            Text = "● 已连接游戏",
            TextAlign = ContentAlignment.MiddleCenter
        };
        Controls.Add(connectionStatus);

        var switchCaption = CreateCaption("功能开关", 72);
        Controls.Add(switchCaption);

        moneyToggle = CreateToggle("无限资金", "F2 · 低于 1 亿自动补满", 103, OverlayCommand.ToggleInfiniteMoney);
        combatToggle = CreateToggle("秒杀＋千倍防御", "F4 · 再按一次恢复", 157, OverlayCommand.ToggleCombatBoost);
        crateToggle = CreateToggle("自动捡箱", "F5 开始 / F6 暂停", 211, OverlayCommand.ToggleCratePicker);

        var buildCaption = CreateCaption("后台建造", 278);
        Controls.Add(buildCaption);

        buildStatus = new Label
        {
            AutoSize = false,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = Color.FromArgb(89, 98, 113),
            Location = new Point(111, 278),
            Size = new Size(384, 24),
            Text = "空闲",
            TextAlign = ContentAlignment.MiddleRight
        };
        Controls.Add(buildStatus);

        var prismButton = CreateButton("选择光棱塔  F7", new Point(24, 311), new Size(226, 42), Color.FromArgb(47, 139, 96));
        prismButton.Click += (_, _) => dispatch(OverlayCommand.PlanPrismTower);
        Controls.Add(prismButton);

        var patriotButton = CreateButton("选择爱国者  F8", new Point(270, 311), new Size(226, 42), Color.FromArgb(47, 112, 164));
        patriotButton.Click += (_, _) => dispatch(OverlayCommand.PlanPatriotMissile);
        Controls.Add(patriotButton);

        var clearButton = CreateButton("清空建造队列  F9", new Point(24, 365), new Size(472, 38), Color.FromArgb(91, 99, 113));
        clearButton.Click += (_, _) => dispatch(OverlayCommand.ClearBuildQueue);
        Controls.Add(clearButton);

        var logCaption = CreateCaption("操作日志", 422);
        Controls.Add(logCaption);

        logBox = new RichTextBox
        {
            BackColor = Color.FromArgb(30, 34, 42),
            BorderStyle = BorderStyle.None,
            DetectUrls = false,
            Font = new Font("Consolas", 9F),
            ForeColor = Color.FromArgb(218, 224, 234),
            Location = new Point(24, 453),
            ReadOnly = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            Size = new Size(472, 130),
            TabStop = false
        };
        Controls.Add(logBox);
    }

    public void UpdateState(OverlayState state)
    {
        RunOnUiThread(() =>
        {
            moneyToggle.Checked = state.InfiniteMoney;
            combatToggle.Checked = state.CombatBoost;
            crateToggle.Checked = state.CratePicker;
            buildStatus.Text = state.PlanningPlacement
                ? "正在游戏中选择部署坐标"
                : state.PendingBuilds > 0
                    ? $"待处理 {state.PendingBuilds} 个任务"
                    : "空闲";
        });
    }

    public void AppendLog(string message)
    {
        RunOnUiThread(() =>
        {
            var normalized = message.Replace("\r", string.Empty).TrimEnd('\n');
            if (normalized.Length == 0)
                return;
            logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {normalized}{Environment.NewLine}");
            logBox.SelectionStart = logBox.TextLength;
            logBox.ScrollToCaret();
        });
    }

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
            connectionStatus.BackColor = Color.FromArgb(252, 235, 210);
            connectionStatus.ForeColor = Color.FromArgb(155, 89, 26);
            connectionStatus.Text = "正在安全退出…";
            Enabled = false;
            dispatch(OverlayCommand.ExitProgram);
        }
        base.OnFormClosing(eventArgs);
    }

    private OverlayToggle CreateToggle(string text, string shortcut, int top, OverlayCommand command)
    {
        var toggle = new OverlayToggle(text, shortcut)
        {
            Location = new Point(24, top),
            Size = new Size(472, 46)
        };
        toggle.ToggleRequested += (_, _) => dispatch(command);
        Controls.Add(toggle);
        return toggle;
    }

    private static Label CreateCaption(string text, int top) => new()
    {
        AutoSize = false,
        Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
        ForeColor = Color.FromArgb(57, 65, 78),
        Location = new Point(24, top),
        Size = new Size(180, 25),
        Text = text
    };

    private static Button CreateButton(string text, Point location, Size size, Color color)
    {
        var button = new Button
        {
            BackColor = color,
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = location,
            Size = size,
            TabStop = false,
            Text = text,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(color, 0.15F);
        button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(color, 0.08F);
        return button;
    }

    private void RunOnUiThread(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
            return;
        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
                // The game or controller closed while an update was queued.
            }
            return;
        }
        action();
    }
}

internal sealed class OverlayToggle : Control
{
    private readonly Font shortcutFont;
    private bool isChecked;
    private bool hovered;

    public OverlayToggle(string text, string shortcut)
    {
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        Text = text;
        Shortcut = shortcut;
        Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
        shortcutFont = new Font("Microsoft YaHei UI", 8F);
        ForeColor = Color.FromArgb(36, 43, 54);
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
    }

    public event EventHandler? ToggleRequested;

    public string Shortcut { get; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Checked
    {
        get => isChecked;
        set
        {
            if (isChecked == value)
                return;
            isChecked = value;
            Invalidate();
        }
    }

    protected override void OnMouseEnter(EventArgs eventArgs)
    {
        hovered = true;
        Invalidate();
        base.OnMouseEnter(eventArgs);
    }

    protected override void OnMouseLeave(EventArgs eventArgs)
    {
        hovered = false;
        Invalidate();
        base.OnMouseLeave(eventArgs);
    }

    protected override void OnMouseUp(MouseEventArgs eventArgs)
    {
        base.OnMouseUp(eventArgs);
        if (eventArgs.Button == MouseButtons.Left && ClientRectangle.Contains(eventArgs.Location))
            ToggleRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        var graphics = eventArgs.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var rowBrush = new SolidBrush(hovered ? Color.White : Color.FromArgb(250, 251, 253));
        using var rowBorder = new Pen(Color.FromArgb(220, 224, 231));
        using var rowPath = RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 12);
        graphics.FillPath(rowBrush, rowPath);
        graphics.DrawPath(rowBorder, rowPath);

        TextRenderer.DrawText(graphics, Text, Font, new Rectangle(14, 4, Width - 100, 22), ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(graphics, Shortcut, shortcutFont, new Rectangle(14, 25, Width - 100, 17),
            Color.FromArgb(112, 121, 136), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        var switchRectangle = new Rectangle(Width - 66, 9, 52, 28);
        using var switchPath = RoundedRectangle(switchRectangle, 14);
        using var switchBrush = new SolidBrush(Checked
            ? Color.FromArgb(73, 190, 122)
            : Color.FromArgb(174, 180, 190));
        graphics.FillPath(switchBrush, switchPath);

        var knobX = Checked ? switchRectangle.Right - 25 : switchRectangle.Left + 3;
        using var knobBrush = new SolidBrush(Color.White);
        graphics.FillEllipse(knobBrush, knobX, switchRectangle.Top + 3, 22, 22);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            shortcutFont.Dispose();
        base.Dispose(disposing);
    }

    private static GraphicsPath RoundedRectangle(Rectangle rectangle, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class DesktopLogWriter(Action<string> append) : TextWriter
{
    public override Encoding Encoding => Encoding.UTF8;

    public override void WriteLine(string? value) => append(value ?? string.Empty);

    public override void Write(char value)
    {
        if (value != '\r' && value != '\n')
            append(value.ToString());
    }

    public override void Write(string? value)
    {
        if (!string.IsNullOrEmpty(value))
            append(value);
    }
}

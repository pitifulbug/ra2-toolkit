#nullable enable

partial class OverlayPanel
{
    private System.ComponentModel.IContainer? components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        mainTabControl = new TabControl();
        resourceTabPage = new TabPage();
        resourceGroupBox = new GroupBox();
        instantBuildHotkeyButton = new Button();
        powerHotkeyButton = new Button();
        moneyHotkeyButton = new Button();
        instantBuildCheckBox = new CheckBox();
        powerCheckBox = new CheckBox();
        moneyCheckBox = new CheckBox();
        combatTabPage = new TabPage();
        combatGroupBox = new GroupBox();
        formationHotkeyButton = new Button();
        formationButton = new Button();
        promoteHotkeyButton = new Button();
        superWeaponHotkeyButton = new Button();
        combatHotkeyButton = new Button();
        promoteButton = new Button();
        superWeaponCheckBox = new CheckBox();
        combatCheckBox = new CheckBox();
        mapTabPage = new TabPage();
        mapGroupBox = new GroupBox();
        autoRepairHotkeyButton = new Button();
        crateHotkeyButton = new Button();
        buildAnywhereHotkeyButton = new Button();
        revealMapHotkeyButton = new Button();
        autoRepairCheckBox = new CheckBox();
        crateCheckBox = new CheckBox();
        crateRouteLinesCheckBox = new CheckBox();
        buildAnywhereCheckBox = new CheckBox();
        revealMapCheckBox = new CheckBox();
        statusStrip = new StatusStrip();
        softwareVersionLabel = new ToolStripStatusLabel();
        statusSpringLabel = new ToolStripStatusLabel();
        updateProgressBar = new ToolStripProgressBar();
        bugReportButton = new ToolStripButton();
        mainTabControl.SuspendLayout();
        resourceTabPage.SuspendLayout();
        resourceGroupBox.SuspendLayout();
        combatTabPage.SuspendLayout();
        combatGroupBox.SuspendLayout();
        mapTabPage.SuspendLayout();
        mapGroupBox.SuspendLayout();
        statusStrip.SuspendLayout();
        SuspendLayout();
        // 
        // mainTabControl
        // 
        mainTabControl.Controls.Add(resourceTabPage);
        mainTabControl.Controls.Add(combatTabPage);
        mainTabControl.Controls.Add(mapTabPage);
        mainTabControl.Dock = DockStyle.Fill;
        mainTabControl.Location = new Point(0, 0);
        mainTabControl.Name = "mainTabControl";
        mainTabControl.SelectedIndex = 0;
        mainTabControl.Size = new Size(684, 439);
        mainTabControl.TabIndex = 0;
        // 
        // resourceTabPage
        // 
        resourceTabPage.Controls.Add(resourceGroupBox);
        resourceTabPage.Location = new Point(4, 26);
        resourceTabPage.Name = "resourceTabPage";
        resourceTabPage.Padding = new Padding(12);
        resourceTabPage.Size = new Size(676, 409);
        resourceTabPage.TabIndex = 0;
        resourceTabPage.Text = "战备资源";
        resourceTabPage.UseVisualStyleBackColor = true;
        // 
        // resourceGroupBox
        // 
        resourceGroupBox.Controls.Add(instantBuildHotkeyButton);
        resourceGroupBox.Controls.Add(powerHotkeyButton);
        resourceGroupBox.Controls.Add(moneyHotkeyButton);
        resourceGroupBox.Controls.Add(instantBuildCheckBox);
        resourceGroupBox.Controls.Add(powerCheckBox);
        resourceGroupBox.Controls.Add(moneyCheckBox);
        resourceGroupBox.Dock = DockStyle.Top;
        resourceGroupBox.Location = new Point(12, 12);
        resourceGroupBox.Name = "resourceGroupBox";
        resourceGroupBox.Size = new Size(652, 158);
        resourceGroupBox.TabIndex = 0;
        resourceGroupBox.TabStop = false;
        resourceGroupBox.Text = "后勤与生产调度";
        // 
        // instantBuildHotkeyButton
        // 
        instantBuildHotkeyButton.Location = new Point(430, 109);
        instantBuildHotkeyButton.Name = "instantBuildHotkeyButton";
        instantBuildHotkeyButton.Size = new Size(200, 28);
        instantBuildHotkeyButton.TabIndex = 5;
        instantBuildHotkeyButton.Text = "快捷键：尚未设定";
        instantBuildHotkeyButton.UseVisualStyleBackColor = true;
        instantBuildHotkeyButton.Click += HotkeyButton_Click;
        // 
        // powerHotkeyButton
        // 
        powerHotkeyButton.Location = new Point(430, 70);
        powerHotkeyButton.Name = "powerHotkeyButton";
        powerHotkeyButton.Size = new Size(200, 28);
        powerHotkeyButton.TabIndex = 3;
        powerHotkeyButton.Text = "快捷键：尚未设定";
        powerHotkeyButton.UseVisualStyleBackColor = true;
        powerHotkeyButton.Click += HotkeyButton_Click;
        // 
        // moneyHotkeyButton
        // 
        moneyHotkeyButton.Location = new Point(430, 31);
        moneyHotkeyButton.Name = "moneyHotkeyButton";
        moneyHotkeyButton.Size = new Size(200, 28);
        moneyHotkeyButton.TabIndex = 1;
        moneyHotkeyButton.Text = "快捷键：尚未设定";
        moneyHotkeyButton.UseVisualStyleBackColor = true;
        moneyHotkeyButton.Click += HotkeyButton_Click;
        // 
        // instantBuildCheckBox
        // 
        instantBuildCheckBox.AutoCheck = false;
        instantBuildCheckBox.AutoSize = true;
        instantBuildCheckBox.Location = new Point(18, 113);
        instantBuildCheckBox.Name = "instantBuildCheckBox";
        instantBuildCheckBox.Size = new Size(207, 21);
        instantBuildCheckBox.TabIndex = 4;
        instantBuildCheckBox.Text = "生产线全速运转（瞬间完成建造）";
        instantBuildCheckBox.UseVisualStyleBackColor = true;
        instantBuildCheckBox.Click += FeatureCheckBox_Click;
        // 
        // powerCheckBox
        // 
        powerCheckBox.AutoCheck = false;
        powerCheckBox.AutoSize = true;
        powerCheckBox.Location = new Point(18, 74);
        powerCheckBox.Name = "powerCheckBox";
        powerCheckBox.Size = new Size(219, 21);
        powerCheckBox.TabIndex = 2;
        powerCheckBox.Text = "电力永不熄灭（自动覆盖全局耗电）";
        powerCheckBox.UseVisualStyleBackColor = true;
        powerCheckBox.Click += FeatureCheckBox_Click;
        // 
        // moneyCheckBox
        // 
        moneyCheckBox.AutoCheck = false;
        moneyCheckBox.AutoSize = true;
        moneyCheckBox.Location = new Point(18, 35);
        moneyCheckBox.Name = "moneyCheckBox";
        moneyCheckBox.Size = new Size(232, 21);
        moneyCheckBox.TabIndex = 0;
        moneyCheckBox.Text = "战略资金保障（余额不低于 100,000）";
        moneyCheckBox.UseVisualStyleBackColor = true;
        moneyCheckBox.Click += FeatureCheckBox_Click;
        // 
        // combatTabPage
        // 
        combatTabPage.Controls.Add(combatGroupBox);
        combatTabPage.Location = new Point(4, 26);
        combatTabPage.Name = "combatTabPage";
        combatTabPage.Padding = new Padding(12);
        combatTabPage.Size = new Size(676, 411);
        combatTabPage.TabIndex = 1;
        combatTabPage.Text = "作战指挥";
        combatTabPage.UseVisualStyleBackColor = true;
        // 
        // combatGroupBox
        // 
        combatGroupBox.Controls.Add(formationHotkeyButton);
        combatGroupBox.Controls.Add(formationButton);
        combatGroupBox.Controls.Add(promoteHotkeyButton);
        combatGroupBox.Controls.Add(superWeaponHotkeyButton);
        combatGroupBox.Controls.Add(combatHotkeyButton);
        combatGroupBox.Controls.Add(promoteButton);
        combatGroupBox.Controls.Add(superWeaponCheckBox);
        combatGroupBox.Controls.Add(combatCheckBox);
        combatGroupBox.Dock = DockStyle.Top;
        combatGroupBox.Location = new Point(12, 12);
        combatGroupBox.Name = "combatGroupBox";
        combatGroupBox.Size = new Size(652, 197);
        combatGroupBox.TabIndex = 0;
        combatGroupBox.TabStop = false;
        combatGroupBox.Text = "部队与火力指挥";
        // 
        // formationHotkeyButton
        // 
        formationHotkeyButton.Location = new Point(430, 148);
        formationHotkeyButton.Name = "formationHotkeyButton";
        formationHotkeyButton.Size = new Size(200, 28);
        formationHotkeyButton.TabIndex = 7;
        formationHotkeyButton.Text = "快捷键：尚未设定";
        formationHotkeyButton.UseVisualStyleBackColor = true;
        formationHotkeyButton.Click += HotkeyButton_Click;
        // 
        // formationButton
        // 
        formationButton.Location = new Point(18, 148);
        formationButton.Name = "formationButton";
        formationButton.Size = new Size(220, 28);
        formationButton.TabIndex = 6;
        formationButton.Text = "方阵集结（选中单位整齐列阵）";
        formationButton.UseVisualStyleBackColor = true;
        formationButton.Click += FormationButton_Click;
        // 
        // promoteHotkeyButton
        // 
        promoteHotkeyButton.Location = new Point(430, 109);
        promoteHotkeyButton.Name = "promoteHotkeyButton";
        promoteHotkeyButton.Size = new Size(200, 28);
        promoteHotkeyButton.TabIndex = 5;
        promoteHotkeyButton.Text = "快捷键：尚未设定";
        promoteHotkeyButton.UseVisualStyleBackColor = true;
        promoteHotkeyButton.Click += HotkeyButton_Click;
        // 
        // superWeaponHotkeyButton
        // 
        superWeaponHotkeyButton.Location = new Point(430, 70);
        superWeaponHotkeyButton.Name = "superWeaponHotkeyButton";
        superWeaponHotkeyButton.Size = new Size(200, 28);
        superWeaponHotkeyButton.TabIndex = 3;
        superWeaponHotkeyButton.Text = "快捷键：尚未设定";
        superWeaponHotkeyButton.UseVisualStyleBackColor = true;
        superWeaponHotkeyButton.Click += HotkeyButton_Click;
        // 
        // combatHotkeyButton
        // 
        combatHotkeyButton.Location = new Point(430, 31);
        combatHotkeyButton.Name = "combatHotkeyButton";
        combatHotkeyButton.Size = new Size(200, 28);
        combatHotkeyButton.TabIndex = 1;
        combatHotkeyButton.Text = "快捷键：尚未设定";
        combatHotkeyButton.UseVisualStyleBackColor = true;
        combatHotkeyButton.Click += HotkeyButton_Click;
        // 
        // promoteButton
        // 
        promoteButton.Location = new Point(18, 109);
        promoteButton.Name = "promoteButton";
        promoteButton.Size = new Size(220, 28);
        promoteButton.TabIndex = 4;
        promoteButton.Text = "百战精英（选中单位晋升三级）";
        promoteButton.UseVisualStyleBackColor = true;
        promoteButton.Click += PromoteButton_Click;
        // 
        // superWeaponCheckBox
        // 
        superWeaponCheckBox.AutoCheck = false;
        superWeaponCheckBox.AutoSize = true;
        superWeaponCheckBox.Location = new Point(18, 74);
        superWeaponCheckBox.Name = "superWeaponCheckBox";
        superWeaponCheckBox.Size = new Size(219, 21);
        superWeaponCheckBox.TabIndex = 2;
        superWeaponCheckBox.Text = "终极武器随时待命（取消冷却时间）";
        superWeaponCheckBox.UseVisualStyleBackColor = true;
        superWeaponCheckBox.Click += FeatureCheckBox_Click;
        // 
        // combatCheckBox
        // 
        combatCheckBox.AutoCheck = false;
        combatCheckBox.AutoSize = true;
        combatCheckBox.Location = new Point(18, 35);
        combatCheckBox.Name = "combatCheckBox";
        combatCheckBox.Size = new Size(188, 21);
        combatCheckBox.TabIndex = 0;
        combatCheckBox.Text = "绝对火力（秒杀 + 千倍防御）";
        combatCheckBox.UseVisualStyleBackColor = true;
        combatCheckBox.Click += FeatureCheckBox_Click;
        // 
        // mapTabPage
        // 
        mapTabPage.Controls.Add(mapGroupBox);
        mapTabPage.Location = new Point(4, 26);
        mapTabPage.Name = "mapTabPage";
        mapTabPage.Padding = new Padding(12);
        mapTabPage.Size = new Size(676, 411);
        mapTabPage.TabIndex = 2;
        mapTabPage.Text = "战场态势";
        mapTabPage.UseVisualStyleBackColor = true;
        // 
        // mapGroupBox
        // 
        mapGroupBox.Controls.Add(autoRepairHotkeyButton);
        mapGroupBox.Controls.Add(crateHotkeyButton);
        mapGroupBox.Controls.Add(buildAnywhereHotkeyButton);
        mapGroupBox.Controls.Add(revealMapHotkeyButton);
        mapGroupBox.Controls.Add(autoRepairCheckBox);
        mapGroupBox.Controls.Add(crateCheckBox);
        mapGroupBox.Controls.Add(crateRouteLinesCheckBox);
        mapGroupBox.Controls.Add(buildAnywhereCheckBox);
        mapGroupBox.Controls.Add(revealMapCheckBox);
        mapGroupBox.Dock = DockStyle.Top;
        mapGroupBox.Location = new Point(12, 12);
        mapGroupBox.Name = "mapGroupBox";
        mapGroupBox.Size = new Size(652, 237);
        mapGroupBox.TabIndex = 0;
        mapGroupBox.TabStop = false;
        mapGroupBox.Text = "战场洞察与自动化";
        // 
        // autoRepairHotkeyButton
        // 
        autoRepairHotkeyButton.Location = new Point(430, 148);
        autoRepairHotkeyButton.Name = "autoRepairHotkeyButton";
        autoRepairHotkeyButton.Size = new Size(200, 28);
        autoRepairHotkeyButton.TabIndex = 7;
        autoRepairHotkeyButton.Text = "快捷键：尚未设定";
        autoRepairHotkeyButton.UseVisualStyleBackColor = true;
        autoRepairHotkeyButton.Click += HotkeyButton_Click;
        // 
        // crateHotkeyButton
        // 
        crateHotkeyButton.Location = new Point(430, 109);
        crateHotkeyButton.Name = "crateHotkeyButton";
        crateHotkeyButton.Size = new Size(200, 28);
        crateHotkeyButton.TabIndex = 5;
        crateHotkeyButton.Text = "快捷键：尚未设定";
        crateHotkeyButton.UseVisualStyleBackColor = true;
        crateHotkeyButton.Click += HotkeyButton_Click;
        // 
        // buildAnywhereHotkeyButton
        // 
        buildAnywhereHotkeyButton.Location = new Point(430, 70);
        buildAnywhereHotkeyButton.Name = "buildAnywhereHotkeyButton";
        buildAnywhereHotkeyButton.Size = new Size(200, 28);
        buildAnywhereHotkeyButton.TabIndex = 3;
        buildAnywhereHotkeyButton.Text = "快捷键：尚未设定";
        buildAnywhereHotkeyButton.UseVisualStyleBackColor = true;
        buildAnywhereHotkeyButton.Click += HotkeyButton_Click;
        // 
        // revealMapHotkeyButton
        // 
        revealMapHotkeyButton.Location = new Point(430, 31);
        revealMapHotkeyButton.Name = "revealMapHotkeyButton";
        revealMapHotkeyButton.Size = new Size(200, 28);
        revealMapHotkeyButton.TabIndex = 1;
        revealMapHotkeyButton.Text = "快捷键：尚未设定";
        revealMapHotkeyButton.UseVisualStyleBackColor = true;
        revealMapHotkeyButton.Click += HotkeyButton_Click;
        // 
        // autoRepairCheckBox
        // 
        autoRepairCheckBox.AutoCheck = false;
        autoRepairCheckBox.AutoSize = true;
        autoRepairCheckBox.Location = new Point(18, 152);
        autoRepairCheckBox.Name = "autoRepairCheckBox";
        autoRepairCheckBox.Size = new Size(195, 21);
        autoRepairCheckBox.TabIndex = 6;
        autoRepairCheckBox.Text = "战地维护（自动修复受损建筑）";
        autoRepairCheckBox.UseVisualStyleBackColor = true;
        autoRepairCheckBox.Click += FeatureCheckBox_Click;
        // 
        // crateCheckBox
        // 
        crateCheckBox.AutoCheck = false;
        crateCheckBox.AutoSize = true;
        crateCheckBox.Location = new Point(18, 113);
        crateCheckBox.Name = "crateCheckBox";
        crateCheckBox.Size = new Size(255, 21);
        crateCheckBox.TabIndex = 4;
        crateCheckBox.Text = "战利品搜寻（快捷键单击启用、双击停止）";
        crateCheckBox.UseVisualStyleBackColor = true;
        crateCheckBox.Click += FeatureCheckBox_Click;
        // 
        // crateRouteLinesCheckBox
        // 
        crateRouteLinesCheckBox.AutoCheck = false;
        crateRouteLinesCheckBox.AutoSize = true;
        crateRouteLinesCheckBox.Location = new Point(18, 191);
        crateRouteLinesCheckBox.Name = "crateRouteLinesCheckBox";
        crateRouteLinesCheckBox.Size = new Size(207, 21);
        crateRouteLinesCheckBox.TabIndex = 8;
        crateRouteLinesCheckBox.Text = "显示搜寻路线（原生集合点样式）";
        crateRouteLinesCheckBox.UseVisualStyleBackColor = true;
        crateRouteLinesCheckBox.Click += FeatureCheckBox_Click;
        // 
        // buildAnywhereCheckBox
        // 
        buildAnywhereCheckBox.AutoCheck = false;
        buildAnywhereCheckBox.AutoSize = true;
        buildAnywhereCheckBox.Location = new Point(18, 74);
        buildAnywhereCheckBox.Name = "buildAnywhereCheckBox";
        buildAnywhereCheckBox.Size = new Size(195, 21);
        buildAnywhereCheckBox.TabIndex = 2;
        buildAnywhereCheckBox.Text = "前线部署（解除基地建造范围）";
        buildAnywhereCheckBox.UseVisualStyleBackColor = true;
        buildAnywhereCheckBox.Click += FeatureCheckBox_Click;
        // 
        // revealMapCheckBox
        // 
        revealMapCheckBox.AutoCheck = false;
        revealMapCheckBox.AutoSize = true;
        revealMapCheckBox.Location = new Point(18, 35);
        revealMapCheckBox.Name = "revealMapCheckBox";
        revealMapCheckBox.Size = new Size(231, 21);
        revealMapCheckBox.TabIndex = 0;
        revealMapCheckBox.Text = "全境洞察（解除战争迷雾并显示单位）";
        revealMapCheckBox.UseVisualStyleBackColor = true;
        revealMapCheckBox.Click += FeatureCheckBox_Click;
        // 
        // statusStrip
        // 
        statusStrip.Items.AddRange(new ToolStripItem[] { softwareVersionLabel, statusSpringLabel, updateProgressBar, bugReportButton });
        statusStrip.Location = new Point(0, 439);
        statusStrip.Name = "statusStrip";
        statusStrip.Size = new Size(684, 25);
        statusStrip.SizingGrip = false;
        statusStrip.TabIndex = 1;
        // 
        // softwareVersionLabel
        // 
        softwareVersionLabel.Name = "softwareVersionLabel";
        softwareVersionLabel.Size = new Size(95, 20);
        softwareVersionLabel.Text = "当前版本：1.0.1";
        // 
        // statusSpringLabel
        // 
        statusSpringLabel.Name = "statusSpringLabel";
        statusSpringLabel.Size = new Size(381, 20);
        statusSpringLabel.Spring = true;
        statusSpringLabel.TextAlign = ContentAlignment.MiddleRight;
        statusSpringLabel.Click += UpdateStatusLabel_Click;
        // 
        // updateProgressBar
        // 
        updateProgressBar.Name = "updateProgressBar";
        updateProgressBar.Size = new Size(100, 19);
        updateProgressBar.Visible = false;
        // 
        // bugReportButton
        // 
        bugReportButton.Name = "bugReportButton";
        bugReportButton.Size = new Size(60, 23);
        bugReportButton.Text = "提交情报";
        bugReportButton.Click += BugReportButton_Click;
        // 
        // OverlayPanel
        // 
        AutoScaleDimensions = new SizeF(7F, 17F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(684, 464);
        Controls.Add(mainTabControl);
        Controls.Add(statusStrip);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Name = "OverlayPanel";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "RA2 Toolkit";
        mainTabControl.ResumeLayout(false);
        resourceTabPage.ResumeLayout(false);
        resourceGroupBox.ResumeLayout(false);
        resourceGroupBox.PerformLayout();
        combatTabPage.ResumeLayout(false);
        combatGroupBox.ResumeLayout(false);
        combatGroupBox.PerformLayout();
        mapTabPage.ResumeLayout(false);
        mapGroupBox.ResumeLayout(false);
        mapGroupBox.PerformLayout();
        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    private TabControl mainTabControl = null!;
    private TabPage resourceTabPage = null!;
    private GroupBox resourceGroupBox = null!;
    private Button instantBuildHotkeyButton = null!;
    private Button powerHotkeyButton = null!;
    private Button moneyHotkeyButton = null!;
    private CheckBox instantBuildCheckBox = null!;
    private CheckBox powerCheckBox = null!;
    private CheckBox moneyCheckBox = null!;
    private TabPage combatTabPage = null!;
    private GroupBox combatGroupBox = null!;
    private Button formationHotkeyButton = null!;
    private Button formationButton = null!;
    private Button promoteHotkeyButton = null!;
    private Button superWeaponHotkeyButton = null!;
    private Button combatHotkeyButton = null!;
    private Button promoteButton = null!;
    private CheckBox superWeaponCheckBox = null!;
    private CheckBox combatCheckBox = null!;
    private TabPage mapTabPage = null!;
    private GroupBox mapGroupBox = null!;
    private Button autoRepairHotkeyButton = null!;
    private Button crateHotkeyButton = null!;
    private Button buildAnywhereHotkeyButton = null!;
    private Button revealMapHotkeyButton = null!;
    private CheckBox autoRepairCheckBox = null!;
    private CheckBox crateCheckBox = null!;
    private CheckBox crateRouteLinesCheckBox = null!;
    private CheckBox buildAnywhereCheckBox = null!;
    private CheckBox revealMapCheckBox = null!;
    private StatusStrip statusStrip = null!;
    private ToolStripStatusLabel softwareVersionLabel = null!;
    private ToolStripStatusLabel statusSpringLabel = null!;
    private ToolStripProgressBar updateProgressBar = null!;
    private ToolStripButton bugReportButton = null!;
}

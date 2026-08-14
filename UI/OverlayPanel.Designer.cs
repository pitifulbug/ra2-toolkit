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
        fullTechHotkeyButton = new Button();
        unlimitedProductionHotkeyButton = new Button();
        powerHotkeyButton = new Button();
        moneyHotkeyButton = new Button();
        instantBuildCheckBox = new CheckBox();
        fullTechCheckBox = new CheckBox();
        unlimitedProductionCheckBox = new CheckBox();
        powerCheckBox = new CheckBox();
        moneyCheckBox = new CheckBox();
        constructionTabPage = new TabPage();
        constructionGroupBox = new GroupBox();
        combatTabPage = new TabPage();
        combatGroupBox = new GroupBox();
        formationHotkeyButton = new Button();
        infiniteRangeHotkeyButton = new Button();
        formationCheckBox = new CheckBox();
        infiniteRangeCheckBox = new CheckBox();
        promoteHotkeyButton = new Button();
        chronoLegionnaireHotkeyButton = new Button();
        superWeaponHotkeyButton = new Button();
        highDefenseHotkeyButton = new Button();
        combatHotkeyButton = new Button();
        promoteCheckBox = new CheckBox();
        chronoLegionnaireCheckBox = new CheckBox();
        superWeaponCheckBox = new CheckBox();
        highDefenseCheckBox = new CheckBox();
        combatCheckBox = new CheckBox();
        mapTabPage = new TabPage();
        mapGroupBox = new GroupBox();
        crateRouteLinesHotkeyButton = new Button();
        autoRepairHotkeyButton = new Button();
        crateHotkeyButton = new Button();
        buildAnywhereHotkeyButton = new Button();
        revealMapHotkeyButton = new Button();
        autoRepairCheckBox = new CheckBox();
        crateCheckBox = new CheckBox();
        crateRouteLinesCheckBox = new CheckBox();
        buildAnywhereCheckBox = new CheckBox();
        revealMapCheckBox = new CheckBox();
        funTabPage = new TabPage();
        funGroupBox = new GroupBox();
        spinningMcvHotkeyButton = new Button();
        spinningMcvCheckBox = new CheckBox();
        autoBuildTabPage = new TabPage();
        autoBuildGroupBox = new GroupBox();
        teslaCoilHotkeyButton = new Button();
        prismTowerHotkeyButton = new Button();
        patriotMissileHotkeyButton = new Button();
        flakCannonHotkeyButton = new Button();
        teslaCoilLabel = new Label();
        prismTowerLabel = new Label();
        patriotMissileLabel = new Label();
        flakCannonLabel = new Label();
        autoBuildHintLabel = new Label();
        statusStrip = new StatusStrip();
        softwareVersionLabel = new ToolStripStatusLabel();
        statusSpringLabel = new ToolStripStatusLabel();
        updateProgressBar = new ToolStripProgressBar();
        bugReportButton = new ToolStripButton();
        mainTabControl.SuspendLayout();
        resourceTabPage.SuspendLayout();
        resourceGroupBox.SuspendLayout();
        constructionTabPage.SuspendLayout();
        constructionGroupBox.SuspendLayout();
        combatTabPage.SuspendLayout();
        combatGroupBox.SuspendLayout();
        mapTabPage.SuspendLayout();
        mapGroupBox.SuspendLayout();
        funTabPage.SuspendLayout();
        funGroupBox.SuspendLayout();
        autoBuildTabPage.SuspendLayout();
        autoBuildGroupBox.SuspendLayout();
        statusStrip.SuspendLayout();
        SuspendLayout();
        // 
        // mainTabControl
        // 
        mainTabControl.Controls.Add(resourceTabPage);
        mainTabControl.Controls.Add(constructionTabPage);
        mainTabControl.Controls.Add(autoBuildTabPage);
        mainTabControl.Controls.Add(combatTabPage);
        mainTabControl.Controls.Add(mapTabPage);
        mainTabControl.Controls.Add(funTabPage);
        mainTabControl.Dock = DockStyle.Fill;
        mainTabControl.Location = new Point(0, 0);
        mainTabControl.Name = "mainTabControl";
        mainTabControl.SelectedIndex = 0;
        mainTabControl.Size = new Size(554, 369);
        mainTabControl.TabIndex = 0;
        // 
        // resourceTabPage
        // 
        resourceTabPage.Controls.Add(resourceGroupBox);
        resourceTabPage.Location = new Point(4, 26);
        resourceTabPage.Name = "resourceTabPage";
        resourceTabPage.Padding = new Padding(12);
        resourceTabPage.Size = new Size(546, 339);
        resourceTabPage.TabIndex = 0;
        resourceTabPage.Text = "资源";
        resourceTabPage.UseVisualStyleBackColor = true;
        // 
        // resourceGroupBox
        // 
        resourceGroupBox.Controls.Add(powerHotkeyButton);
        resourceGroupBox.Controls.Add(moneyHotkeyButton);
        resourceGroupBox.Controls.Add(powerCheckBox);
        resourceGroupBox.Controls.Add(moneyCheckBox);
        resourceGroupBox.Dock = DockStyle.Top;
        resourceGroupBox.Location = new Point(12, 12);
        resourceGroupBox.Name = "resourceGroupBox";
        resourceGroupBox.Size = new Size(522, 120);
        resourceGroupBox.TabIndex = 0;
        resourceGroupBox.TabStop = false;
        resourceGroupBox.Text = "资源功能";
        // 
        // constructionTabPage
        // 
        constructionTabPage.Controls.Add(constructionGroupBox);
        constructionTabPage.Location = new Point(4, 26);
        constructionTabPage.Name = "constructionTabPage";
        constructionTabPage.Padding = new Padding(12);
        constructionTabPage.Size = new Size(546, 339);
        constructionTabPage.TabIndex = 1;
        constructionTabPage.Text = "建造";
        constructionTabPage.UseVisualStyleBackColor = true;
        // 
        // constructionGroupBox
        // 
        constructionGroupBox.Controls.Add(autoRepairHotkeyButton);
        constructionGroupBox.Controls.Add(buildAnywhereHotkeyButton);
        constructionGroupBox.Controls.Add(unlimitedProductionHotkeyButton);
        constructionGroupBox.Controls.Add(fullTechHotkeyButton);
        constructionGroupBox.Controls.Add(instantBuildHotkeyButton);
        constructionGroupBox.Controls.Add(autoRepairCheckBox);
        constructionGroupBox.Controls.Add(buildAnywhereCheckBox);
        constructionGroupBox.Controls.Add(unlimitedProductionCheckBox);
        constructionGroupBox.Controls.Add(fullTechCheckBox);
        constructionGroupBox.Controls.Add(instantBuildCheckBox);
        constructionGroupBox.Dock = DockStyle.Top;
        constructionGroupBox.Location = new Point(12, 12);
        constructionGroupBox.Name = "constructionGroupBox";
        constructionGroupBox.Size = new Size(522, 237);
        constructionGroupBox.TabIndex = 0;
        constructionGroupBox.TabStop = false;
        constructionGroupBox.Text = "基地建设";
        // 
        // instantBuildHotkeyButton
        // 
        instantBuildHotkeyButton.Location = new Point(300, 31);
        instantBuildHotkeyButton.Name = "instantBuildHotkeyButton";
        instantBuildHotkeyButton.Size = new Size(200, 28);
        instantBuildHotkeyButton.TabIndex = 1;
        instantBuildHotkeyButton.Text = "快捷键：尚未设定";
        instantBuildHotkeyButton.UseVisualStyleBackColor = true;
        instantBuildHotkeyButton.Click += HotkeyButton_Click;
        // 
        // fullTechHotkeyButton
        // 
        fullTechHotkeyButton.Location = new Point(300, 70);
        fullTechHotkeyButton.Name = "fullTechHotkeyButton";
        fullTechHotkeyButton.Size = new Size(200, 28);
        fullTechHotkeyButton.TabIndex = 3;
        fullTechHotkeyButton.Text = "快捷键：尚未设定";
        fullTechHotkeyButton.UseVisualStyleBackColor = true;
        fullTechHotkeyButton.Click += HotkeyButton_Click;
        // 
        // unlimitedProductionHotkeyButton
        // 
        unlimitedProductionHotkeyButton.Location = new Point(300, 109);
        unlimitedProductionHotkeyButton.Name = "unlimitedProductionHotkeyButton";
        unlimitedProductionHotkeyButton.Size = new Size(200, 28);
        unlimitedProductionHotkeyButton.TabIndex = 5;
        unlimitedProductionHotkeyButton.Text = "快捷键：尚未设定";
        unlimitedProductionHotkeyButton.UseVisualStyleBackColor = true;
        unlimitedProductionHotkeyButton.Click += HotkeyButton_Click;
        // 
        // powerHotkeyButton
        // 
        powerHotkeyButton.Location = new Point(300, 70);
        powerHotkeyButton.Name = "powerHotkeyButton";
        powerHotkeyButton.Size = new Size(200, 28);
        powerHotkeyButton.TabIndex = 3;
        powerHotkeyButton.Text = "快捷键：尚未设定";
        powerHotkeyButton.UseVisualStyleBackColor = true;
        powerHotkeyButton.Click += HotkeyButton_Click;
        // 
        // moneyHotkeyButton
        // 
        moneyHotkeyButton.Location = new Point(300, 31);
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
        instantBuildCheckBox.Location = new Point(18, 35);
        instantBuildCheckBox.Name = "instantBuildCheckBox";
        instantBuildCheckBox.Size = new Size(207, 21);
        instantBuildCheckBox.TabIndex = 0;
        instantBuildCheckBox.Text = "快速建造（单位与建筑瞬间完成）";
        instantBuildCheckBox.UseVisualStyleBackColor = true;
        instantBuildCheckBox.Click += FeatureCheckBox_Click;
        // 
        // fullTechCheckBox
        // 
        fullTechCheckBox.AutoCheck = false;
        fullTechCheckBox.AutoSize = true;
        fullTechCheckBox.Location = new Point(18, 74);
        fullTechCheckBox.Name = "fullTechCheckBox";
        fullTechCheckBox.Size = new Size(219, 21);
        fullTechCheckBox.TabIndex = 2;
        fullTechCheckBox.Text = "全科技解锁（无视阵营与科技要求）";
        fullTechCheckBox.UseVisualStyleBackColor = true;
        fullTechCheckBox.Click += FeatureCheckBox_Click;
        // 
        // unlimitedProductionCheckBox
        // 
        unlimitedProductionCheckBox.AutoCheck = false;
        unlimitedProductionCheckBox.AutoSize = true;
        unlimitedProductionCheckBox.Location = new Point(18, 113);
        unlimitedProductionCheckBox.Name = "unlimitedProductionCheckBox";
        unlimitedProductionCheckBox.Size = new Size(195, 21);
        unlimitedProductionCheckBox.TabIndex = 4;
        unlimitedProductionCheckBox.Text = "无限生产（解除建造数量上限）";
        unlimitedProductionCheckBox.UseVisualStyleBackColor = true;
        unlimitedProductionCheckBox.Click += FeatureCheckBox_Click;
        // 
        // powerCheckBox
        // 
        powerCheckBox.AutoCheck = false;
        powerCheckBox.AutoSize = true;
        powerCheckBox.Location = new Point(18, 74);
        powerCheckBox.Name = "powerCheckBox";
        powerCheckBox.Size = new Size(195, 21);
        powerCheckBox.TabIndex = 2;
        powerCheckBox.Text = "无限电力（始终满足基地耗电）";
        powerCheckBox.UseVisualStyleBackColor = true;
        powerCheckBox.Click += FeatureCheckBox_Click;
        // 
        // moneyCheckBox
        // 
        moneyCheckBox.AutoCheck = false;
        moneyCheckBox.AutoSize = true;
        moneyCheckBox.Location = new Point(18, 35);
        moneyCheckBox.Name = "moneyCheckBox";
        moneyCheckBox.Size = new Size(220, 21);
        moneyCheckBox.TabIndex = 0;
        moneyCheckBox.Text = "无限金钱（余额自动补至 100,000）";
        moneyCheckBox.UseVisualStyleBackColor = true;
        moneyCheckBox.Click += FeatureCheckBox_Click;
        // 
        // combatTabPage
        // 
        combatTabPage.Controls.Add(combatGroupBox);
        combatTabPage.Location = new Point(4, 26);
        combatTabPage.Name = "combatTabPage";
        combatTabPage.Padding = new Padding(12);
        combatTabPage.Size = new Size(546, 339);
        combatTabPage.TabIndex = 3;
        combatTabPage.Text = "战斗";
        combatTabPage.UseVisualStyleBackColor = true;
        // 
        // combatGroupBox
        // 
        combatGroupBox.Controls.Add(formationHotkeyButton);
        combatGroupBox.Controls.Add(infiniteRangeHotkeyButton);
        combatGroupBox.Controls.Add(formationCheckBox);
        combatGroupBox.Controls.Add(infiniteRangeCheckBox);
        combatGroupBox.Controls.Add(promoteHotkeyButton);
        combatGroupBox.Controls.Add(chronoLegionnaireHotkeyButton);
        combatGroupBox.Controls.Add(superWeaponHotkeyButton);
        combatGroupBox.Controls.Add(highDefenseHotkeyButton);
        combatGroupBox.Controls.Add(combatHotkeyButton);
        combatGroupBox.Controls.Add(promoteCheckBox);
        combatGroupBox.Controls.Add(chronoLegionnaireCheckBox);
        combatGroupBox.Controls.Add(superWeaponCheckBox);
        combatGroupBox.Controls.Add(highDefenseCheckBox);
        combatGroupBox.Controls.Add(combatCheckBox);
        combatGroupBox.Dock = DockStyle.Top;
        combatGroupBox.Location = new Point(12, 12);
        combatGroupBox.Name = "combatGroupBox";
        combatGroupBox.Size = new Size(522, 314);
        combatGroupBox.TabIndex = 0;
        combatGroupBox.TabStop = false;
        combatGroupBox.Text = "战斗功能";
        // 
        // formationHotkeyButton
        // 
        formationHotkeyButton.Location = new Point(300, 187);
        formationHotkeyButton.Name = "formationHotkeyButton";
        formationHotkeyButton.Size = new Size(200, 28);
        formationHotkeyButton.TabIndex = 9;
        formationHotkeyButton.Text = "快捷键：尚未设定";
        formationHotkeyButton.UseVisualStyleBackColor = true;
        formationHotkeyButton.Click += HotkeyButton_Click;
        // 
        // infiniteRangeHotkeyButton
        // 
        infiniteRangeHotkeyButton.Location = new Point(300, 265);
        infiniteRangeHotkeyButton.Name = "infiniteRangeHotkeyButton";
        infiniteRangeHotkeyButton.Size = new Size(200, 28);
        infiniteRangeHotkeyButton.TabIndex = 13;
        infiniteRangeHotkeyButton.Text = "快捷键：尚未设定";
        infiniteRangeHotkeyButton.UseVisualStyleBackColor = true;
        infiniteRangeHotkeyButton.Click += HotkeyButton_Click;
        // 
        // formationCheckBox
        // 
        formationCheckBox.AutoCheck = false;
        formationCheckBox.AutoSize = true;
        formationCheckBox.Location = new Point(18, 191);
        formationCheckBox.Name = "formationCheckBox";
        formationCheckBox.Size = new Size(219, 21);
        formationCheckBox.TabIndex = 8;
        formationCheckBox.Text = "方阵排列（用快捷键排列选中单位）";
        formationCheckBox.UseVisualStyleBackColor = true;
        formationCheckBox.Click += FeatureCheckBox_Click;
        // 
        // infiniteRangeCheckBox
        // 
        infiniteRangeCheckBox.AutoCheck = false;
        infiniteRangeCheckBox.AutoSize = true;
        infiniteRangeCheckBox.Location = new Point(18, 269);
        infiniteRangeCheckBox.Name = "infiniteRangeCheckBox";
        infiniteRangeCheckBox.Size = new Size(219, 21);
        infiniteRangeCheckBox.TabIndex = 12;
        infiniteRangeCheckBox.Text = "无限射程（用快捷键切换选中单位）";
        infiniteRangeCheckBox.UseVisualStyleBackColor = true;
        infiniteRangeCheckBox.Click += FeatureCheckBox_Click;
        // 
        // promoteHotkeyButton
        // 
        promoteHotkeyButton.Location = new Point(300, 148);
        promoteHotkeyButton.Name = "promoteHotkeyButton";
        promoteHotkeyButton.Size = new Size(200, 28);
        promoteHotkeyButton.TabIndex = 7;
        promoteHotkeyButton.Text = "快捷键：尚未设定";
        promoteHotkeyButton.UseVisualStyleBackColor = true;
        promoteHotkeyButton.Click += HotkeyButton_Click;
        // 
        // chronoLegionnaireHotkeyButton
        // 
        chronoLegionnaireHotkeyButton.Location = new Point(300, 226);
        chronoLegionnaireHotkeyButton.Name = "chronoLegionnaireHotkeyButton";
        chronoLegionnaireHotkeyButton.Size = new Size(200, 28);
        chronoLegionnaireHotkeyButton.TabIndex = 11;
        chronoLegionnaireHotkeyButton.Text = "快捷键：尚未设定";
        chronoLegionnaireHotkeyButton.UseVisualStyleBackColor = true;
        chronoLegionnaireHotkeyButton.Click += HotkeyButton_Click;
        // 
        // superWeaponHotkeyButton
        // 
        superWeaponHotkeyButton.Location = new Point(300, 109);
        superWeaponHotkeyButton.Name = "superWeaponHotkeyButton";
        superWeaponHotkeyButton.Size = new Size(200, 28);
        superWeaponHotkeyButton.TabIndex = 5;
        superWeaponHotkeyButton.Text = "快捷键：尚未设定";
        superWeaponHotkeyButton.UseVisualStyleBackColor = true;
        superWeaponHotkeyButton.Click += HotkeyButton_Click;
        // 
        // highDefenseHotkeyButton
        // 
        highDefenseHotkeyButton.Location = new Point(300, 70);
        highDefenseHotkeyButton.Name = "highDefenseHotkeyButton";
        highDefenseHotkeyButton.Size = new Size(200, 28);
        highDefenseHotkeyButton.TabIndex = 3;
        highDefenseHotkeyButton.Text = "快捷键：尚未设定";
        highDefenseHotkeyButton.UseVisualStyleBackColor = true;
        highDefenseHotkeyButton.Click += HotkeyButton_Click;
        // 
        // combatHotkeyButton
        // 
        combatHotkeyButton.Location = new Point(300, 31);
        combatHotkeyButton.Name = "combatHotkeyButton";
        combatHotkeyButton.Size = new Size(200, 28);
        combatHotkeyButton.TabIndex = 1;
        combatHotkeyButton.Text = "快捷键：尚未设定";
        combatHotkeyButton.UseVisualStyleBackColor = true;
        combatHotkeyButton.Click += HotkeyButton_Click;
        // 
        // promoteCheckBox
        // 
        promoteCheckBox.AutoCheck = false;
        promoteCheckBox.AutoSize = true;
        promoteCheckBox.Location = new Point(18, 152);
        promoteCheckBox.Name = "promoteCheckBox";
        promoteCheckBox.Size = new Size(231, 21);
        promoteCheckBox.TabIndex = 6;
        promoteCheckBox.Text = "全员三级（现有及新造单位自动满级）";
        promoteCheckBox.UseVisualStyleBackColor = true;
        promoteCheckBox.Click += FeatureCheckBox_Click;
        // 
        // chronoLegionnaireCheckBox
        // 
        chronoLegionnaireCheckBox.AutoSize = true;
        chronoLegionnaireCheckBox.Location = new Point(18, 230);
        chronoLegionnaireCheckBox.Name = "chronoLegionnaireCheckBox";
        chronoLegionnaireCheckBox.Size = new Size(231, 21);
        chronoLegionnaireCheckBox.TabIndex = 10;
        chronoLegionnaireCheckBox.Text = "超时空无冷却（攻击、传送立即恢复）";
        chronoLegionnaireCheckBox.UseVisualStyleBackColor = true;
        chronoLegionnaireCheckBox.Click += FeatureCheckBox_Click;
        // 
        // superWeaponCheckBox
        // 
        superWeaponCheckBox.AutoCheck = false;
        superWeaponCheckBox.AutoSize = true;
        superWeaponCheckBox.Location = new Point(18, 113);
        superWeaponCheckBox.Name = "superWeaponCheckBox";
        superWeaponCheckBox.Size = new Size(219, 21);
        superWeaponCheckBox.TabIndex = 4;
        superWeaponCheckBox.Text = "超级武器无冷却（建成后始终就绪）";
        superWeaponCheckBox.UseVisualStyleBackColor = true;
        superWeaponCheckBox.Click += FeatureCheckBox_Click;
        // 
        // highDefenseCheckBox
        // 
        highDefenseCheckBox.AutoCheck = false;
        highDefenseCheckBox.AutoSize = true;
        highDefenseCheckBox.Location = new Point(18, 74);
        highDefenseCheckBox.Name = "highDefenseCheckBox";
        highDefenseCheckBox.Size = new Size(248, 21);
        highDefenseCheckBox.TabIndex = 2;
        highDefenseCheckBox.Text = "极高防御（己方单位与建筑护甲 ×1000）";
        highDefenseCheckBox.UseVisualStyleBackColor = true;
        highDefenseCheckBox.Click += FeatureCheckBox_Click;
        // 
        // combatCheckBox
        // 
        combatCheckBox.AutoCheck = false;
        combatCheckBox.AutoSize = true;
        combatCheckBox.Location = new Point(18, 35);
        combatCheckBox.Name = "combatCheckBox";
        combatCheckBox.Size = new Size(248, 21);
        combatCheckBox.TabIndex = 0;
        combatCheckBox.Text = "一击必杀（己方单位与建筑火力 ×1000）";
        combatCheckBox.UseVisualStyleBackColor = true;
        combatCheckBox.Click += FeatureCheckBox_Click;
        // 
        // mapTabPage
        // 
        mapTabPage.Controls.Add(mapGroupBox);
        mapTabPage.Location = new Point(4, 26);
        mapTabPage.Name = "mapTabPage";
        mapTabPage.Padding = new Padding(12);
        mapTabPage.Size = new Size(546, 339);
        mapTabPage.TabIndex = 4;
        mapTabPage.Text = "地图与采集";
        mapTabPage.UseVisualStyleBackColor = true;
        // 
        // mapGroupBox
        // 
        mapGroupBox.Controls.Add(crateRouteLinesHotkeyButton);
        mapGroupBox.Controls.Add(crateHotkeyButton);
        mapGroupBox.Controls.Add(revealMapHotkeyButton);
        mapGroupBox.Controls.Add(crateCheckBox);
        mapGroupBox.Controls.Add(crateRouteLinesCheckBox);
        mapGroupBox.Controls.Add(revealMapCheckBox);
        mapGroupBox.Dock = DockStyle.Top;
        mapGroupBox.Location = new Point(12, 12);
        mapGroupBox.Name = "mapGroupBox";
        mapGroupBox.Size = new Size(522, 159);
        mapGroupBox.TabIndex = 0;
        mapGroupBox.TabStop = false;
        mapGroupBox.Text = "地图与资源采集";
        // 
        // crateRouteLinesHotkeyButton
        // 
        crateRouteLinesHotkeyButton.Location = new Point(300, 109);
        crateRouteLinesHotkeyButton.Name = "crateRouteLinesHotkeyButton";
        crateRouteLinesHotkeyButton.Size = new Size(200, 28);
        crateRouteLinesHotkeyButton.TabIndex = 5;
        crateRouteLinesHotkeyButton.Text = "快捷键：尚未设定";
        crateRouteLinesHotkeyButton.UseVisualStyleBackColor = true;
        crateRouteLinesHotkeyButton.Click += HotkeyButton_Click;
        // 
        // autoRepairHotkeyButton
        // 
        autoRepairHotkeyButton.Location = new Point(300, 187);
        autoRepairHotkeyButton.Name = "autoRepairHotkeyButton";
        autoRepairHotkeyButton.Size = new Size(200, 28);
        autoRepairHotkeyButton.TabIndex = 9;
        autoRepairHotkeyButton.Text = "快捷键：尚未设定";
        autoRepairHotkeyButton.UseVisualStyleBackColor = true;
        autoRepairHotkeyButton.Click += HotkeyButton_Click;
        // 
        // crateHotkeyButton
        // 
        crateHotkeyButton.Location = new Point(300, 70);
        crateHotkeyButton.Name = "crateHotkeyButton";
        crateHotkeyButton.Size = new Size(200, 28);
        crateHotkeyButton.TabIndex = 3;
        crateHotkeyButton.Text = "快捷键：尚未设定";
        crateHotkeyButton.UseVisualStyleBackColor = true;
        crateHotkeyButton.Click += HotkeyButton_Click;
        // 
        // buildAnywhereHotkeyButton
        // 
        buildAnywhereHotkeyButton.Location = new Point(300, 148);
        buildAnywhereHotkeyButton.Name = "buildAnywhereHotkeyButton";
        buildAnywhereHotkeyButton.Size = new Size(200, 28);
        buildAnywhereHotkeyButton.TabIndex = 7;
        buildAnywhereHotkeyButton.Text = "快捷键：尚未设定";
        buildAnywhereHotkeyButton.UseVisualStyleBackColor = true;
        buildAnywhereHotkeyButton.Click += HotkeyButton_Click;
        // 
        // revealMapHotkeyButton
        // 
        revealMapHotkeyButton.Location = new Point(300, 31);
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
        autoRepairCheckBox.Location = new Point(18, 191);
        autoRepairCheckBox.Name = "autoRepairCheckBox";
        autoRepairCheckBox.Size = new Size(219, 21);
        autoRepairCheckBox.TabIndex = 8;
        autoRepairCheckBox.Text = "自动维修（受损建筑自动开始维修）";
        autoRepairCheckBox.UseVisualStyleBackColor = true;
        autoRepairCheckBox.Click += FeatureCheckBox_Click;
        // 
        // crateCheckBox
        // 
        crateCheckBox.AutoCheck = false;
        crateCheckBox.AutoSize = true;
        crateCheckBox.Location = new Point(18, 74);
        crateCheckBox.Name = "crateCheckBox";
        crateCheckBox.Size = new Size(219, 21);
        crateCheckBox.TabIndex = 2;
        crateCheckBox.Text = "自动捡箱（选中单位自动寻找箱子）";
        crateCheckBox.UseVisualStyleBackColor = true;
        crateCheckBox.Click += FeatureCheckBox_Click;
        // 
        // crateRouteLinesCheckBox
        // 
        crateRouteLinesCheckBox.AutoCheck = false;
        crateRouteLinesCheckBox.AutoSize = true;
        crateRouteLinesCheckBox.Location = new Point(18, 113);
        crateRouteLinesCheckBox.Name = "crateRouteLinesCheckBox";
        crateRouteLinesCheckBox.Size = new Size(231, 21);
        crateRouteLinesCheckBox.TabIndex = 4;
        crateRouteLinesCheckBox.Text = "捡箱路线（显示单位前往箱子的路线）";
        crateRouteLinesCheckBox.UseVisualStyleBackColor = true;
        crateRouteLinesCheckBox.Click += FeatureCheckBox_Click;
        // 
        // buildAnywhereCheckBox
        // 
        buildAnywhereCheckBox.AutoCheck = false;
        buildAnywhereCheckBox.AutoSize = true;
        buildAnywhereCheckBox.Location = new Point(18, 152);
        buildAnywhereCheckBox.Name = "buildAnywhereCheckBox";
        buildAnywhereCheckBox.Size = new Size(219, 21);
        buildAnywhereCheckBox.TabIndex = 6;
        buildAnywhereCheckBox.Text = "随处建造（取消基地邻近范围限制）";
        buildAnywhereCheckBox.UseVisualStyleBackColor = true;
        buildAnywhereCheckBox.Click += FeatureCheckBox_Click;
        // 
        // revealMapCheckBox
        // 
        revealMapCheckBox.AutoCheck = false;
        revealMapCheckBox.AutoSize = true;
        revealMapCheckBox.Location = new Point(18, 35);
        revealMapCheckBox.Name = "revealMapCheckBox";
        revealMapCheckBox.Size = new Size(195, 21);
        revealMapCheckBox.TabIndex = 0;
        revealMapCheckBox.Text = "地图全开（立即揭开整张地图）";
        revealMapCheckBox.UseVisualStyleBackColor = true;
        revealMapCheckBox.Click += FeatureCheckBox_Click;
        // 
        // funTabPage
        // 
        funTabPage.Controls.Add(funGroupBox);
        funTabPage.Location = new Point(4, 26);
        funTabPage.Name = "funTabPage";
        funTabPage.Padding = new Padding(12);
        funTabPage.Size = new Size(546, 339);
        funTabPage.TabIndex = 5;
        funTabPage.Text = "游戏";
        funTabPage.UseVisualStyleBackColor = true;
        // 
        // funGroupBox
        // 
        funGroupBox.Controls.Add(spinningMcvHotkeyButton);
        funGroupBox.Controls.Add(spinningMcvCheckBox);
        funGroupBox.Dock = DockStyle.Top;
        funGroupBox.Location = new Point(12, 12);
        funGroupBox.Name = "funGroupBox";
        funGroupBox.Size = new Size(522, 72);
        funGroupBox.TabIndex = 0;
        funGroupBox.TabStop = false;
        funGroupBox.Text = "游戏控制与娱乐";
        // 
        // 
        // spinningMcvHotkeyButton
        // 
        spinningMcvHotkeyButton.Location = new Point(300, 31);
        spinningMcvHotkeyButton.Name = "spinningMcvHotkeyButton";
        spinningMcvHotkeyButton.Size = new Size(200, 28);
        spinningMcvHotkeyButton.TabIndex = 1;
        spinningMcvHotkeyButton.Text = "快捷键：尚未设定";
        spinningMcvHotkeyButton.UseVisualStyleBackColor = true;
        spinningMcvHotkeyButton.Click += HotkeyButton_Click;
        // 
        // 
        // spinningMcvCheckBox
        // 
        spinningMcvCheckBox.AutoCheck = false;
        spinningMcvCheckBox.AutoSize = true;
        spinningMcvCheckBox.Location = new Point(18, 35);
        spinningMcvCheckBox.Name = "spinningMcvCheckBox";
        spinningMcvCheckBox.Size = new Size(243, 21);
        spinningMcvCheckBox.TabIndex = 0;
        spinningMcvCheckBox.Text = "基地车旋转（用快捷键控制选中基地车）";
        spinningMcvCheckBox.UseVisualStyleBackColor = true;
        spinningMcvCheckBox.Click += FeatureCheckBox_Click;
        // 
        // autoBuildTabPage
        // 
        autoBuildTabPage.Controls.Add(autoBuildGroupBox);
        autoBuildTabPage.Location = new Point(4, 26);
        autoBuildTabPage.Name = "autoBuildTabPage";
        autoBuildTabPage.Padding = new Padding(12);
        autoBuildTabPage.Size = new Size(546, 339);
        autoBuildTabPage.TabIndex = 2;
        autoBuildTabPage.Text = "自动建造";
        autoBuildTabPage.UseVisualStyleBackColor = true;
        // 
        // autoBuildGroupBox
        // 
        autoBuildGroupBox.Controls.Add(teslaCoilHotkeyButton);
        autoBuildGroupBox.Controls.Add(prismTowerHotkeyButton);
        autoBuildGroupBox.Controls.Add(patriotMissileHotkeyButton);
        autoBuildGroupBox.Controls.Add(flakCannonHotkeyButton);
        autoBuildGroupBox.Controls.Add(teslaCoilLabel);
        autoBuildGroupBox.Controls.Add(prismTowerLabel);
        autoBuildGroupBox.Controls.Add(patriotMissileLabel);
        autoBuildGroupBox.Controls.Add(flakCannonLabel);
        autoBuildGroupBox.Controls.Add(autoBuildHintLabel);
        autoBuildGroupBox.Dock = DockStyle.Top;
        autoBuildGroupBox.Location = new Point(12, 12);
        autoBuildGroupBox.Name = "autoBuildGroupBox";
        autoBuildGroupBox.Size = new Size(522, 237);
        autoBuildGroupBox.TabIndex = 0;
        autoBuildGroupBox.TabStop = false;
        autoBuildGroupBox.Text = "围绕选中建筑自动建造";
        // 
        // teslaCoilHotkeyButton
        // 
        teslaCoilHotkeyButton.Location = new Point(300, 187);
        teslaCoilHotkeyButton.Name = "teslaCoilHotkeyButton";
        teslaCoilHotkeyButton.Size = new Size(200, 28);
        teslaCoilHotkeyButton.TabIndex = 8;
        teslaCoilHotkeyButton.Text = "快捷键：尚未设定";
        teslaCoilHotkeyButton.UseVisualStyleBackColor = true;
        teslaCoilHotkeyButton.Click += HotkeyButton_Click;
        // 
        // prismTowerHotkeyButton
        // 
        prismTowerHotkeyButton.Location = new Point(300, 148);
        prismTowerHotkeyButton.Name = "prismTowerHotkeyButton";
        prismTowerHotkeyButton.Size = new Size(200, 28);
        prismTowerHotkeyButton.TabIndex = 6;
        prismTowerHotkeyButton.Text = "快捷键：尚未设定";
        prismTowerHotkeyButton.UseVisualStyleBackColor = true;
        prismTowerHotkeyButton.Click += HotkeyButton_Click;
        // 
        // patriotMissileHotkeyButton
        // 
        patriotMissileHotkeyButton.Location = new Point(300, 109);
        patriotMissileHotkeyButton.Name = "patriotMissileHotkeyButton";
        patriotMissileHotkeyButton.Size = new Size(200, 28);
        patriotMissileHotkeyButton.TabIndex = 4;
        patriotMissileHotkeyButton.Text = "快捷键：尚未设定";
        patriotMissileHotkeyButton.UseVisualStyleBackColor = true;
        patriotMissileHotkeyButton.Click += HotkeyButton_Click;
        // 
        // flakCannonHotkeyButton
        // 
        flakCannonHotkeyButton.Location = new Point(300, 70);
        flakCannonHotkeyButton.Name = "flakCannonHotkeyButton";
        flakCannonHotkeyButton.Size = new Size(200, 28);
        flakCannonHotkeyButton.TabIndex = 2;
        flakCannonHotkeyButton.Text = "快捷键：尚未设定";
        flakCannonHotkeyButton.UseVisualStyleBackColor = true;
        flakCannonHotkeyButton.Click += HotkeyButton_Click;
        // 
        // teslaCoilLabel
        // 
        teslaCoilLabel.AutoSize = true;
        teslaCoilLabel.Location = new Point(18, 192);
        teslaCoilLabel.Name = "teslaCoilLabel";
        teslaCoilLabel.Size = new Size(56, 17);
        teslaCoilLabel.TabIndex = 7;
        teslaCoilLabel.Text = "磁暴线圈";
        // 
        // prismTowerLabel
        // 
        prismTowerLabel.AutoSize = true;
        prismTowerLabel.Location = new Point(18, 153);
        prismTowerLabel.Name = "prismTowerLabel";
        prismTowerLabel.Size = new Size(44, 17);
        prismTowerLabel.TabIndex = 5;
        prismTowerLabel.Text = "光棱塔";
        // 
        // patriotMissileLabel
        // 
        patriotMissileLabel.AutoSize = true;
        patriotMissileLabel.Location = new Point(18, 114);
        patriotMissileLabel.Name = "patriotMissileLabel";
        patriotMissileLabel.Size = new Size(68, 17);
        patriotMissileLabel.TabIndex = 3;
        patriotMissileLabel.Text = "爱国者导弹";
        // 
        // flakCannonLabel
        // 
        flakCannonLabel.AutoSize = true;
        flakCannonLabel.Location = new Point(18, 75);
        flakCannonLabel.Name = "flakCannonLabel";
        flakCannonLabel.Size = new Size(44, 17);
        flakCannonLabel.TabIndex = 1;
        flakCannonLabel.Text = "防空炮";
        // 
        // autoBuildHintLabel
        // 
        autoBuildHintLabel.AutoSize = true;
        autoBuildHintLabel.Location = new Point(18, 31);
        autoBuildHintLabel.Name = "autoBuildHintLabel";
        autoBuildHintLabel.Size = new Size(320, 17);
        autoBuildHintLabel.TabIndex = 0;
        autoBuildHintLabel.Text = "选中己方建筑后按快捷键；再次按任一建造快捷键可停止。";
        // 
        // statusStrip
        // 
        statusStrip.Items.AddRange(new ToolStripItem[] { softwareVersionLabel, statusSpringLabel, updateProgressBar, bugReportButton });
        statusStrip.Location = new Point(0, 369);
        statusStrip.Name = "statusStrip";
        statusStrip.Size = new Size(554, 23);
        statusStrip.SizingGrip = false;
        statusStrip.TabIndex = 1;
        // 
        // softwareVersionLabel
        // 
        softwareVersionLabel.Name = "softwareVersionLabel";
        softwareVersionLabel.Size = new Size(95, 18);
        softwareVersionLabel.Text = "当前版本：1.0.2";
        softwareVersionLabel.Click += softwareVersionLabel_Click;
        // 
        // statusSpringLabel
        // 
        statusSpringLabel.Name = "statusSpringLabel";
        statusSpringLabel.Size = new Size(384, 18);
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
        bugReportButton.Size = new Size(60, 21);
        bugReportButton.Text = "报告问题";
        bugReportButton.Click += BugReportButton_Click;
        // 
        // OverlayPanel
        // 
        AutoScaleDimensions = new SizeF(7F, 17F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(554, 392);
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
        constructionTabPage.ResumeLayout(false);
        constructionGroupBox.ResumeLayout(false);
        constructionGroupBox.PerformLayout();
        combatTabPage.ResumeLayout(false);
        combatGroupBox.ResumeLayout(false);
        combatGroupBox.PerformLayout();
        mapTabPage.ResumeLayout(false);
        mapGroupBox.ResumeLayout(false);
        mapGroupBox.PerformLayout();
        funTabPage.ResumeLayout(false);
        funGroupBox.ResumeLayout(false);
        funGroupBox.PerformLayout();
        autoBuildTabPage.ResumeLayout(false);
        autoBuildGroupBox.ResumeLayout(false);
        autoBuildGroupBox.PerformLayout();
        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    private TabControl mainTabControl = null!;
    private TabPage resourceTabPage = null!;
    private GroupBox resourceGroupBox = null!;
    private Button instantBuildHotkeyButton = null!;
    private Button fullTechHotkeyButton = null!;
    private Button unlimitedProductionHotkeyButton = null!;
    private Button powerHotkeyButton = null!;
    private Button moneyHotkeyButton = null!;
    private CheckBox instantBuildCheckBox = null!;
    private CheckBox fullTechCheckBox = null!;
    private CheckBox unlimitedProductionCheckBox = null!;
    private CheckBox powerCheckBox = null!;
    private CheckBox moneyCheckBox = null!;
    private TabPage constructionTabPage = null!;
    private GroupBox constructionGroupBox = null!;
    private TabPage combatTabPage = null!;
    private GroupBox combatGroupBox = null!;
    private Button formationHotkeyButton = null!;
    private Button infiniteRangeHotkeyButton = null!;
    private CheckBox formationCheckBox = null!;
    private CheckBox infiniteRangeCheckBox = null!;
    private Button promoteHotkeyButton = null!;
    private Button superWeaponHotkeyButton = null!;
    private Button highDefenseHotkeyButton = null!;
    private Button combatHotkeyButton = null!;
    private CheckBox promoteCheckBox = null!;
    private CheckBox superWeaponCheckBox = null!;
    private CheckBox highDefenseCheckBox = null!;
    private CheckBox combatCheckBox = null!;
    private TabPage mapTabPage = null!;
    private GroupBox mapGroupBox = null!;
    private Button crateRouteLinesHotkeyButton = null!;
    private Button autoRepairHotkeyButton = null!;
    private Button crateHotkeyButton = null!;
    private Button buildAnywhereHotkeyButton = null!;
    private Button revealMapHotkeyButton = null!;
    private CheckBox autoRepairCheckBox = null!;
    private CheckBox crateCheckBox = null!;
    private CheckBox crateRouteLinesCheckBox = null!;
    private CheckBox buildAnywhereCheckBox = null!;
    private CheckBox revealMapCheckBox = null!;
    private TabPage funTabPage = null!;
    private GroupBox funGroupBox = null!;
    private Button spinningMcvHotkeyButton = null!;
    private CheckBox spinningMcvCheckBox = null!;
    private TabPage autoBuildTabPage = null!;
    private GroupBox autoBuildGroupBox = null!;
    private Button teslaCoilHotkeyButton = null!;
    private Button prismTowerHotkeyButton = null!;
    private Button patriotMissileHotkeyButton = null!;
    private Button flakCannonHotkeyButton = null!;
    private Label teslaCoilLabel = null!;
    private Label prismTowerLabel = null!;
    private Label patriotMissileLabel = null!;
    private Label flakCannonLabel = null!;
    private Label autoBuildHintLabel = null!;
    private Button chronoLegionnaireHotkeyButton = null!;
    private CheckBox chronoLegionnaireCheckBox = null!;
    private StatusStrip statusStrip = null!;
    private ToolStripStatusLabel softwareVersionLabel = null!;
    private ToolStripStatusLabel statusSpringLabel = null!;
    private ToolStripProgressBar updateProgressBar = null!;
    private ToolStripButton bugReportButton = null!;
}

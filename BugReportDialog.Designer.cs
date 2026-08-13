#nullable enable

partial class BugReportDialog
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
        reportTypeLabel = new Label();
        reportTypeComboBox = new ComboBox();
        SuspendLayout();
        // 
        // reportTypeLabel
        // 
        reportTypeLabel.AutoSize = true;
        reportTypeLabel.Location = new Point(20, 22);
        reportTypeLabel.Name = "reportTypeLabel";
        reportTypeLabel.Size = new Size(68, 17);
        reportTypeLabel.TabIndex = 0;
        reportTypeLabel.Text = "情报类型：";
        // 
        // reportTypeComboBox
        // 
        reportTypeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        reportTypeComboBox.FormattingEnabled = true;
        reportTypeComboBox.Items.AddRange(new object[] { "错误", "意见" });
        reportTypeComboBox.Location = new Point(94, 18);
        reportTypeComboBox.Name = "reportTypeComboBox";
        reportTypeComboBox.Size = new Size(328, 25);
        reportTypeComboBox.TabIndex = 1;
        reportTypeComboBox.SelectedIndexChanged += ReportTypeComboBox_SelectedIndexChanged;
        // 
        // BugReportDialog
        // 
        AutoScaleDimensions = new SizeF(7F, 17F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(442, 64);
        Controls.Add(reportTypeComboBox);
        Controls.Add(reportTypeLabel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "BugReportDialog";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "提交战场情报";
        ResumeLayout(false);
        PerformLayout();
    }

    private Label reportTypeLabel = null!;
    private ComboBox reportTypeComboBox = null!;
}

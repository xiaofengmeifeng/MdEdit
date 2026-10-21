namespace MdEditor.Forms;

/// <summary>
/// 简单的文本输入对话框（用于重命名等场景）。
/// </summary>
internal static class PromptDialog
{
    public static string? Show(IWin32Window owner, string title, string label, string initial)
    {
        using var form = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(360, 122)
        };

        var labelControl = new Label { Text = label, Left = 12, Top = 14, Width = 336, AutoSize = false };
        var textBox = new TextBox { Text = initial, Left = 12, Top = 40, Width = 336 };
        var okButton = new Button { Text = "确定", DialogResult = DialogResult.OK, Left = 190, Top = 76, Width = 75 };
        var cancelButton = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Left = 273, Top = 76, Width = 75 };

        form.Controls.Add(labelControl);
        form.Controls.Add(textBox);
        form.Controls.Add(okButton);
        form.Controls.Add(cancelButton);
        form.AcceptButton = okButton;
        form.CancelButton = cancelButton;
        textBox.SelectAll();

        return form.ShowDialog(owner) == DialogResult.OK ? textBox.Text : null;
    }
}

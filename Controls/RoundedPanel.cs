using System.Drawing.Drawing2D;

namespace MdEditor.Controls;

/// <summary>
/// 圆角面板（用于搜索框、查找框等输入框外框）。
/// </summary>
internal sealed class RoundedPanel : Panel
{
    public RoundedPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);
    }

    /// <summary>圆角半径。</summary>
    public int Radius { get; set; } = 8;

    /// <summary>边框颜色。</summary>
    public Color BorderColor { get; set; } = Color.FromArgb(208, 215, 222);

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? Color.White);

        var rect = ClientRectangle;
        rect.Width -= 1;
        rect.Height -= 1;

        using var path = CreateRoundedPath(rect, Radius);
        using var fill = new SolidBrush(BackColor);
        e.Graphics.FillPath(fill, path);
        using var pen = new Pen(BorderColor);
        e.Graphics.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedPath(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

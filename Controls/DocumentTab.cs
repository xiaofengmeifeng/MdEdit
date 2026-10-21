using System.Drawing.Drawing2D;

namespace MdEditor.Controls;

/// <summary>
/// 文档标签：文件名 + 关闭按钮，支持激活状态与未保存标记。
/// </summary>
internal sealed class DocumentTab : Control
{
    private const int CloseAreaWidth = 22;
    private const int MaxWidth = 240;
    private const int MinWidth = 96;

    private static readonly Color InactiveBack = Color.FromArgb(238, 241, 245);
    private static readonly Color HoverBack = Color.FromArgb(228, 233, 240);
    private static readonly Color Accent = Color.FromArgb(79, 110, 247);
    private static readonly Color TextActive = Color.FromArgb(31, 35, 40);
    private static readonly Color TextInactive = Color.FromArgb(87, 96, 106);
    private static readonly Color CloseGlyph = Color.FromArgb(110, 116, 124);
    private static readonly Color CloseHoverBack = Color.FromArgb(210, 216, 224);

    private readonly Font _boldFont;
    private bool _hover;
    private bool _closeHover;

    public DocumentTab(string title)
    {
        Title = title;
        Height = 30;
        TabStop = false;
        Font = new Font("Microsoft YaHei UI", 9f);
        _boldFont = new Font(Font, FontStyle.Bold);
        Margin = new Padding(0, 0, 2, 0);
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        RecomputeWidth();
    }

    /// <summary>标签标题（文件名）。</summary>
    public string Title { get; }

    /// <summary>是否为当前活动标签。</summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            Invalidate();
        }
    }

    /// <summary>是否有未保存修改（标题后显示 *）。</summary>
    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (_isDirty == value)
            {
                return;
            }

            _isDirty = value;
            RecomputeWidth();
            Invalidate();
        }
    }

    private bool _isActive;
    private bool _isDirty;

    /// <summary>点击标签（切换文档）。</summary>
    public event EventHandler? Activated;

    /// <summary>点击关闭按钮。</summary>
    public event EventHandler? CloseRequested;

    /// <summary>右键点击（弹出上下文菜单）。</summary>
    public event EventHandler? ContextMenuRequested;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var body = new Rectangle(0, 0, Width, Height - 2);
        var background = IsActive ? Color.White : _hover ? HoverBack : InactiveBack;
        using (var brush = new SolidBrush(background))
        {
            g.FillRectangle(brush, body);
        }

        if (IsActive)
        {
            using var accent = new SolidBrush(Accent);
            g.FillRectangle(accent, 0, Height - 2, Width, 2);
        }

        var text = Title + (IsDirty ? " *" : string.Empty);
        var textRect = new Rectangle(10, 0, Width - CloseAreaWidth - 12, Height);
        TextRenderer.DrawText(g, text, IsActive ? _boldFont : Font, textRect,
            IsActive ? TextActive : TextInactive,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        var closeRect = new Rectangle(Width - CloseAreaWidth + 2, (Height - 16) / 2, 16, 16);
        if (_closeHover)
        {
            using var circle = new SolidBrush(CloseHoverBack);
            g.FillEllipse(circle, closeRect);
        }

        using var pen = new Pen(_closeHover ? TextActive : CloseGlyph, 1.4f);
        const int pad = 5;
        g.DrawLine(pen, closeRect.Left + pad, closeRect.Top + pad, closeRect.Right - pad, closeRect.Bottom - pad);
        g.DrawLine(pen, closeRect.Right - pad, closeRect.Top + pad, closeRect.Left + pad, closeRect.Bottom - pad);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var closeHover = e.X >= Width - CloseAreaWidth;
        if (!_hover || _closeHover != closeHover)
        {
            _hover = true;
            _closeHover = closeHover;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        _closeHover = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButtons.Right)
        {
            ContextMenuRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (e.X >= Width - CloseAreaWidth)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            Activated?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _boldFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void RecomputeWidth()
    {
        var text = Title + (IsDirty ? " *" : string.Empty);
        var size = TextRenderer.MeasureText(text, _boldFont);
        Width = Math.Min(MaxWidth, Math.Max(MinWidth, size.Width + 18 + CloseAreaWidth + 8));
    }
}

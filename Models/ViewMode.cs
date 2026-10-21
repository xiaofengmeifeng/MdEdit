namespace MdEditor.Models;

/// <summary>
/// 主窗口的视图模式。
/// </summary>
public enum ViewMode
{
    /// <summary>仅显示编辑器。</summary>
    Edit,

    /// <summary>编辑与预览分栏显示。</summary>
    Split,

    /// <summary>仅显示预览。</summary>
    Preview,
}

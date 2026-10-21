namespace MdEditor.Models;

/// <summary>
/// 表示一个已打开的文档。文本内容始终以 CRLF 保存在内存中（用于 WinForms 文本框）。
/// </summary>
public sealed class OpenDocument
{
    /// <summary>文件完整路径；未命名文档为 <c>null</c>。</summary>
    public string? Path { get; set; }

    /// <summary>当前文本内容（CRLF 换行）。</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>是否有未保存的修改。</summary>
    public bool IsDirty { get; set; }

    /// <summary>该文件原始的换行符（保存时还原）。</summary>
    public string LineEnding { get; set; } = "\r\n";

    /// <summary>最近一次从磁盘读取/保存时的文件写入时间（用于检测外部修改）。</summary>
    public DateTime LastWriteTimeUtc { get; set; }

    /// <summary>在标签上显示的名称。</summary>
    public string DisplayName => Path is null ? "未命名" : System.IO.Path.GetFileName(Path);
}

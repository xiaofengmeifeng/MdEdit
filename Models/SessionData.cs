namespace MdEditor.Models;

/// <summary>
/// 会话记录：上次关闭时打开的文件、当前文件以及文件夹。
/// </summary>
public sealed class SessionData
{
    /// <summary>打开的文件路径列表。</summary>
    public List<string>? Files { get; set; }

    /// <summary>当前活动文件的路径。</summary>
    public string? Active { get; set; }

    /// <summary>左侧文件树根目录。</summary>
    public string? Folder { get; set; }
}

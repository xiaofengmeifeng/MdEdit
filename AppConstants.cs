namespace MdEditor;

/// <summary>
/// 应用级常量。
/// </summary>
internal static class AppConstants
{
    /// <summary>应用显示名称。</summary>
    public const string AppName = "MdEdit";

    /// <summary>编辑器内部统一使用的换行符（WinForms 文本框需要 CRLF 才会断行）。</summary>
    public const string NewLine = "\r\n";

    /// <summary>预览中用于映射本地文件目录的虚拟主机名。</summary>
    public const string PreviewHostName = "mdlocal";

    /// <summary>预览中用于加载内置脚本/样式（如 Mermaid）的虚拟主机名。</summary>
    public const string AssetHostName = "mdedit";

    /// <summary>单实例通信用命名管道的名称。</summary>
    public const string IpcPipeName = "MdEdit_IPC_Pipe";

    /// <summary>单实例互斥体名称。</summary>
    public const string SingleInstanceMutexName = "MdEdit_SingleInstance_Mutex_v1";

    /// <summary>工具栏图标的像素尺寸。</summary>
    public const int IconSize = 18;

    /// <summary>目录收起后左侧竖栏的宽度。</summary>
    public const int TocRailWidth = 18;
}

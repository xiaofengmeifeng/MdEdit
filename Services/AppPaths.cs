namespace MdEditor.Services;

/// <summary>
/// 应用相关的文件系统路径。
/// </summary>
internal static class AppPaths
{
    private static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppConstants.AppName);

    /// <summary>会话记录文件（记录上次打开的文件/文件夹）。</summary>
    public static string SessionFile => Path.Combine(DataDirectory, "session.json");

    /// <summary>WebView2 的用户数据目录。</summary>
    public static string WebViewDataFolder => Path.Combine(DataDirectory, "WebView2");

    /// <summary>图标字体释放到临时目录后的路径。</summary>
    public static string IconFontTempFile => Path.Combine(Path.GetTempPath(), "mdedit-fa-solid-900.ttf");

    /// <summary>确保数据目录存在。</summary>
    public static void EnsureDataDirectory() => Directory.CreateDirectory(DataDirectory);
}

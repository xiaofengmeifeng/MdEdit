namespace MdEditor;

/// <summary>
/// 插入代码块时可选择的语言（同时作为 highlight.js 的语言标识）。
/// 空字符串表示纯文本。
/// </summary>
internal static class CodeLanguages
{
    public static readonly string[] All =
    {
        "",
        "csharp", "javascript", "typescript", "python", "java", "kotlin", "go", "rust",
        "c", "cpp", "sql", "json", "yaml", "xml", "html", "css", "bash", "powershell",
        "dockerfile", "php", "ruby", "swift", "r", "markdown", "diff", "ini", "nginx", "latex",
    };
}

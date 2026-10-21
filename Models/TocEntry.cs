namespace MdEditor.Models;

/// <summary>
/// Markdown 目录（TOC）中的一条标题。
/// </summary>
/// <param name="Level">标题层级（1-6）。</param>
/// <param name="Id">渲染后 HTML 中标题的 id（用于跳转）。</param>
/// <param name="Text">标题纯文本。</param>
public readonly record struct TocEntry(int Level, string Id, string Text);

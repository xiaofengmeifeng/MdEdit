using System.Text.RegularExpressions;
using Markdig;
using MdEditor.Models;

namespace MdEditor.Services;

/// <summary>
/// Markdown 解析与目录提取。
/// </summary>
internal sealed class MarkdownService
{
    private static readonly Regex HeadingRegex = new(
        "<h(?<level>[1-6])\\b[^>]*\\bid=\"(?<id>[^\"]+)\"[^>]*>(?<text>.*?)</h\\k<level>>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled);

    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseEmojiAndSmiley()
        .Build();

    /// <summary>把 Markdown 文本渲染为 HTML 片段。</summary>
    public string ToHtml(string markdown)
    {
        try
        {
            return Markdown.ToHtml(markdown ?? string.Empty, _pipeline);
        }
        catch (Exception ex)
        {
            return "<pre>" + System.Net.WebUtility.HtmlEncode(ex.Message) + "</pre>";
        }
    }

    /// <summary>从渲染后的 HTML 中提取标题，用于生成目录。</summary>
    public static IReadOnlyList<TocEntry> ExtractHeadings(string html)
    {
        var entries = new List<TocEntry>();
        foreach (Match match in HeadingRegex.Matches(html))
        {
            var text = System.Net.WebUtility.HtmlDecode(
                TagRegex.Replace(match.Groups["text"].Value, string.Empty)).Trim();
            if (text.Length == 0)
            {
                continue;
            }

            entries.Add(new TocEntry(
                int.Parse(match.Groups["level"].Value),
                match.Groups["id"].Value,
                text));
        }

        return entries;
    }
}

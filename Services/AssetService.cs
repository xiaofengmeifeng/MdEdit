using System.Drawing.Text;
using System.Reflection;

namespace MdEditor.Services;

/// <summary>
/// 内嵌资源（图标、脚本、样式、字体）的访问与图标渲染。
/// </summary>
internal static class AssetService
{
    private const string IconResource = "MdEditor.Assets.app.ico";
    private const string IconFontResource = "MdEditor.Assets.fa-solid-900.ttf";
    private const string HighlightScriptResource = "MdEditor.Assets.hljs-all.min.js";
    private const string HighlightStyleResource = "MdEditor.Assets.hljs-github.min.css";
    private const string MermaidScriptResource = "MdEditor.Assets.mermaid.min.js";

    private static string? _highlightScript;
    private static string? _highlightStyle;
    private static FontFamily? _iconFontFamily;
    private static bool _iconFontTried;

    /// <summary>代码高亮脚本（highlight.js）。</summary>
    public static string HighlightScript =>
        _highlightScript ??= GetResourceText(HighlightScriptResource);

    /// <summary>代码高亮样式（GitHub 主题）。</summary>
    public static string HighlightStyle =>
        _highlightStyle ??= GetResourceText(HighlightStyleResource);

    /// <summary>把体积较大的预览脚本（Mermaid）释放到本地目录，供虚拟主机加载。</summary>
    public static string PreparePreviewAssets()
    {
        var dir = AppPaths.PreviewAssetsFolder;
        Directory.CreateDirectory(dir);
        WriteIfChanged(Path.Combine(dir, "mermaid.min.js"), GetResourceBytes(MermaidScriptResource));
        return dir;
    }

    /// <summary>加载应用程序图标（用于窗口标题栏）。</summary>
    public static Icon? LoadApplicationIcon()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(IconResource);
            return stream != null ? new Icon(stream) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>把图标字体中的一个字形渲染为位图。</summary>
    public static Image? RenderGlyph(string glyph)
    {
        var family = IconFontFamily();
        if (family == null)
        {
            return null;
        }

        var size = AppConstants.IconSize;
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        using var font = new Font(family, size - 3f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.FromArgb(54, 58, 66));
        var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString(glyph, font, brush, new RectangleF(0, 0, size, size), format);
        return bitmap;
    }

    /// <summary>把一段短文本渲染为图标样式的位图（如 Aa / W / .*）。</summary>
    public static Image RenderTextIcon(string text)
    {
        var height = AppConstants.IconSize;
        var width = height + 4;
        var bitmap = new Bitmap(width, height);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        using var font = new Font("Consolas", height - 6f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.FromArgb(54, 58, 66));
        var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString(text, font, brush, new RectangleF(0, 0, width, height), format);
        return bitmap;
    }

    private static FontFamily? IconFontFamily()
    {
        if (_iconFontTried)
        {
            return _iconFontFamily;
        }

        _iconFontTried = true;
        try
        {
            var data = GetResourceBytes(IconFontResource);
            if (data is { Length: > 0 })
            {
                File.WriteAllBytes(AppPaths.IconFontTempFile, data);
                var collection = new PrivateFontCollection();
                collection.AddFontFile(AppPaths.IconFontTempFile);
                if (collection.Families.Length > 0)
                {
                    _iconFontFamily = collection.Families[0];
                }
            }
        }
        catch
        {
            // 忽略字体加载失败，图标退化为空白
        }

        return _iconFontFamily;
    }

    private static void WriteIfChanged(string path, byte[]? data)
    {
        if (data is not { Length: > 0 })
        {
            return;
        }

        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length != data.Length)
            {
                File.WriteAllBytes(path, data);
            }
        }
        catch
        {
            // 忽略写入失败
        }
    }

    private static byte[]? GetResourceBytes(string name)
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (stream == null)
            {
                return null;
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static string GetResourceText(string name)
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (stream == null)
            {
                return string.Empty;
            }

            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch
        {
            return string.Empty;
        }
    }
}

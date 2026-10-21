using System.Diagnostics;
using System.Text.Json;
using MdEditor.Models;
using MdEditor.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MdEditor.Forms;

public partial class MainForm
{
    // ---- 视图模式 ----

    private void SetViewMode(ViewMode mode)
    {
        if (_viewMode == ViewMode.Split)
        {
            _lastSplitterDistance = _split.SplitterDistance;
        }

        _viewMode = mode;
        switch (mode)
        {
            case ViewMode.Edit:
                _split.Panel2Collapsed = true;
                _split.Panel1Collapsed = false;
                break;
            case ViewMode.Preview:
                _split.Panel1Collapsed = true;
                _split.Panel2Collapsed = false;
                break;
            default:
                _split.Panel1Collapsed = false;
                _split.Panel2Collapsed = false;
                RestoreSplitter();
                break;
        }

        UpdateViewChecks();

        if (mode != ViewMode.Edit)
        {
            UpdatePreview();
        }
    }

    private void RestoreSplitter()
    {
        try
        {
            var max = _split.Width - _split.Panel2MinSize - _split.SplitterWidth;
            var min = _split.Panel1MinSize;
            var distance = Math.Max(min, Math.Min(_lastSplitterDistance, max));
            if (distance > 0 && distance < _split.Width)
            {
                _split.SplitterDistance = distance;
            }
        }
        catch
        {
            // 忽略布局异常
        }
    }

    private void UpdateViewChecks()
    {
        _viewEditItem.Checked = _viewMode == ViewMode.Edit;
        _viewSplitItem.Checked = _viewMode == ViewMode.Split;
        _viewPreviewItem.Checked = _viewMode == ViewMode.Preview;
        _btnEdit.Checked = _viewEditItem.Checked;
        _btnSplit.Checked = _viewSplitItem.Checked;
        _btnPreview.Checked = _viewPreviewItem.Checked;
    }

    // ---- 目录显示/隐藏 ----

    private void SetTocVisible(bool visible)
    {
        if (_tocContent == null || _previewSplit == null)
        {
            return;
        }

        try
        {
            if (visible)
            {
                _tocContent.Visible = true;
                var distance = _previewSplit.Width - _tocExpandedWidth - _previewSplit.SplitterWidth;
                distance = Math.Max(_previewSplit.Panel1MinSize, distance);
                if (distance < _previewSplit.Width - _previewSplit.Panel2MinSize - _previewSplit.SplitterWidth)
                {
                    _previewSplit.SplitterDistance = distance;
                }
            }
            else
            {
                _tocExpandedWidth = Math.Max(120,
                    _previewSplit.Width - _previewSplit.SplitterDistance - _previewSplit.SplitterWidth);
                _tocContent.Visible = false;
                var distance = Math.Max(_previewSplit.Panel1MinSize,
                    _previewSplit.Width - AppConstants.TocRailWidth - _previewSplit.SplitterWidth);
                _previewSplit.SplitterDistance = distance;
            }
        }
        catch
        {
            // 忽略布局异常
        }

        UpdateTocToggleIcon(visible);

        if (_toggleTocItem != null && _toggleTocItem.Checked != visible)
        {
            _toggleTocItem.Checked = visible;
        }
    }

    private void UpdateTocToggleIcon(bool visible)
    {
        if (_tocToggleBtn != null)
        {
            _tocToggleBtn.Image = AssetService.RenderGlyph(visible ? "\uf054" : "\uf053");
        }
    }

    // ---- WebView2 初始化与导航 ----

    private async Task InitWebViewAsync()
    {
        try
        {
            var userDataFolder = AppPaths.WebViewDataFolder;
            Directory.CreateDirectory(userDataFolder);

            var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await _preview.EnsureCoreWebView2Async(environment);

            var core = _preview.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.IsZoomControlEnabled = true;

            core.NavigationStarting += OnNavigationStarting;
            core.WebMessageReceived += OnWebMessageReceived;
            core.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                OpenExternal(args.Uri);
            };
            core.NavigationCompleted += (_, _) =>
            {
                _webReady = true;
                ApplyBaseHref();
                UpdatePreview();
            };

            core.NavigateToString(PreviewDocument.BuildShellHtml());
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "初始化预览组件失败：\n" + ex.Message,
                AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        var uri = e.Uri ?? string.Empty;
        if (uri.Length == 0 || uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var hostPrefix = "https://" + AppConstants.PreviewHostName + "/";
        if (uri.StartsWith(hostPrefix, StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            HandleLocalLink(uri.Substring(hostPrefix.Length));
            return;
        }

        if (uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            OpenExternal(uri);
        }
    }

    /// <summary>处理预览中对本地文件的链接：改为在编辑器中打开；纯锚点则滚动。</summary>
    private void HandleLocalLink(string relative)
    {
        var hashIndex = relative.IndexOf('#');
        var fragment = hashIndex >= 0 ? relative.Substring(hashIndex + 1) : null;
        var pathPart = hashIndex >= 0 ? relative.Substring(0, hashIndex) : relative;

        var dir = _currentFile != null ? Path.GetDirectoryName(_currentFile) : null;
        if (!string.IsNullOrEmpty(dir) && pathPart.Length > 0)
        {
            try
            {
                var localPath = Path.GetFullPath(Path.Combine(
                    dir, Uri.UnescapeDataString(pathPart).Replace('/', Path.DirectorySeparatorChar)));

                if (File.Exists(localPath) && IsSupportedFile(localPath))
                {
                    var target = localPath;
                    BeginInvoke(new Action(() => OpenFilePath(target)));
                    return;
                }

                if (File.Exists(localPath))
                {
                    var target = localPath;
                    BeginInvoke(new Action(() => OpenExternal(target)));
                    return;
                }
            }
            catch
            {
                // 落到锚点处理
            }
        }

        if (!string.IsNullOrEmpty(fragment))
        {
            ScrollPreviewTo(Uri.UnescapeDataString(fragment));
        }
    }

    private void ScrollPreviewTo(string elementId)
    {
        if (!_webReady)
        {
            return;
        }

        _ = _preview.CoreWebView2.ExecuteScriptAsync(
            "try{var el=document.getElementById(" + JsonSerializer.Serialize(elementId) +
            ");if(el)el.scrollIntoView({block:'start',behavior:'smooth'});}catch(e){}");
    }

    private static void OpenExternal(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch
        {
            // 忽略
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var raw = e.TryGetWebMessageAsString();
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            using var json = JsonDocument.Parse(raw);
            if (json.RootElement.TryGetProperty("type", out var type) &&
                type.GetString() == "copy" &&
                json.RootElement.TryGetProperty("text", out var text))
            {
                var value = text.GetString();
                if (!string.IsNullOrEmpty(value))
                {
                    Clipboard.SetText(value);
                }
            }
        }
        catch
        {
            // 忽略
        }
    }

    // ---- 预览渲染 ----

    private void ApplyBaseHref()
    {
        if (!_webReady)
        {
            return;
        }

        var href = "about:blank";
        if (_currentFile != null)
        {
            var dir = Path.GetDirectoryName(_currentFile);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                try
                {
                    _preview.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        AppConstants.PreviewHostName, dir, CoreWebView2HostResourceAccessKind.Allow);
                    href = "https://" + AppConstants.PreviewHostName + "/";
                }
                catch
                {
                    href = "about:blank";
                }
            }
        }

        _ = _preview.CoreWebView2.ExecuteScriptAsync(
            "try{document.getElementById('baseEl').href=" + JsonSerializer.Serialize(href) + ";}catch(e){}");
    }

    private void UpdatePreview()
    {
        if (!_webReady)
        {
            return;
        }

        _previewHitCount = 0;
        if (_previewCount != null)
        {
            _previewCount.Text = string.Empty;
        }

        var html = _markdown.ToHtml(_editor.Text ?? string.Empty);
        BuildToc(html);

        var script =
            "try{var y=window.scrollY;" +
            "document.getElementById('content').innerHTML=" + JsonSerializer.Serialize(html) + ";" +
            "if(window.hljs){document.querySelectorAll('#content pre code').forEach(function(el){try{hljs.highlightElement(el);}catch(e){}});}" +
            "if(window.addCopyButtons){window.addCopyButtons();}" +
            "window.scrollTo(0,y);}catch(e){}";
        _ = _preview.CoreWebView2.ExecuteScriptAsync(script);
    }

    private void BuildToc(string html)
    {
        _tocTree.BeginUpdate();
        _tocTree.Nodes.Clear();

        var stack = new Stack<(int Level, TreeNode Node)>();
        foreach (var entry in MarkdownService.ExtractHeadings(html))
        {
            var node = new TreeNode(entry.Text) { Tag = entry.Id };
            while (stack.Count > 0 && stack.Peek().Level >= entry.Level)
            {
                stack.Pop();
            }

            if (stack.Count == 0)
            {
                _tocTree.Nodes.Add(node);
            }
            else
            {
                stack.Peek().Node.Nodes.Add(node);
            }

            stack.Push((entry.Level, node));
        }

        _tocTree.EndUpdate();
        _tocTree.ExpandAll();
    }

    private void OnTocNodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Button == MouseButtons.Left && e.Node.Tag is string id)
        {
            ScrollPreviewTo(id);
        }
    }

    // ---- 预览内查找 ----

    private void OnPreviewSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            RunPreviewSearch();
            e.SuppressKeyPress = true;
        }
    }

    private async void RunPreviewSearch()
    {
        if (!_webReady)
        {
            return;
        }

        try
        {
            var script = "window.searchInPreview(" +
                         JsonSerializer.Serialize(_previewSearchBox.Text) + "," +
                         Bool(_previewCase) + "," + Bool(_previewWord) + "," + Bool(_previewRegex) + ")";
            var result = await _preview.CoreWebView2.ExecuteScriptAsync(script);
            _previewHitCount = ParseInt(result);
            UpdateSearchLabel(_previewHitCount > 0 ? 0 : -1);
        }
        catch
        {
            // 忽略
        }
    }

    private async void StepPreviewSearch(int delta)
    {
        if (!_webReady)
        {
            return;
        }

        if (_previewHitCount == 0)
        {
            RunPreviewSearch();
            return;
        }

        try
        {
            var result = await _preview.CoreWebView2.ExecuteScriptAsync("window.stepHit(" + delta + ")");
            UpdateSearchLabel(ParseInt(result));
        }
        catch
        {
            // 忽略
        }
    }

    private void UpdateSearchLabel(int index)
    {
        if (_previewSearchBox.Text.Length == 0)
        {
            _previewCount.Text = string.Empty;
        }
        else if (_previewHitCount <= 0)
        {
            _previewCount.Text = "0/0";
        }
        else
        {
            _previewCount.Text = (index + 1) + "/" + _previewHitCount;
        }
    }

    private static string Bool(bool value) => value ? "true" : "false";

    private static int ParseInt(string json)
        => !string.IsNullOrEmpty(json) && int.TryParse(json.Trim(), out var value) ? value : -1;
}

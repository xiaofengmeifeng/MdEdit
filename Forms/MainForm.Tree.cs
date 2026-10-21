using MdEditor.Models;

namespace MdEditor.Forms;

public partial class MainForm
{
    private static bool IsFileNode(TreeNode node) => node.Tag is string path && File.Exists(path);

    private static bool IsFolderNode(TreeNode node) => node.Tag is string path && Directory.Exists(path);

    private static bool IsSupportedFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".md" or ".markdown" or ".txt";
    }

    // ---- 文件夹树 ----

    private void OpenFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择要显示文件树的文件夹",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _suppressFileFilter = true;
            _fileSearchBox.Text = string.Empty;
            _suppressFileFilter = false;
            LoadFolder(dialog.SelectedPath);
        }
    }

    private void LoadFolder(string path)
    {
        try
        {
            path = Path.GetFullPath(path);
        }
        catch
        {
            return;
        }

        _folderPath = path;
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var root = new TreeNode(string.IsNullOrEmpty(name) ? path : name) { Tag = path };
        AddPlaceholder(root);
        _tree.Nodes.Add(root);
        _tree.EndUpdate();
        root.Expand();
        SetSelectedNode(root);
    }

    private static void AddPlaceholder(TreeNode node)
    {
        node.Nodes.Add(new TreeNode("...") { Tag = null });
    }

    private void PopulateNode(TreeNode node)
    {
        if (node.Tag is not string dir || !Directory.Exists(dir))
        {
            return;
        }

        node.Nodes.Clear();
        try
        {
            foreach (var sub in Directory.GetDirectories(dir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var child = new TreeNode(Path.GetFileName(sub)) { Tag = sub };
                AddPlaceholder(child);
                node.Nodes.Add(child);
            }

            foreach (var file in Directory.GetFiles(dir)
                         .Where(IsSupportedFile)
                         .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                node.Nodes.Add(new TreeNode(Path.GetFileName(file)) { Tag = file });
            }
        }
        catch
        {
            // 忽略无权限访问的目录
        }
    }

    private void OnTreeBeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        if (e.Node is { } node && node.Nodes.Count == 1 && node.Nodes[0]?.Tag == null)
        {
            PopulateNode(node);
        }
    }

    private void OnTreeNodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            _suppressTreeSelect = true;
            _tree.SelectedNode = e.Node;
            _suppressTreeSelect = false;
            return;
        }

        if (e.Button == MouseButtons.Left && IsFolderNode(e.Node))
        {
            e.Node.Toggle();
        }
    }

    private void OnTreeAfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (_suppressTreeSelect)
        {
            return;
        }

        if (e.Node != null && IsFileNode(e.Node))
        {
            LoadFile((string)e.Node.Tag!);
        }
    }

    private void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && _tree.SelectedNode != null && IsFileNode(_tree.SelectedNode))
        {
            LoadFile((string)_tree.SelectedNode.Tag!);
            e.Handled = true;
        }
    }

    private void SetSelectedNode(TreeNode node)
    {
        _suppressTreeSelect = true;
        _tree.SelectedNode = node;
        _suppressTreeSelect = false;
    }

    // ---- 文件树右键菜单 ----

    private void TreeOpen()
    {
        if (_contextNode != null && IsFileNode(_contextNode))
        {
            LoadFile((string)_contextNode.Tag!);
        }
    }

    private void TreeReveal()
    {
        if (_contextNode?.Tag is string path)
        {
            RevealInExplorer(path);
        }
    }

    private void TreeCopyPath()
    {
        if (_contextNode?.Tag is string path)
        {
            try
            {
                Clipboard.SetText(path);
            }
            catch
            {
                // 忽略剪贴板占用
            }
        }
    }

    private void TreeRename()
    {
        if (_contextNode?.Tag is not string path)
        {
            return;
        }

        var oldName = Path.GetFileName(path);
        var newName = PromptDialog.Show(this, "重命名", "请输入新名称：", oldName);
        if (string.IsNullOrWhiteSpace(newName) || newName == oldName)
        {
            return;
        }

        var dir = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }

        var target = Path.Combine(dir, newName.Trim());
        try
        {
            if (IsFolderNode(_contextNode))
            {
                Directory.Move(path, target);
            }
            else
            {
                File.Move(path, target);
            }

            var renamed = FindDocument(path);
            if (renamed != null)
            {
                renamed.Path = target;
                if (ReferenceEquals(renamed, _currentDoc))
                {
                    _currentFile = target;
                    UpdateTitle();
                    UpdateStatus();
                    ApplyBaseHref();
                }

                RefreshTabs();
            }

            RefreshTree();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "重命名失败：\n" + ex.Message,
                AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void TreeDelete()
    {
        if (_contextNode?.Tag is not string path)
        {
            return;
        }

        var name = Path.GetFileName(path);
        var isDirectory = IsFolderNode(_contextNode);
        var message = isDirectory
            ? $"确定删除文件夹「{name}」及其全部内容？"
            : $"确定删除文件「{name}」？";

        if (MessageBox.Show(this, message, "删除确认",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            if (isDirectory)
            {
                Directory.Delete(path, true);
            }
            else
            {
                File.Delete(path);
            }

            foreach (var doc in _docs.ToList())
            {
                if (doc.Path != null &&
                    (string.Equals(doc.Path, path, StringComparison.OrdinalIgnoreCase) ||
                     doc.Path.StartsWith(path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                {
                    var wasCurrent = ReferenceEquals(doc, _currentDoc);
                    _docs.Remove(doc);
                    if (wasCurrent)
                    {
                        _currentDoc = null;
                        _currentFile = null;
                    }
                }
            }

            if (_currentDoc == null)
            {
                if (_docs.Count > 0)
                {
                    SwitchToDocument(_docs[0]);
                }
                else
                {
                    NewDocument();
                }
            }
            else
            {
                RefreshTabs();
            }

            RefreshTree();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "删除失败：\n" + ex.Message,
                AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void TreeRefresh()
    {
        RefreshTree();
        ReloadChangedFiles();
    }

    private void RefreshTree()
    {
        if (_folderPath != null && Directory.Exists(_folderPath))
        {
            LoadFolder(_folderPath);
        }
    }

    private void UpdateTreeSelectionFor(string? path)
    {
        if (path == null || _folderPath == null || !IsUnder(path, _folderPath))
        {
            return;
        }

        var node = FindNodeByPath(_tree.Nodes, path);
        if (node != null)
        {
            SetSelectedNode(node);
        }
    }

    private static bool IsUnder(string path, string folder)
    {
        var prefix = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                     Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static TreeNode? FindNodeByPath(TreeNodeCollection nodes, string path)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag is string p && string.Equals(p, path, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            var found = FindNodeByPath(node.Nodes, path);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    // ---- 文件名过滤（搜索框）----

    private void OnFileSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            ApplyFileFilter();
            e.SuppressKeyPress = true;
        }
    }

    private void ApplyFileFilter()
    {
        if (_suppressFileFilter)
        {
            return;
        }

        var term = _fileSearchBox.Text.Trim();
        if (term.Length == 0)
        {
            if (_folderPath != null && Directory.Exists(_folderPath))
            {
                LoadFolder(_folderPath);
            }

            return;
        }

        if (_folderPath == null)
        {
            return;
        }

        var matches = new List<string>();
        SearchFiles(_folderPath, term, matches, 300);

        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        foreach (var file in matches)
        {
            var relative = file.Length > _folderPath.Length
                ? file.Substring(_folderPath.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : Path.GetFileName(file);
            _tree.Nodes.Add(new TreeNode(relative) { Tag = file });
        }

        if (matches.Count == 0)
        {
            _tree.Nodes.Add(new TreeNode("（无匹配）"));
        }

        _tree.EndUpdate();
    }

    private static void SearchFiles(string dir, string term, List<string> results, int limit)
    {
        if (results.Count >= limit)
        {
            return;
        }

        try
        {
            foreach (var file in Directory.GetFiles(dir))
            {
                if (IsSupportedFile(file) &&
                    Path.GetFileName(file).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    results.Add(file);
                    if (results.Count >= limit)
                    {
                        return;
                    }
                }
            }

            foreach (var sub in Directory.GetDirectories(dir))
            {
                SearchFiles(sub, term, results, limit);
                if (results.Count >= limit)
                {
                    return;
                }
            }
        }
        catch
        {
            // 忽略无权限访问的目录
        }
    }
}

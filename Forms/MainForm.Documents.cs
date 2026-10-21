using System.Diagnostics;
using MdEditor.Controls;
using MdEditor.Models;
using MdEditor.Services;

namespace MdEditor.Forms;

public partial class MainForm
{
    // ---- 文档标签栏 ----

    private static void RevealInExplorer(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"")
            {
                UseShellExecute = true
            });
        }
        catch
        {
            // 忽略
        }
    }

    private void RefreshTabs()
    {
        _tabStrip.SuspendLayout();
        foreach (Control control in _tabStrip.Controls)
        {
            control.Dispose();
        }

        _tabStrip.Controls.Clear();

        DocumentTab? activeTab = null;
        foreach (var doc in _docs)
        {
            var captured = doc;
            var tab = new DocumentTab(doc.DisplayName)
            {
                IsActive = ReferenceEquals(doc, _currentDoc),
                IsDirty = doc.IsDirty,
                Tag = doc
            };
            tab.Activated += (_, _) => SwitchToDocument(captured);
            tab.CloseRequested += (_, _) => CloseDocument(captured);
            tab.ContextMenuRequested += (sender, _) =>
            {
                _contextDocument = (OpenDocument?)((DocumentTab)sender!).Tag;
                _tabMenu.Show(Cursor.Position);
            };
            _tabStrip.Controls.Add(tab);
            if (tab.IsActive)
            {
                activeTab = tab;
            }
        }

        _tabStrip.ResumeLayout();
        EnsureTabVisible(activeTab);
    }

    private void UpdateActiveTabDirty()
    {
        if (_currentDoc == null)
        {
            return;
        }

        foreach (Control control in _tabStrip.Controls)
        {
            if (control is DocumentTab { Tag: OpenDocument doc } tab && ReferenceEquals(doc, _currentDoc))
            {
                tab.IsDirty = doc.IsDirty;
                EnsureTabVisible(tab);
                return;
            }
        }
    }

    private void EnsureTabVisible(DocumentTab? tab)
    {
        if (tab == null)
        {
            return;
        }

        try
        {
            _tabStrip.ScrollControlIntoView(tab);
        }
        catch
        {
            // 忽略
        }
    }

    // ---- 当前文档 ----

    private void CommitCurrent()
    {
        if (_currentDoc == null)
        {
            return;
        }

        _currentDoc.Text = _editor.Text;
        _currentDoc.IsDirty = _dirty;
        _currentDoc.LineEnding = _lineEnding;
    }

    private void SwitchToDocument(OpenDocument doc)
    {
        if (ReferenceEquals(doc, _currentDoc))
        {
            return;
        }

        CommitCurrent();

        _currentDoc = doc;
        SetEditorText(doc.Text);
        _currentFile = doc.Path;
        _lineEnding = doc.LineEnding;
        _dirty = doc.IsDirty;

        _previewSearchBox.Text = string.Empty;
        RefreshTabs();
        UpdateTitle();
        UpdateStatus();
        ApplyBaseHref();
        UpdatePreview();
        UpdateTreeSelectionFor(doc.Path);
    }

    private void OpenFilePath(string path)
    {
        try
        {
            path = Path.GetFullPath(path);
        }
        catch
        {
            return;
        }

        var existing = FindDocument(path);
        if (existing != null)
        {
            SwitchToDocument(existing);
            return;
        }

        var doc = AddDocumentFromFile(path);
        if (doc != null)
        {
            SwitchToDocument(doc);
        }
    }

    private OpenDocument? AddDocumentFromFile(string path)
    {
        try
        {
            path = Path.GetFullPath(path);
        }
        catch
        {
            return null;
        }

        var existing = FindDocument(path);
        if (existing != null)
        {
            return existing;
        }

        if (!File.Exists(path))
        {
            return null;
        }

        string raw;
        try
        {
            raw = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "无法打开文件：\n" + ex.Message,
                AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        var doc = new OpenDocument
        {
            Path = path,
            Text = NormalizeToCrlf(raw),
            IsDirty = false,
            LineEnding = raw.Contains("\r\n") ? "\r\n" : "\n",
            LastWriteTimeUtc = SafeGetWriteTimeUtc(path)
        };
        _docs.Add(doc);
        return doc;
    }

    private static DateTime SafeGetWriteTimeUtc(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private OpenDocument? FindDocument(string path) => _docs.FirstOrDefault(d =>
        d.Path != null && string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase));

    // ---- 新建 / 打开 / 保存 ----

    private void NewDocument()
    {
        CommitCurrent();
        var doc = new OpenDocument { Path = null, Text = string.Empty, IsDirty = false };
        _docs.Add(doc);
        SwitchToDocument(doc);
    }

    private void OpenDocument()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "打开 Markdown 文件",
            Filter = "Markdown (*.md;*.markdown)|*.md;*.markdown|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            OpenFilePath(dialog.FileName);
        }
    }

    private void LoadFile(string path) => OpenFilePath(path);

    private void SaveDocument()
    {
        CommitCurrent();
        if (_currentDoc == null)
        {
            SaveUntitled();
            return;
        }

        if (_currentDoc.Path == null)
        {
            SaveDocumentAs(_currentDoc);
            return;
        }

        SaveDocument(_currentDoc, _currentDoc.Path);
    }

    private void SaveDocumentAs()
    {
        CommitCurrent();
        if (_currentDoc != null)
        {
            SaveDocumentAs(_currentDoc);
        }
    }

    private void SaveUntitled()
    {
        var doc = new OpenDocument { Path = null, Text = _editor.Text, IsDirty = true, LineEnding = _lineEnding };
        if (SaveDocumentAs(doc))
        {
            _docs.Add(doc);
            SwitchToDocument(doc);
        }
    }

    private bool ConfirmSaveDocument(OpenDocument doc)
    {
        if (!doc.IsDirty)
        {
            return true;
        }

        var result = MessageBox.Show(this,
            $"「{doc.DisplayName}」已修改，是否保存？",
            AppConstants.AppName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            return doc.Path == null ? SaveDocumentAs(doc) : SaveDocument(doc, doc.Path);
        }

        return result == DialogResult.No;
    }

    private bool SaveDocument(OpenDocument doc, string path)
    {
        try
        {
            path = Path.GetFullPath(path);
            var text = doc.Text;
            if (doc.LineEnding == "\n")
            {
                text = text.Replace("\r\n", "\n");
            }

            File.WriteAllText(path, text, new System.Text.UTF8Encoding(false));
            doc.Path = path;
            doc.IsDirty = false;
            doc.LastWriteTimeUtc = SafeGetWriteTimeUtc(path);

            if (ReferenceEquals(doc, _currentDoc))
            {
                _currentFile = path;
                _dirty = false;
                UpdateTitle();
                UpdateStatus();
                ApplyBaseHref();
                UpdateTreeSelectionFor(path);
            }

            RefreshTabs();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "保存失败：\n" + ex.Message,
                AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    private bool SaveDocumentAs(OpenDocument doc)
    {
        using var dialog = new SaveFileDialog
        {
            Title = "另存为",
            Filter = "Markdown (*.md)|*.md|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            DefaultExt = "md",
            AddExtension = true,
            FileName = doc.Path == null ? "未命名.md" : Path.GetFileName(doc.Path)
        };
        return dialog.ShowDialog(this) == DialogResult.OK && SaveDocument(doc, dialog.FileName);
    }

    // ---- 从磁盘重新加载 ----

    /// <summary>重新加载当前文档（有未保存修改时先确认）。</summary>
    private void ReloadCurrentDocument()
    {
        if (_currentDoc?.Path == null)
        {
            return;
        }

        if (_currentDoc.IsDirty)
        {
            var result = MessageBox.Show(this,
                "重新加载将丢弃当前未保存的修改，是否继续？",
                AppConstants.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes)
            {
                return;
            }
        }

        ReloadDocument(_currentDoc);
    }

    /// <summary>从磁盘重新读取指定文档，并刷新编辑器、标签与预览。</summary>
    private bool ReloadDocument(OpenDocument doc)
    {
        if (doc.Path == null || !File.Exists(doc.Path))
        {
            return false;
        }

        try
        {
            var raw = File.ReadAllText(doc.Path);
            var normalized = NormalizeToCrlf(raw);
            doc.Text = normalized;
            doc.LineEnding = raw.Contains("\r\n") ? "\r\n" : "\n";
            doc.LastWriteTimeUtc = SafeGetWriteTimeUtc(doc.Path);
            doc.IsDirty = false;

            if (ReferenceEquals(doc, _currentDoc))
            {
                SetEditorText(normalized);
                _lineEnding = doc.LineEnding;
                _dirty = false;
                ApplyBaseHref();
                UpdatePreview();
                UpdateTitle();
                UpdateStatus();
            }

            RefreshTabs();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>检测被外部修改（且本地无未保存修改）的已打开文件并自动重新加载。</summary>
    private void ReloadChangedFiles()
    {
        if (_docs.Count == 0)
        {
            return;
        }

        foreach (var doc in _docs.ToList())
        {
            if (doc.Path == null || doc.IsDirty || !File.Exists(doc.Path))
            {
                continue;
            }

            if (SafeGetWriteTimeUtc(doc.Path) > doc.LastWriteTimeUtc)
            {
                ReloadDocument(doc);
            }
        }
    }

    // ---- 关闭文档 ----

    private bool CloseDocument(OpenDocument? doc)
    {
        if (doc == null)
        {
            return true;
        }

        var wasCurrent = ReferenceEquals(doc, _currentDoc);
        if (wasCurrent)
        {
            CommitCurrent();
        }

        if (doc.IsDirty && !ConfirmSaveDocument(doc))
        {
            return false;
        }

        var index = _docs.IndexOf(doc);
        _docs.Remove(doc);

        if (wasCurrent)
        {
            _currentDoc = null;
            if (_docs.Count > 0)
            {
                SwitchToDocument(_docs[Math.Min(index, _docs.Count - 1)]);
            }
            else
            {
                NewDocument();
                return true;
            }
        }

        RefreshTabs();
        return true;
    }

    private void CloseOtherDocuments(OpenDocument? keep)
    {
        if (keep == null)
        {
            return;
        }

        foreach (var doc in _docs.ToList())
        {
            if (!ReferenceEquals(doc, keep) && !CloseDocument(doc))
            {
                return;
            }
        }
    }

    private void CloseAllDocuments()
    {
        foreach (var doc in _docs.ToList())
        {
            if (!CloseDocument(doc))
            {
                return;
            }
        }
    }

    // ---- 外部打开（单实例转发）----

    /// <summary>由 IPC / 命令行触发的打开请求（可能在非 UI 线程调用）。</summary>
    public void OpenExternalPath(string path)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => OpenExternalPath(path)));
            return;
        }

        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        Activate();

        if (Directory.Exists(path))
        {
            LoadFolder(path);
        }
        else if (File.Exists(path))
        {
            OpenFilePath(path);
        }
    }

    // ---- 会话持久化 ----

    private void SaveSession()
    {
        CommitCurrent();
        SessionService.Save(new SessionData
        {
            Files = _docs.Where(d => d.Path != null).Select(d => d.Path!).ToList(),
            Active = _currentDoc?.Path,
            Folder = _folderPath
        });
    }

    private void RestoreSession()
    {
        var data = SessionService.Load();
        if (data == null)
        {
            return;
        }

        OpenDocument? active = null;
        if (data.Files != null)
        {
            foreach (var file in data.Files)
            {
                var doc = AddDocumentFromFile(file);
                if (doc != null && data.Active != null &&
                    string.Equals(doc.Path, data.Active, StringComparison.OrdinalIgnoreCase))
                {
                    active = doc;
                }
            }
        }

        if (!string.IsNullOrEmpty(data.Folder) && Directory.Exists(data.Folder))
        {
            LoadFolder(data.Folder);
        }

        if (_docs.Count > 0)
        {
            SwitchToDocument(active ?? _docs[0]);
        }
    }
}

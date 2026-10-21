using System.Diagnostics;
using MdEditor.Controls;
using MdEditor.Models;
using MdEditor.Services;
using Microsoft.Web.WebView2.WinForms;

namespace MdEditor.Forms;

/// <summary>
/// 主窗口。代码按职责拆分到多个 partial 文件：
/// MainForm.cs（核心/生命周期）、MainForm.Layout.cs（界面构建）、
/// MainForm.Documents.cs（多文档与会话）、MainForm.Tree.cs（文件树）、
/// MainForm.Editor.cs（编辑/格式化/查找替换）、MainForm.Preview.cs（预览/目录/视图）。
/// </summary>
public partial class MainForm : Form
{
    private readonly string? _startupFile;
    private readonly MarkdownService _markdown = new();
    private readonly ToolTip _toolTip = new();

    // 编辑与预览
    private TextBox _editor = null!;
    private SplitContainer _split = null!;
    private SplitContainer _mainSplit = null!;
    private SplitContainer _previewSplit = null!;

    // 文件树
    private TreeView _tree = null!;
    private ContextMenuStrip _treeMenu = null!;
    private TextBox _fileSearchBox = null!;
    private Button _btnFileSearch = null!;

    // 文档标签栏
    private Panel _tabBar = null!;
    private FlowLayoutPanel _tabStrip = null!;
    private ContextMenuStrip _tabMenu = null!;
    private OpenDocument? _contextDocument;

    // 预览区（WebView2）
    private WebView2 _preview = null!;
    private bool _webReady;

    // 预览搜索
    private TextBox _previewSearchBox = null!;
    private Button _btnPreviewSearch = null!;
    private Button _btnPrevSearch = null!;
    private Button _btnNextSearch = null!;
    private Label _previewCount = null!;
    private int _previewHitCount;
    private bool _previewCase;
    private bool _previewWord;
    private bool _previewRegex;

    // 目录（TOC）
    private TreeView _tocTree = null!;
    private Panel _tocHeader = null!;
    private Panel _tocHost = null!;
    private Panel _tocContent = null!;
    private Panel _tocRail = null!;
    private Button _tocToggleBtn = null!;
    private ToolStripMenuItem _toggleTocItem = null!;
    private int _tocExpandedWidth = 220;

    // 菜单与工具栏
    private MenuStrip _menu = null!;
    private ToolStrip _toolbar = null!;
    private ToolStrip _formatBar = null!;
    private StatusStrip _status = null!;
    private ToolStripStatusLabel _statusFile = null!;
    private ToolStripStatusLabel _statusInfo = null!;
    private System.Windows.Forms.Timer _debounce = null!;
    private ToolStripMenuItem _viewEditItem = null!;
    private ToolStripMenuItem _viewSplitItem = null!;
    private ToolStripMenuItem _viewPreviewItem = null!;
    private ToolStripMenuItem _toggleSidebarItem = null!;
    private ToolStripMenuItem _toggleWrapItem = null!;
    private ToolStripButton _btnEdit = null!;
    private ToolStripButton _btnSplit = null!;
    private ToolStripButton _btnPreview = null!;

    // 查找/替换
    private Panel _findBar = null!;
    private TextBox _findBox = null!;
    private TextBox _replaceBox = null!;
    private Button _findCaseBtn = null!;
    private Button _findWordBtn = null!;
    private Button _findRegexBtn = null!;
    private Button _replaceBtn = null!;
    private Button _replaceAllBtn = null!;
    private bool _findCase;
    private bool _findWord;
    private bool _findRegex;
    private readonly List<(int Start, int Length)> _findMatches = new();
    private int _findIndex = -1;

    // 状态
    private ViewMode _viewMode = ViewMode.Split;
    private int _lastSplitterDistance = 560;
    private string? _currentFile;
    private string? _folderPath;
    private TreeNode? _contextNode;
    private bool _dirty;
    private bool _suppressTextChanged;
    private bool _suppressTreeSelect;
    private bool _suppressFileFilter;
    private string _lineEnding = AppConstants.NewLine;

    // 多文档
    private readonly List<OpenDocument> _docs = new();
    private OpenDocument? _currentDoc;

    public MainForm(string? startupFile = null)
    {
        _startupFile = startupFile;
        BuildUi();
        WireEvents();
    }

    private void WireEvents()
    {
        _editor.TextChanged += OnEditorTextChanged;
        _editor.KeyDown += OnEditorKeyDown;
        _editor.DragEnter += OnDragEnter;
        _editor.DragDrop += OnDragDrop;

        _tree.BeforeExpand += OnTreeBeforeExpand;
        _tree.NodeMouseClick += OnTreeNodeMouseClick;
        _tree.AfterSelect += OnTreeAfterSelect;
        _tree.KeyDown += OnTreeKeyDown;
        _tree.AllowDrop = true;
        _tree.DragEnter += OnDragEnter;
        _tree.DragDrop += OnDragDrop;

        _tocTree.NodeMouseClick += OnTocNodeMouseClick;

        _fileSearchBox.TextChanged += (_, _) => ApplyFileFilter();
        _fileSearchBox.KeyDown += OnFileSearchBoxKeyDown;
        _btnFileSearch.Click += (_, _) => ApplyFileFilter();

        _previewSearchBox.KeyDown += OnPreviewSearchBoxKeyDown;
        _btnPreviewSearch.Click += (_, _) => RunPreviewSearch();
        _btnPrevSearch.Click += (_, _) => StepPreviewSearch(-1);
        _btnNextSearch.Click += (_, _) => StepPreviewSearch(1);

        _debounce.Tick += (_, _) => { _debounce.Stop(); UpdatePreview(); };

        AllowDrop = true;
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        FormClosing += OnFormClosing;
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await InitWebViewAsync();

        RestoreSession();

        if (!string.IsNullOrWhiteSpace(_startupFile) && Directory.Exists(_startupFile))
        {
            LoadFolder(_startupFile!);
        }
        else if (!string.IsNullOrWhiteSpace(_startupFile))
        {
            OpenFilePath(_startupFile!);
        }

        if (_docs.Count == 0)
        {
            NewDocument();
        }

        UpdateTitle();
        UpdateStatus();

        new IpcServer(OpenExternalPath).Start();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyInitialLayout();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        ReloadChangedFiles();
    }

    private void ApplyInitialLayout()
    {
        try
        {
            _mainSplit.SplitterDistance = 220;
            _mainSplit.Panel1MinSize = 140;
            _mainSplit.Panel2MinSize = 240;
        }
        catch
        {
            // 忽略布局异常
        }

        try
        {
            _split.SplitterDistance = (int)(_split.Width * 0.55);
            _split.Panel1MinSize = 240;
            _split.Panel2MinSize = 440;
        }
        catch
        {
            // 忽略布局异常
        }

        try
        {
            var distance = _previewSplit.Width - 220 - _previewSplit.SplitterWidth;
            if (distance > 0)
            {
                _previewSplit.SplitterDistance = distance;
                _previewSplit.Panel1MinSize = 200;
                _previewSplit.Panel2MinSize = 16;
            }
        }
        catch
        {
            // 忽略布局异常
        }

        _lastSplitterDistance = _split.SplitterDistance;
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        CommitCurrent();
        foreach (var doc in _docs.ToList())
        {
            if (doc.IsDirty && !ConfirmSaveDocument(doc))
            {
                e.Cancel = true;
                return;
            }
        }

        SaveSession();
    }

    private void SetEditorText(string text)
    {
        _suppressTextChanged = true;
        _editor.Text = text;
        _editor.SelectionStart = 0;
        _editor.SelectionLength = 0;
        _suppressTextChanged = false;
    }

    private static string NormalizeToCrlf(string text)
        => text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

    private void UpdateTitle()
    {
        var name = _currentFile == null ? "未命名" : Path.GetFileName(_currentFile);
        Text = name + (_dirty ? " *" : string.Empty) + " - " + AppConstants.AppName;
    }

    private void UpdateStatus()
    {
        _statusFile.Text = _currentFile ?? "未命名（未保存）";
        _statusInfo.Text = $"{_editor.Text.Length} 字符";
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
        }
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
        {
            return;
        }

        foreach (var file in files)
        {
            if (Directory.Exists(file))
            {
                LoadFolder(file);
            }
            else if (File.Exists(file))
            {
                OpenFilePath(file);
            }
        }
    }
}

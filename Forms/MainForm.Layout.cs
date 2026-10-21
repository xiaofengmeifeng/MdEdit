using MdEditor.Controls;
using MdEditor.Models;
using MdEditor.Services;
using Microsoft.Web.WebView2.WinForms;

namespace MdEditor.Forms;

public partial class MainForm
{
    private void BuildUi()
    {
        Text = AppConstants.AppName + " - Markdown 编辑器";
        Width = 1640;
        Height = 840;
        MinimumSize = new Size(900, 520);
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;
        Icon = AssetService.LoadApplicationIcon();

        _editor = new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = true,
            AcceptsTab = true,
            Font = new Font("Consolas", 11f),
            BorderStyle = BorderStyle.None,
            AllowDrop = true,
            HideSelection = false
        };

        _preview = new WebView2 { Dock = DockStyle.Fill };

        // ---- 左侧：打开的文件 + 文件树 ----
        _tree = new TreeView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            HideSelection = false,
            FullRowSelect = true,
            ShowLines = true,
            ShowRootLines = true,
            ItemHeight = 24,
            Font = new Font("Microsoft YaHei UI", 9.5f)
        };
        _treeMenu = BuildTreeMenu();
        _tree.ContextMenuStrip = _treeMenu;

        _fileSearchBox = new TextBox { PlaceholderText = "搜索文件…" };
        _btnFileSearch = MakeIconButton("\uf002", "搜索文件");
        var sidebarSearch = BuildSearchRow(_fileSearchBox, _btnFileSearch);

        // ---- 中间：编辑器 + 格式栏 + 查找栏 ----
        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6
        };
        _formatBar = BuildFormatBar();
        _formatBar.Dock = DockStyle.Top;

        var editorPad = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 6, 8),
            BackColor = Color.White
        };
        editorPad.Controls.Add(_editor);

        var editorHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        _findBar = BuildFindBar();
        editorHost.Controls.Add(editorPad);
        editorHost.Controls.Add(_formatBar);
        editorHost.Controls.Add(_findBar);
        _split.Panel1.Controls.Add(editorHost);

        // ---- 右侧：预览 + 预览搜索 + 目录 ----
        _split.Panel2.Controls.Add(BuildPreviewPanel());

        _mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            FixedPanel = FixedPanel.Panel1
        };
        _mainSplit.Panel1.Controls.Add(_tree);
        _mainSplit.Panel1.Controls.Add(sidebarSearch);
        _mainSplit.Panel2.Controls.Add(_split);

        _menu = BuildMenu();
        _toolbar = BuildToolbar();
        _tabMenu = BuildTabMenu();
        _tabBar = BuildTabBar();
        _status = new StatusStrip();
        _statusFile = new ToolStripStatusLabel("未命名") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _statusInfo = new ToolStripStatusLabel("0 字符");
        _status.Items.Add(_statusFile);
        _status.Items.Add(_statusInfo);

        Controls.Add(_mainSplit);
        Controls.Add(_tabBar);
        Controls.Add(_toolbar);
        Controls.Add(_menu);
        Controls.Add(_status);

        _debounce = new System.Windows.Forms.Timer { Interval = 150 };
        UpdateViewChecks();
    }

    private Panel BuildPreviewPanel()
    {
        _previewSearchBox = new TextBox { PlaceholderText = "在预览中搜索…" };
        _btnPreviewSearch = MakeIconButton("\uf002", "在预览中搜索");
        _btnPrevSearch = MakeIconButton("\uf077", "上一个匹配");
        _btnNextSearch = MakeIconButton("\uf078", "下一个匹配");
        var caseBtn = MakeToggleButton(AssetService.RenderTextIcon("cC"), "区分大小写",
            value => { _previewCase = value; RunPreviewSearch(); });
        var wordBtn = MakeToggleButton(AssetService.RenderTextIcon("W"), "全字匹配",
            value => { _previewWord = value; RunPreviewSearch(); });
        var regexBtn = MakeToggleButton(AssetService.RenderTextIcon(".*"), "正则表达式",
            value => { _previewRegex = value; RunPreviewSearch(); });
        var refreshBtn = MakeIconButton("\uf021", "从磁盘重新加载");
        refreshBtn.Click += (_, _) => ReloadCurrentDocument();
        _previewCount = new Label
        {
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(6, 6, 4, 0),
            ForeColor = Color.FromArgb(90, 96, 104)
        };
        var previewSearch = BuildSearchRow(_previewSearchBox, caseBtn, wordBtn, regexBtn,
            _btnPreviewSearch, _btnPrevSearch, _btnNextSearch, refreshBtn, _previewCount);

        // 目录面板
        _tocHeader = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Color.FromArgb(238, 241, 245) };
        var tocTitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = "  目录",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(90, 96, 104)
        };
        _tocHeader.Controls.Add(tocTitle);

        _tocTree = new TreeView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            HideSelection = false,
            FullRowSelect = true,
            ShowLines = false,
            ShowRootLines = true,
            ItemHeight = 22,
            Font = new Font("Microsoft YaHei UI", 9.5f)
        };

        _tocContent = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        _tocContent.Controls.Add(_tocTree);
        _tocContent.Controls.Add(_tocHeader);

        _tocToggleBtn = new Button
        {
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            Text = string.Empty,
            Image = AssetService.RenderGlyph("\uf054"),
            BackColor = Color.FromArgb(238, 241, 245),
            TabStop = false
        };
        _tocToggleBtn.FlatAppearance.BorderSize = 0;
        _tocToggleBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(226, 232, 240);
        _toolTip.SetToolTip(_tocToggleBtn, "隐藏 / 显示目录");
        _tocToggleBtn.Click += (_, _) => SetTocVisible(!_tocContent.Visible);

        _tocRail = new Panel
        {
            Dock = DockStyle.Left,
            Width = AppConstants.TocRailWidth,
            BackColor = Color.FromArgb(238, 241, 245)
        };
        _tocRail.Controls.Add(_tocToggleBtn);

        _tocHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        _tocHost.Controls.Add(_tocContent);
        _tocHost.Controls.Add(_tocRail);

        _previewSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            FixedPanel = FixedPanel.Panel2
        };
        _previewSplit.Panel1.Controls.Add(_preview);
        _previewSplit.Panel2.Controls.Add(_tocHost);

        var host = new Panel { Dock = DockStyle.Fill };
        host.Controls.Add(_previewSplit);
        host.Controls.Add(previewSearch);
        return host;
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();

        var fileMenu = new ToolStripMenuItem("文件(&F)");
        fileMenu.DropDownItems.Add(MenuItem("新建(&N)", Keys.Control | Keys.N, (_, _) => NewDocument()));
        fileMenu.DropDownItems.Add(MenuItem("打开(&O)…", Keys.Control | Keys.O, (_, _) => OpenDocument()));
        fileMenu.DropDownItems.Add(MenuItem("打开文件夹(&D)…", Keys.Control | Keys.K, (_, _) => OpenFolder()));
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(MenuItem("保存(&S)", Keys.Control | Keys.S, (_, _) => SaveDocument()));
        fileMenu.DropDownItems.Add(MenuItem("另存为(&A)…", Keys.Control | Keys.Shift | Keys.S, (_, _) => SaveDocumentAs()));
        fileMenu.DropDownItems.Add(MenuItem("重新加载(&R)", Keys.F5, (_, _) => ReloadCurrentDocument()));
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(MenuItem("退出(&X)", Keys.Alt | Keys.F4, (_, _) => Close()));

        var editMenu = new ToolStripMenuItem("编辑(&E)");
        editMenu.DropDownItems.Add(MenuItem("查找(&F)…", Keys.Control | Keys.F, (_, _) => ShowFindBar(false)));
        editMenu.DropDownItems.Add(MenuItem("替换(&R)…", Keys.Control | Keys.R, (_, _) => ShowFindBar(true)));
        editMenu.DropDownItems.Add(new ToolStripSeparator());
        editMenu.DropDownItems.Add(MenuItem("下一个匹配", Keys.F3, (_, _) => FindStep(1)));
        editMenu.DropDownItems.Add(MenuItem("上一个匹配", Keys.Shift | Keys.F3, (_, _) => FindStep(-1)));

        var viewMenu = new ToolStripMenuItem("视图(&V)");
        _viewEditItem = MenuItem("仅编辑(&E)", Keys.Control | Keys.D1, (_, _) => SetViewMode(ViewMode.Edit));
        _viewSplitItem = MenuItem("分栏(&S)", Keys.Control | Keys.D2, (_, _) => SetViewMode(ViewMode.Split));
        _viewPreviewItem = MenuItem("仅预览(&P)", Keys.Control | Keys.D3, (_, _) => SetViewMode(ViewMode.Preview));
        viewMenu.DropDownItems.Add(_viewEditItem);
        viewMenu.DropDownItems.Add(_viewSplitItem);
        viewMenu.DropDownItems.Add(_viewPreviewItem);
        viewMenu.DropDownItems.Add(new ToolStripSeparator());

        _toggleSidebarItem = new ToolStripMenuItem("显示文件列表(&L)") { Checked = true, CheckOnClick = true };
        _toggleSidebarItem.CheckedChanged += (_, _) => _mainSplit.Panel1Collapsed = !_toggleSidebarItem.Checked;
        viewMenu.DropDownItems.Add(_toggleSidebarItem);

        _toggleWrapItem = new ToolStripMenuItem("自动换行(&W)") { Checked = true, CheckOnClick = true };
        _toggleWrapItem.CheckedChanged += (_, _) => SetWordWrap(_toggleWrapItem.Checked);
        viewMenu.DropDownItems.Add(_toggleWrapItem);

        _toggleTocItem = new ToolStripMenuItem("显示目录(&C)") { Checked = true, CheckOnClick = true };
        _toggleTocItem.CheckedChanged += (_, _) => SetTocVisible(_toggleTocItem.Checked);
        viewMenu.DropDownItems.Add(_toggleTocItem);

        menu.Items.Add(fileMenu);
        menu.Items.Add(editMenu);
        menu.Items.Add(viewMenu);
        MainMenuStrip = menu;
        return menu;
    }

    private ToolStrip BuildToolbar()
    {
        var toolbar = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            RenderMode = ToolStripRenderMode.System,
            Padding = new Padding(4, 2, 4, 2),
            ImageScalingSize = new Size(AppConstants.IconSize, AppConstants.IconSize)
        };
        toolbar.Items.Add(IconButton("\uf15b", "新建 (Ctrl+N)", NewDocument));
        toolbar.Items.Add(IconButton("\uf07c", "打开 (Ctrl+O)", OpenDocument));
        toolbar.Items.Add(IconButton("\uf07b", "打开文件夹 (Ctrl+K)", OpenFolder));
        toolbar.Items.Add(IconButton("\uf0c7", "保存 (Ctrl+S)", SaveDocument));
        toolbar.Items.Add(IconButton("\uf56e", "另存为 (Ctrl+Shift+S)", SaveDocumentAs));
        toolbar.Items.Add(IconButton("\uf021", "重新加载 (F5)", ReloadCurrentDocument));
        toolbar.Items.Add(new ToolStripSeparator());
        _btnEdit = ViewButton("\uf044", "仅编辑 (Ctrl+1)", ViewMode.Edit);
        _btnSplit = ViewButton("\uf0db", "分栏 (Ctrl+2)", ViewMode.Split);
        _btnPreview = ViewButton("\uf06e", "仅预览 (Ctrl+3)", ViewMode.Preview);
        toolbar.Items.Add(_btnEdit);
        toolbar.Items.Add(_btnSplit);
        toolbar.Items.Add(_btnPreview);
        return toolbar;
    }

    private ToolStrip BuildFormatBar()
    {
        var bar = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            RenderMode = ToolStripRenderMode.System,
            Padding = new Padding(2),
            ImageScalingSize = new Size(AppConstants.IconSize, AppConstants.IconSize),
            BackColor = Color.FromArgb(246, 248, 250)
        };

        var heading = new ToolStripDropDownButton
        {
            Image = AssetService.RenderGlyph("\uf1dc"),
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            ToolTipText = "标题"
        };
        heading.DropDownItems.Add(FormatItem("正文", () => SetHeading(0)));
        for (var level = 1; level <= 6; level++)
        {
            var l = level;
            heading.DropDownItems.Add(FormatItem("H" + l, () => SetHeading(l)));
        }

        bar.Items.Add(heading);
        bar.Items.Add(new ToolStripSeparator());
        bar.Items.Add(IconButton("\uf032", "粗体", () => WrapSelection("**", "**", "粗体")));
        bar.Items.Add(IconButton("\uf033", "斜体", () => WrapSelection("*", "*", "斜体")));
        bar.Items.Add(IconButton("\uf0cc", "删除线", () => WrapSelection("~~", "~~", "删除线")));
        bar.Items.Add(IconButton("\uf121", "行内代码", () => WrapSelection("`", "`", "code")));

        var codeBlock = new ToolStripDropDownButton
        {
            Image = AssetService.RenderGlyph("\uf1c9"),
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            ToolTipText = "代码块（选择语言）"
        };
        foreach (var language in CodeLanguages.All)
        {
            var lang = language;
            codeBlock.DropDownItems.Add(FormatItem(
                string.IsNullOrEmpty(lang) ? "纯文本" : lang,
                () => InsertCodeBlock(lang)));
        }

        bar.Items.Add(codeBlock);
        bar.Items.Add(new ToolStripSeparator());
        bar.Items.Add(IconButton("\uf10d", "引用", () => PrefixSelectedLines("> ")));
        bar.Items.Add(IconButton("\uf0ca", "无序列表", () => PrefixSelectedLines("- ")));
        bar.Items.Add(IconButton("\uf0cb", "有序列表", () => PrefixSelectedLines(string.Empty, true)));
        bar.Items.Add(IconButton("\uf0ae", "任务列表", () => PrefixSelectedLines("- [ ] ")));
        bar.Items.Add(new ToolStripSeparator());
        bar.Items.Add(IconButton("\uf0c1", "链接", () => WrapSelection("[", "](url)", "文字")));
        bar.Items.Add(IconButton("\uf03e", "图片", () => WrapSelection("![", "](url)", "图片")));
        bar.Items.Add(IconButton("\uf0ce", "表格", InsertTable));
        bar.Items.Add(IconButton("\uf068", "分隔线", () => InsertAtCaret(AppConstants.NewLine + "---" + AppConstants.NewLine)));
        return bar;
    }

    private Panel BuildFindBar()
    {
        _findBox = new TextBox { PlaceholderText = "查找" };
        _replaceBox = new TextBox { PlaceholderText = "替换为" };

        var findHost = MakeSearchBox(_findBox, 190, 28);
        var replaceHost = MakeSearchBox(_replaceBox, 190, 28);

        _findCaseBtn = MakeToggleButton(AssetService.RenderTextIcon("cC"), "区分大小写",
            value => { _findCase = value; RunFind(false); });
        _findWordBtn = MakeToggleButton(AssetService.RenderTextIcon("W"), "全字匹配",
            value => { _findWord = value; RunFind(false); });
        _findRegexBtn = MakeToggleButton(AssetService.RenderTextIcon(".*"), "正则表达式",
            value => { _findRegex = value; RunFind(false); });

        var prevBtn = MakeIconButton("\uf077", "上一个匹配");
        prevBtn.Click += (_, _) => FindStep(-1);
        var nextBtn = MakeIconButton("\uf078", "下一个匹配");
        nextBtn.Click += (_, _) => FindStep(1);
        _replaceBtn = MakeIconButton("\uf0ec", "替换");
        _replaceBtn.Click += (_, _) => ReplaceCurrent();
        _replaceAllBtn = MakeIconButton("\uf101", "全部替换");
        _replaceAllBtn.Click += (_, _) => ReplaceAll();
        var closeBtn = MakeIconButton("\uf00d", "关闭");
        closeBtn.Click += (_, _) => HideFindBar();

        _findBox.KeyDown += OnFindBoxKeyDown;
        _findBox.TextChanged += (_, _) => RunFind(false);
        _replaceBox.KeyDown += OnReplaceBoxKeyDown;

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.FromArgb(246, 248, 250)
        };
        flow.Controls.Add(findHost);
        flow.Controls.Add(replaceHost);
        flow.Controls.Add(Sep());
        flow.Controls.Add(_findCaseBtn);
        flow.Controls.Add(_findWordBtn);
        flow.Controls.Add(_findRegexBtn);
        flow.Controls.Add(Sep());
        flow.Controls.Add(prevBtn);
        flow.Controls.Add(nextBtn);
        flow.Controls.Add(Sep());
        flow.Controls.Add(_replaceBtn);
        flow.Controls.Add(_replaceAllBtn);
        flow.Controls.Add(Sep());
        flow.Controls.Add(closeBtn);

        var bar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 46,
            Padding = new Padding(10, 9, 10, 9),
            BackColor = Color.FromArgb(246, 248, 250),
            Visible = false
        };
        bar.Controls.Add(flow);
        return bar;
    }

    private Panel BuildSearchRow(TextBox box, params Control[] rightControls)
    {
        box.Dock = DockStyle.Fill;
        box.BorderStyle = BorderStyle.None;
        box.Font = new Font("Microsoft YaHei UI", 9.5f);

        var boxHost = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(9, 5, 6, 5),
            BackColor = Color.White
        };
        boxHost.Controls.Add(box);

        var rights = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.FromArgb(246, 248, 250),
            Padding = new Padding(2, 0, 0, 0)
        };
        foreach (var control in rightControls)
        {
            rights.Controls.Add(control);
        }

        var row = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(10, 8, 10, 8),
            BackColor = Color.FromArgb(246, 248, 250)
        };
        row.Controls.Add(boxHost);
        row.Controls.Add(rights);
        return row;
    }

    private static Panel MakeSearchBox(TextBox box, int width, int height)
    {
        box.Dock = DockStyle.Fill;
        box.BorderStyle = BorderStyle.None;
        box.Font = new Font("Microsoft YaHei UI", 9.5f);

        var host = new RoundedPanel
        {
            Width = width,
            Height = height,
            Padding = new Padding(8, 4, 6, 4),
            Margin = new Padding(0, 0, 8, 0),
            BackColor = Color.White
        };
        host.Controls.Add(box);
        return host;
    }

    private static Panel Sep() => new()
    {
        Width = 1,
        Height = 20,
        Margin = new Padding(6, 4, 6, 4),
        BackColor = Color.FromArgb(200, 206, 214)
    };

    private Button MakeIconButton(string glyph, string tooltip)
    {
        var button = new Button
        {
            Image = AssetService.RenderGlyph(glyph),
            Width = 32,
            Height = 28,
            FlatStyle = FlatStyle.Flat,
            ImageAlign = ContentAlignment.MiddleCenter,
            Text = string.Empty,
            TabStop = false,
            BackColor = Color.FromArgb(246, 248, 250),
            Margin = new Padding(2, 0, 0, 0)
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(226, 232, 240);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(214, 222, 235);
        _toolTip.SetToolTip(button, tooltip);
        return button;
    }

    private Button MakeToggleButton(Image? image, string tooltip, Action<bool> onToggle)
    {
        var button = new Button
        {
            Image = image,
            Width = 32,
            Height = 28,
            FlatStyle = FlatStyle.Flat,
            ImageAlign = ContentAlignment.MiddleCenter,
            Text = string.Empty,
            TabStop = false,
            BackColor = Color.FromArgb(246, 248, 250),
            Margin = new Padding(2, 0, 0, 0)
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(226, 232, 240);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(214, 222, 235);
        _toolTip.SetToolTip(button, tooltip);

        var enabled = false;
        button.Click += (_, _) =>
        {
            enabled = !enabled;
            button.BackColor = enabled ? Color.FromArgb(199, 220, 252) : Color.FromArgb(246, 248, 250);
            onToggle(enabled);
        };
        return button;
    }

    private static ToolStripMenuItem MenuItem(string text, Keys shortcut, EventHandler handler)
    {
        var item = new ToolStripMenuItem(text) { ShortcutKeys = shortcut, ShowShortcutKeys = true };
        item.Click += handler;
        return item;
    }

    private static ToolStripMenuItem FormatItem(string text, Action action)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (_, _) => action();
        return item;
    }

    private static ToolStripButton IconButton(string glyph, string tooltip, Action action)
    {
        var button = new ToolStripButton
        {
            Image = AssetService.RenderGlyph(glyph),
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            ToolTipText = tooltip
        };
        button.Click += (_, _) => action();
        return button;
    }

    private ToolStripButton ViewButton(string glyph, string tooltip, ViewMode mode)
    {
        var button = new ToolStripButton
        {
            Image = AssetService.RenderGlyph(glyph),
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            CheckOnClick = false,
            ToolTipText = tooltip
        };
        button.Click += (_, _) => SetViewMode(mode);
        return button;
    }

    private Panel BuildTabBar()
    {
        _tabStrip = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.FromArgb(238, 241, 245),
            Padding = new Padding(6, 1, 6, 1)
        };

        // 鼠标滚轮横向滚动标签
        _tabStrip.MouseWheel += (_, e) =>
        {
            var scroll = _tabStrip.HorizontalScroll;
            var value = Math.Max(scroll.Minimum, Math.Min(scroll.Maximum, scroll.Value - e.Delta));
            _tabStrip.HorizontalScroll.Value = value;
        };

        var bar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 33,
            BackColor = Color.FromArgb(238, 241, 245)
        };
        bar.Controls.Add(_tabStrip);
        return bar;
    }

    private ContextMenuStrip BuildTabMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("关闭", null, (_, _) => CloseDocument(_contextDocument));
        menu.Items.Add("关闭其他", null, (_, _) => CloseOtherDocuments(_contextDocument));
        menu.Items.Add("关闭全部", null, (_, _) => CloseAllDocuments());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("重新加载", null, (_, _) =>
        {
            if (_contextDocument == null)
            {
                return;
            }

            if (ReferenceEquals(_contextDocument, _currentDoc))
            {
                ReloadCurrentDocument();
            }
            else
            {
                ReloadDocument(_contextDocument);
            }
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("在资源管理器中显示", null, (_, _) =>
        {
            if (_contextDocument?.Path is { } path)
            {
                RevealInExplorer(path);
            }
        });
        return menu;
    }

    private ContextMenuStrip BuildTreeMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("打开", null, (_, _) => TreeOpen());
        menu.Items.Add("在资源管理器中显示", null, (_, _) => TreeReveal());
        menu.Items.Add("复制完整路径", null, (_, _) => TreeCopyPath());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("重命名", null, (_, _) => TreeRename());
        menu.Items.Add("删除", null, (_, _) => TreeDelete());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("刷新", null, (_, _) => TreeRefresh());
        menu.Opening += (_, _) =>
        {
            var point = _tree.PointToClient(Cursor.Position);
            _contextNode = _tree.GetNodeAt(point);
            menu.Items[0].Enabled = _contextNode != null && IsFileNode(_contextNode);
            menu.Items[2].Enabled = _contextNode?.Tag is string;
        };
        return menu;
    }
}

using System.Text;
using System.Text.RegularExpressions;

namespace MdEditor.Forms;

public partial class MainForm
{
    // ---- 编辑事件 ----

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_suppressTextChanged)
        {
            return;
        }

        if (!_dirty)
        {
            _dirty = true;
            if (_currentDoc != null)
            {
                _currentDoc.IsDirty = true;
                UpdateActiveTabDirty();
            }

            UpdateTitle();
        }

        _debounce.Stop();
        _debounce.Start();
        UpdateStatus();
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.S)
        {
            SaveDocument();
        }
        else if (e.Control && e.KeyCode == Keys.O)
        {
            OpenDocument();
        }
        else if (e.Control && e.KeyCode == Keys.N)
        {
            NewDocument();
        }
        else if (e.Control && e.KeyCode == Keys.B)
        {
            WrapSelection("**", "**", "粗体");
        }
        else if (e.Control && e.KeyCode == Keys.I)
        {
            WrapSelection("*", "*", "斜体");
        }
        else if (e.Control && e.KeyCode == Keys.F)
        {
            ShowFindBar(false);
        }
        else if (e.Control && e.KeyCode == Keys.R)
        {
            ShowFindBar(true);
        }
        else if (e.KeyCode == Keys.F3)
        {
            FindStep(e.Shift ? -1 : 1);
        }
        else
        {
            return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void SetWordWrap(bool wrap)
    {
        try
        {
            _editor.WordWrap = wrap;
            _editor.ScrollBars = wrap ? ScrollBars.Vertical : ScrollBars.Both;
        }
        catch
        {
            // 忽略
        }
    }

    // ---- Markdown 格式化 ----

    private void WrapSelection(string before, string after, string placeholder)
    {
        var text = _editor.Text;
        var start = _editor.SelectionStart;
        var length = _editor.SelectionLength;
        var selected = length > 0 ? text.Substring(start, length) : placeholder;
        _editor.SelectedText = before + selected + after;
        _editor.SelectionStart = start + before.Length;
        _editor.SelectionLength = selected.Length;
        _editor.Focus();
    }

    private void PrefixSelectedLines(string prefix, bool ordered = false)
    {
        ApplyLineTransform(lineIndex =>
            ordered ? (lineIndex + 1) + ". " : prefix);
    }

    private void SetHeading(int level)
    {
        var prefix = level <= 0 ? string.Empty : new string('#', level) + " ";
        ApplyLineTransform(_ => prefix, stripHeading: true);
    }

    private void ApplyLineTransform(Func<int, string> prefixForLine, bool stripHeading = false)
    {
        var text = _editor.Text;
        var start = _editor.SelectionStart;
        var length = _editor.SelectionLength;
        var firstLineStart = text.LastIndexOf('\n', Math.Max(0, start - 1)) + 1;
        var lastLineEnd = text.IndexOf('\n', start + length);
        if (lastLineEnd < 0)
        {
            lastLineEnd = text.Length;
        }

        var block = text.Substring(firstLineStart, lastLineEnd - firstLineStart);
        var lines = block.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var builder = new StringBuilder();
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(AppConstants.NewLine);
            }

            var line = lines[i];
            if (stripHeading)
            {
                line = Regex.Replace(line, @"^\s*#{1,6}\s+", string.Empty);
            }

            builder.Append(prefixForLine(i)).Append(line);
        }

        _editor.Select(firstLineStart, lastLineEnd - firstLineStart);
        _editor.SelectedText = builder.ToString();
        _editor.Focus();
    }

    private void InsertCodeBlock(string language)
    {
        var start = _editor.SelectionStart;
        var selected = _editor.SelectedText;
        var body = selected;
        if (body.Length > 0 && !body.EndsWith(AppConstants.NewLine))
        {
            body += AppConstants.NewLine;
        }

        _editor.SelectedText = "```" + language + AppConstants.NewLine + body + "```";
        _editor.SelectionStart = start + 3 + language.Length + AppConstants.NewLine.Length;
        _editor.SelectionLength = selected.Length;
        _editor.Focus();
    }

    private void InsertTable()
    {
        InsertAtCaret(
            AppConstants.NewLine + "| 列1 | 列2 | 列3 |" +
            AppConstants.NewLine + "| --- | --- | --- |" +
            AppConstants.NewLine + "| 内容 | 内容 | 内容 |" +
            AppConstants.NewLine);
    }

    private void InsertAtCaret(string text)
    {
        var start = _editor.SelectionStart;
        _editor.SelectedText = text;
        _editor.SelectionStart = start + text.Length;
        _editor.SelectionLength = 0;
        _editor.Focus();
    }

    // ---- 查找 / 替换 ----

    private void ShowFindBar(bool withReplace)
    {
        _findBar.Visible = true;
        _replaceBox.Visible = withReplace;
        _replaceBtn.Visible = withReplace;
        _replaceAllBtn.Visible = withReplace;

        if (_findBox.Text.Length == 0 && _editor.SelectionLength is > 0 and < 200 &&
            !_editor.SelectedText.Contains('\n'))
        {
            _findBox.Text = _editor.SelectedText;
        }

        if (withReplace)
        {
            _replaceBox.Focus();
            _replaceBox.SelectAll();
        }
        else
        {
            _findBox.Focus();
            _findBox.SelectAll();
        }

        RunFind(false);
    }

    private void HideFindBar()
    {
        _findBar.Visible = false;
        _findMatches.Clear();
        _findIndex = -1;
        _editor.Focus();
    }

    private void OnFindBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            FindStep(e.Shift ? -1 : 1);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            HideFindBar();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void OnReplaceBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            ReplaceCurrent();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            HideFindBar();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private Regex? BuildFindRegex()
    {
        var term = _findBox.Text;
        if (term.Length == 0)
        {
            return null;
        }

        var options = RegexOptions.Multiline;
        if (!_findCase)
        {
            options |= RegexOptions.IgnoreCase;
        }

        try
        {
            return new Regex(term, options);
        }
        catch
        {
            return null;
        }
    }

    private void RunFind(bool keepIndex)
    {
        _findMatches.Clear();
        var term = _findBox.Text;
        if (term.Length == 0)
        {
            _findIndex = -1;
            return;
        }

        var text = _editor.Text;
        if (_findRegex)
        {
            var regex = BuildFindRegex();
            if (regex != null)
            {
                foreach (Match match in regex.Matches(text))
                {
                    if (match.Length > 0)
                    {
                        _findMatches.Add((match.Index, match.Length));
                    }
                }
            }
        }
        else
        {
            var comparison = _findCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var position = 0;
            while (position <= text.Length - term.Length)
            {
                var index = text.IndexOf(term, position, comparison);
                if (index < 0)
                {
                    break;
                }

                if (!_findWord || IsWholeWord(text, index, term.Length))
                {
                    _findMatches.Add((index, term.Length));
                }

                position = index + Math.Max(1, term.Length);
            }
        }

        if (_findMatches.Count == 0)
        {
            _findIndex = -1;
            return;
        }

        if (!keepIndex || _findIndex < 0 || _findIndex >= _findMatches.Count)
        {
            var caret = _editor.SelectionStart;
            _findIndex = 0;
            for (var k = 0; k < _findMatches.Count; k++)
            {
                if (_findMatches[k].Start >= caret)
                {
                    _findIndex = k;
                    break;
                }
            }
        }

        GoToMatch(_findIndex);
    }

    private void FindStep(int delta)
    {
        if (_findMatches.Count == 0)
        {
            RunFind(false);
        }

        if (_findMatches.Count == 0)
        {
            return;
        }

        _findIndex = _findIndex < 0
            ? 0
            : (_findIndex + delta + _findMatches.Count) % _findMatches.Count;
        GoToMatch(_findIndex);
    }

    private void GoToMatch(int index)
    {
        if (index < 0 || index >= _findMatches.Count)
        {
            return;
        }

        var (start, length) = _findMatches[index];
        _editor.Select(start, length);
        _editor.ScrollToCaret();
    }

    private void ReplaceCurrent()
    {
        if (_findIndex < 0 || _findIndex >= _findMatches.Count)
        {
            FindStep(1);
            return;
        }

        var (start, length) = _findMatches[_findIndex];
        var replacement = _replaceBox.Text;
        if (_findRegex)
        {
            var regex = BuildFindRegex();
            if (regex != null)
            {
                var match = regex.Match(_editor.Text, start, length);
                if (match.Success && match.Index == start)
                {
                    replacement = match.Result(_replaceBox.Text);
                }
            }
        }

        _editor.Select(start, length);
        _editor.SelectedText = replacement;
        RunFind(true);
        FindStep(1);
    }

    private void ReplaceAll()
    {
        if (_findBox.Text.Length == 0)
        {
            return;
        }

        RunFind(true);
        if (_findMatches.Count == 0)
        {
            return;
        }

        if (_findRegex)
        {
            var regex = BuildFindRegex();
            if (regex == null)
            {
                return;
            }

            _editor.Text = regex.Replace(_editor.Text, _replaceBox.Text);
        }
        else
        {
            var text = _editor.Text;
            for (var k = _findMatches.Count - 1; k >= 0; k--)
            {
                var (start, length) = _findMatches[k];
                text = text.Substring(0, start) + _replaceBox.Text + text.Substring(start + length);
            }

            _editor.Text = text;
        }

        RunFind(false);
    }

    private static bool IsWholeWord(string text, int start, int length)
    {
        var before = start == 0 || !IsWordChar(text[start - 1]);
        var after = start + length >= text.Length || !IsWordChar(text[start + length]);
        return before && after;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}

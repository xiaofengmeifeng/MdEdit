namespace MdEditor.Services;

/// <summary>
/// 预览页面的 HTML 外壳：内嵌 GitHub 风格样式、highlight.js 与自定义脚本
/// （复制代码按钮、预览内查找高亮/跳转）。脚本均为纯文本注入，不依赖外部网络。
/// </summary>
internal static class PreviewDocument
{
    private static string? _shell;

    /// <summary>构建预览外壳 HTML。</summary>
    public static string BuildShellHtml()
    {
        return _shell ??=
            "<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">" +
            "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
            "<base id=\"baseEl\" href=\"about:blank\">" +
            "<style>" + GitHubCss + "\n" + AssetService.HighlightStyle + "</style></head>" +
            "<body><article id=\"content\" class=\"markdown-body\"></article>" +
            "<script>" + AssetService.HighlightScript + "</script>" +
            "<script>" + InteractionScript + "</script></body></html>";
    }

    private const string InteractionScript = @"
window.makeCopyIcon = function (kind) {
  var ns = 'http://www.w3.org/2000/svg';
  var svg = document.createElementNS(ns, 'svg');
  svg.setAttribute('width', '14');
  svg.setAttribute('height', '14');
  svg.setAttribute('viewBox', '0 0 24 24');
  svg.setAttribute('fill', 'none');
  svg.setAttribute('stroke', 'currentColor');
  svg.setAttribute('stroke-width', '2');
  svg.setAttribute('stroke-linecap', 'round');
  svg.setAttribute('stroke-linejoin', 'round');
  if (kind === 'check') {
    var pl = document.createElementNS(ns, 'polyline');
    pl.setAttribute('points', '20 6 9 17 4 12');
    svg.appendChild(pl);
  } else {
    var r = document.createElementNS(ns, 'rect');
    r.setAttribute('x', '9');
    r.setAttribute('y', '9');
    r.setAttribute('width', '13');
    r.setAttribute('height', '13');
    r.setAttribute('rx', '2');
    r.setAttribute('ry', '2');
    svg.appendChild(r);
    var p = document.createElementNS(ns, 'path');
    p.setAttribute('d', 'M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1');
    svg.appendChild(p);
  }
  return svg;
};
window.addCopyButtons = function () {
  var pres = document.querySelectorAll('#content pre');
  for (var i = 0; i < pres.length; i++) {
    (function (pre) {
      if (pre.querySelector('.copy-btn')) { return; }
      var code = pre.querySelector('code');
      var btn = document.createElement('button');
      btn.className = 'copy-btn';
      btn.type = 'button';
      btn.title = '复制代码';
      btn.appendChild(window.makeCopyIcon('copy'));
      btn.addEventListener('click', function () {
        var text = code ? code.innerText : pre.innerText;
        try {
          window.chrome.webview.postMessage(JSON.stringify({ type: 'copy', text: text }));
        } catch (e) {}
        btn.classList.add('copied');
        btn.textContent = '';
        btn.appendChild(window.makeCopyIcon('check'));
        setTimeout(function () {
          btn.classList.remove('copied');
          btn.textContent = '';
          btn.appendChild(window.makeCopyIcon('copy'));
        }, 1200);
      });
      pre.appendChild(btn);
    })(pres[i]);
  }
};
window._hits = [];
window._hitIndex = -1;
window.clearPreviewSearch = function () {
  var marks = document.querySelectorAll('#content mark.search-hit');
  for (var i = marks.length - 1; i >= 0; i--) {
    var m = marks[i];
    var parent = m.parentNode;
    if (parent) {
      parent.replaceChild(document.createTextNode(m.textContent), m);
      parent.normalize();
    }
  }
};
window.searchInPreview = function (term, caseSensitive, wholeWord, regex) {
  window.clearPreviewSearch();
  window._hits = [];
  window._hitIndex = -1;
  window._lastTerm = term;
  if (!term) { return 0; }
  var root = document.getElementById('content');
  if (!root) { return 0; }

  var rx = null;
  if (regex) {
    try { rx = new RegExp(term, caseSensitive ? 'g' : 'gi'); } catch (e) { rx = null; }
  }

  function isWordChar(c) { return /[A-Za-z0-9_]/.test(c); }

  var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
    acceptNode: function (node) {
      if (!node.nodeValue || !node.nodeValue.trim()) { return NodeFilter.FILTER_REJECT; }
      var p = node.parentNode;
      while (p && p !== root) {
        if (p.classList && p.classList.contains('copy-btn')) { return NodeFilter.FILTER_REJECT; }
        p = p.parentNode;
      }
      return NodeFilter.FILTER_ACCEPT;
    }
  });
  var nodes = [];
  var n;
  while ((n = walker.nextNode())) { nodes.push(n); }
  var hits = [];

  for (var i = 0; i < nodes.length; i++) {
    var node = nodes[i];
    var text = node.nodeValue;
    var ranges = [];

    if (regex) {
      if (rx) {
        rx.lastIndex = 0;
        var m;
        while ((m = rx.exec(text)) !== null) {
          if (m[0].length === 0) { rx.lastIndex++; continue; }
          ranges.push([m.index, m[0].length]);
        }
      }
    } else {
      var hay = caseSensitive ? text : text.toLowerCase();
      var needle = caseSensitive ? term : term.toLowerCase();
      var idx = hay.indexOf(needle);
      while (idx >= 0) {
        var ok = true;
        if (wholeWord) {
          var before = idx > 0 ? text.charAt(idx - 1) : '';
          var after = idx + needle.length < text.length ? text.charAt(idx + needle.length) : '';
          ok = (!before || !isWordChar(before)) && (!after || !isWordChar(after));
        }
        if (ok) { ranges.push([idx, needle.length]); }
        idx = hay.indexOf(needle, idx + Math.max(1, needle.length));
      }
    }

    if (regex && wholeWord) {
      ranges = ranges.filter(function (r) {
        var before = r[0] > 0 ? text.charAt(r[0] - 1) : '';
        var after = r[0] + r[1] < text.length ? text.charAt(r[0] + r[1]) : '';
        return (!before || !isWordChar(before)) && (!after || !isWordChar(after));
      });
    }

    if (ranges.length === 0) { continue; }

    var frag = document.createDocumentFragment();
    var pos = 0;
    for (var k = 0; k < ranges.length; k++) {
      var s = ranges[k][0];
      var len = ranges[k][1];
      if (s > pos) { frag.appendChild(document.createTextNode(text.substring(pos, s))); }
      var mark = document.createElement('mark');
      mark.className = 'search-hit';
      mark.textContent = text.substr(s, len);
      frag.appendChild(mark);
      hits.push(mark);
      pos = s + len;
    }
    if (pos < text.length) { frag.appendChild(document.createTextNode(text.substring(pos))); }
    if (node.parentNode) { node.parentNode.replaceChild(frag, node); }
  }

  window._hits = hits;
  if (hits.length > 0) { window._hitIndex = 0; window._focusHit(); }
  return hits.length;
};
window._focusHit = function () {
  var hits = window._hits || [];
  for (var i = 0; i < hits.length; i++) { hits[i].classList.remove('current'); }
  if (window._hitIndex >= 0 && window._hitIndex < hits.length) {
    var el = hits[window._hitIndex];
    el.classList.add('current');
    try { el.scrollIntoView({ block: 'center', behavior: 'smooth' }); }
    catch (e) { el.scrollIntoView(); }
  }
};
window.stepHit = function (delta) {
  var hits = window._hits || [];
  if (hits.length === 0) { return -1; }
  window._hitIndex = (window._hitIndex + delta + hits.length) % hits.length;
  window._focusHit();
  return window._hitIndex;
};
";

    private const string GitHubCss = @"
:root { color-scheme: light; }
* { box-sizing: border-box; }
html, body { margin: 0; padding: 0; }
body {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Microsoft YaHei', 'PingFang SC', Roboto, Helvetica, Arial, sans-serif;
  font-size: 16px;
  line-height: 1.7;
  color: #1f2328;
  background: #ffffff;
}
.markdown-body {
  max-width: 900px;
  margin: 0 auto;
  padding: 32px 40px 64px;
  word-wrap: break-word;
}
.markdown-body > *:first-child { margin-top: 0 !important; }
.markdown-body > *:last-child { margin-bottom: 0 !important; }
.markdown-body h1, .markdown-body h2, .markdown-body h3,
.markdown-body h4, .markdown-body h5, .markdown-body h6 {
  margin-top: 24px;
  margin-bottom: 16px;
  font-weight: 600;
  line-height: 1.25;
}
.markdown-body h1 { font-size: 2em; padding-bottom: .3em; border-bottom: 1px solid #d0d7de; }
.markdown-body h2 { font-size: 1.5em; padding-bottom: .3em; border-bottom: 1px solid #d0d7de; }
.markdown-body h3 { font-size: 1.25em; }
.markdown-body h4 { font-size: 1em; }
.markdown-body h5 { font-size: .875em; }
.markdown-body h6 { font-size: .85em; color: #59636e; }
.markdown-body p, .markdown-body blockquote, .markdown-body ul,
.markdown-body ol, .markdown-body dl, .markdown-body table,
.markdown-body pre, .markdown-body details { margin-top: 0; margin-bottom: 16px; }
.markdown-body a { color: #0969da; text-decoration: none; }
.markdown-body a:hover { text-decoration: underline; }
.markdown-body strong { font-weight: 600; }
.markdown-body hr { height: .25em; padding: 0; margin: 24px 0; background-color: #d0d7de; border: 0; }
.markdown-body blockquote {
  padding: 0 1em; color: #59636e; border-left: .25em solid #d0d7de;
}
.markdown-body ul, .markdown-body ol { padding-left: 2em; }
.markdown-body li + li { margin-top: .25em; }
.markdown-body li > p { margin-top: 16px; }
.markdown-body ul.task-list { list-style: none; padding-left: 1.4em; }
.markdown-body ul.task-list li { position: relative; }
.markdown-body ul.task-list input[type=checkbox] { margin-right: .5em; }
.markdown-body code {
  font-family: 'Cascadia Code', Consolas, 'Courier New', monospace;
  font-size: 85%;
  padding: .2em .4em;
  background-color: #eff1f3;
  border-radius: 6px;
}
.markdown-body pre {
  position: relative;
  padding: 16px;
  overflow: auto;
  font-size: 85%;
  line-height: 1.45;
  background-color: #f6f8fa;
  border-radius: 6px;
}
.markdown-body pre code {
  display: inline; padding: 0; margin: 0; background: transparent;
  font-size: 100%; white-space: pre; word-break: normal;
}
.copy-btn {
  position: absolute; top: 8px; right: 8px;
  display: inline-flex; align-items: center; justify-content: center;
  width: 26px; height: 26px; padding: 0;
  color: #57606a; background: rgba(255,255,255,.85);
  border: 1px solid #d0d7de; border-radius: 6px; cursor: pointer;
  opacity: .55; transition: opacity .15s, color .15s, border-color .15s;
}
.markdown-body pre:hover .copy-btn { opacity: 1; }
.copy-btn:hover { color: #0969da; border-color: #0969da; }
.copy-btn.copied { color: #1a7f37; border-color: #1a7f37; opacity: 1; }
.copy-btn svg { pointer-events: none; }
mark.search-hit { background: #ffe58f; color: inherit; padding: 0 1px; border-radius: 2px; }
mark.search-hit.current { background: #ff9f0a; color: #1f2328; }
.markdown-body table { display: block; width: max-content; max-width: 100%; overflow: auto; border-spacing: 0; border-collapse: collapse; }
.markdown-body table th, .markdown-body table td { padding: 6px 13px; border: 1px solid #d0d7de; }
.markdown-body table tr { background-color: #ffffff; border-top: 1px solid #d0d7de; }
.markdown-body table tr:nth-child(2n) { background-color: #f6f8fa; }
.markdown-body img { max-width: 100%; }
.markdown-body kbd {
  display: inline-block; padding: 3px 5px; font-size: 11px; line-height: 10px;
  color: #1f2328; vertical-align: middle; background-color: #f6f8fa;
  border: 1px solid #d1d9e0; border-radius: 6px; box-shadow: inset 0 -1px 0 #d1d9e0;
}
";
}

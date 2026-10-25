using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Phloem.Helpers;

/// <summary>
/// Turns the small Markdown the legal texts use into a simple HTML page in the app's colours, for a WebView:
/// headings (#, ##, ###) with an optional anchor ("## Selling {#sellers}" → id="sellers"), paragraphs, bullet lists,
/// quotes, **bold**, *italic* and [links](/privacy). Every piece of text is HTML-encoded first, so nothing in the
/// text can run as code.
/// </summary>
public static partial class SimpleMarkdown
{
    /// <summary>The website; a link like "/privacy" in the text points here (LegalPage opens those in the app).</summary>
    public const string SiteHost = "phloemmarket.com";
    [GeneratedRegex(@"^(.*?)\s*\{#([\w-]+)\}\s*$")]
    private static partial Regex AnchorRegex();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"(?<!\*)\*(?!\s)(.+?)\*")]
    private static partial Regex ItalicRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)\s]+)\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"^(#{1,3})\s+(.*)$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^\s*[-*]\s+")]
    private static partial Regex BulletRegex();

    /// <summary>The whole HTML page.</summary>
    /// <param name="markdown">The text.</param>
    /// <param name="scrollTo">An anchor to open at (e.g. "sellers"), or null.</param>
    public static string ToHtmlPage(string markdown, string? scrollTo)
    {
        var body = ToHtml(markdown);
        var script = string.IsNullOrWhiteSpace(scrollTo)
            ? string.Empty
            : $"<script>window.onload=function(){{var e=document.getElementById('{WebUtility.HtmlEncode(scrollTo)}');" +
              "if(e)e.scrollIntoView();};</script>";
        return $$"""
            <!DOCTYPE html><html><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <style>
              body { margin: 0; padding: 4px 18px 32px; font-family: -apple-system, Roboto, "Segoe UI", sans-serif;
                     font-size: 16px; line-height: 1.6; color: #070F19; background: #F8F5EF; }
              h2 { font-size: 19px; margin: 24px 0 6px; color: #043D1F; }
              h3 { font-size: 17px; margin: 18px 0 4px; }
              p, ul { margin: 0 0 12px; }
              ul { padding-left: 20px; }
              li + li { margin-top: 4px; }
              a { color: #046334; font-weight: 600; }
              blockquote { margin: 12px 0; padding: 10px 12px; border-left: 4px solid #F4A51A;
                           background: #FDF3E6; border-radius: 8px; }
            </style>{{script}}</head><body>{{body}}</body></html>
            """;
    }

    /// <summary>The text as HTML (no page around it).</summary>
    /// <param name="markdown">The text.</param>
    public static string ToHtml(string markdown)
    {
        var html = new StringBuilder();
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) { i++; continue; }

            var heading = HeadingRegex().Match(line);
            if (heading.Success)
            {
                var level = heading.Groups[1].Value.Length == 3 ? 3 : 2;
                var raw = heading.Groups[2].Value;
                var anchor = AnchorRegex().Match(raw);
                var text = anchor.Success ? anchor.Groups[1].Value : raw;
                var id = anchor.Success ? $" id=\"{anchor.Groups[2].Value}\"" : string.Empty;
                html.Append($"<h{level}{id}>{Inline(text)}</h{level}>");
                i++;
                continue;
            }

            if (BulletRegex().IsMatch(line))
            {
                html.Append("<ul>");
                while (i < lines.Length && BulletRegex().IsMatch(lines[i]))
                    html.Append($"<li>{Inline(BulletRegex().Replace(lines[i++], string.Empty))}</li>");
                html.Append("</ul>");
                continue;
            }

            if (line.StartsWith('>'))
            {
                var quote = new List<string>();
                while (i < lines.Length && lines[i].StartsWith('>')) quote.Add(lines[i++].TrimStart('>').Trim());
                html.Append($"<blockquote>{Inline(string.Join(' ', quote))}</blockquote>");
                continue;
            }

            var paragraph = new List<string>();
            while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]) && !HeadingRegex().IsMatch(lines[i])
                   && !BulletRegex().IsMatch(lines[i]) && !lines[i].StartsWith('>'))
                paragraph.Add(lines[i++].Trim());
            html.Append($"<p>{Inline(string.Join(' ', paragraph))}</p>");
        }
        return html.ToString();
    }

    /// <summary>Bold, italic and links, after encoding the text.</summary>
    private static string Inline(string text)
    {
        var s = WebUtility.HtmlEncode(text);
        s = LinkRegex().Replace(s, m =>
        {
            var href = m.Groups[2].Value;
            if (href.StartsWith('/')) href = $"https://{SiteHost}{href}";               // "/privacy" → the website
            return $"<a href=\"{href}\">{m.Groups[1].Value}</a>";
        });
        s = BoldRegex().Replace(s, "<strong>$1</strong>");
        s = ItalicRegex().Replace(s, "<em>$1</em>");
        return s;
    }
}

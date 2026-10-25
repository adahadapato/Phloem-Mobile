using CommunityToolkit.Mvvm.ComponentModel;
using Phloem.Helpers;
using Phloem.Services;
using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.ViewModels;

/// <summary>
/// Terms &amp; Conditions / Privacy Policy page (route "legal?type=Terms&amp;section=sellers"): loads the current
/// version from the API (or the copy kept on the phone) and shows it as a simple HTML page.
/// </summary>
public partial class LegalViewModel(LegalService legal) : ObservableObject, IQueryAttributable
{
    private LegalDocumentType _type = LegalDocumentType.Terms;
    private string? _section;

    [ObservableProperty]
    public partial string Title { get; set; } = "Terms & Conditions";

    /// <summary>"Version 1 · in effect from 25 Oct 2026".</summary>
    [ObservableProperty]
    public partial string VersionText { get; set; } = string.Empty;

    /// <summary>True while the wording is temporary: the page says so.</summary>
    [ObservableProperty]
    public partial bool IsDraft { get; set; }

    /// <summary>The text for the WebView; null until loaded.</summary>
    [ObservableProperty]
    public partial HtmlWebViewSource? Page { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>Reads "type" (Terms / Privacy) and "section" (an anchor such as "sellers").</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("type", out var t) && Enum.TryParse<LegalDocumentType>(t?.ToString(), true, out var type))
            _type = type;
        _section = query.TryGetValue("section", out var s) ? s?.ToString() : null;
        Title = _type == LegalDocumentType.Privacy ? "Privacy Policy" : "Terms & Conditions";
    }

    /// <summary>Loads the text (the page calls this when it opens).</summary>
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        Message = string.Empty;
        try
        {
            var (document, error) = await legal.GetAsync(_type);
            if (document is null)
            {
                Message = error?.Status == 404
                    ? "This document isn't available yet."
                    : "We couldn't load this page right now. Please check your internet connection and try again.";
                return;
            }

            Title = document.Title;
            IsDraft = document.IsDraft;
            VersionText = $"Version {document.Version} · in effect from {document.PublishedAt.ToLocalTime():d MMM yyyy}";
            // The page says "Draft" itself, so the file's own draft note (a quote at the top) isn't shown twice
            var text = document.IsDraft && document.Content.TrimStart().StartsWith('>')
                ? string.Join('\n', document.Content.TrimStart().Split('\n').SkipWhile(l => l.StartsWith('>')))
                : document.Content;
            Page = new HtmlWebViewSource { Html = SimpleMarkdown.ToHtmlPage(text, _section) };
        }
        finally
        {
            IsLoading = false;
        }
    }
}

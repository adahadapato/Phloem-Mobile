using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Phloem.Helpers;
using Phloem.Models;

namespace Phloem.ViewModels;

/// <summary>
/// The full-screen State / LGA list: a search box that filters as you type (small typos forgiven, e.g. "lagso" finds
/// Lagos) and the matching names, best first. The page returns the tapped one.
/// </summary>
public partial class LocationPickerViewModel : ObservableObject
{
    private readonly IReadOnlyList<LocationOption> _all;

    public LocationPickerViewModel(string title, IReadOnlyList<LocationOption> options, Guid? selectedId)
    {
        Title = title;
        _all = options.Select(o => o with { IsSelected = o.Id == selectedId }).OrderBy(o => o.Name).ToList();
        Filter();
    }

    public string Title { get; }

    public ObservableCollection<LocationOption> Shown { get; } = new();

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool NoMatch { get; set; }

    partial void OnSearchTextChanged(string value) => Filter();

    private void Filter()
    {
        var typed = SearchText.Trim();
        var matches = typed.Length == 0
            ? _all
            : _all.Select(o => (o, score: TextMatch.Score(o.Name, typed)))
                  .Where(x => x.score is not null)
                  .OrderBy(x => x.score).ThenBy(x => x.o.Name)
                  .Select(x => x.o)
                  .ToList();

        Shown.Clear();
        foreach (var option in matches) Shown.Add(option);
        NoMatch = Shown.Count == 0;
    }
}

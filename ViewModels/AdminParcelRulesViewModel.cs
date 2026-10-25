using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Services;
using Phloem.Services.Api;
using Phloem.Services.Api.Contracts.Commands;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.ViewModels;

/// <summary>One line on Admin → What can be sent.</summary>
public sealed class AdminParcelRuleRow
{
    public required ParcelItemRuleDto Rule { get; init; }
    public bool IsFirst { get; init; }
    public bool IsLast { get; init; }
    public string Name => Rule.Name;
    public string? Detail => Rule.Detail;
    public bool HasDetail => !string.IsNullOrWhiteSpace(Rule.Detail);
    public bool IsRestricted => Rule.Kind == ParcelItemRuleKind.Restricted;
    public string KindText => IsRestricted ? "ONLY IF" : "NOT ALLOWED";
    public bool CanMoveUp => !IsFirst;
    public bool CanMoveDown => !IsLast;
}

/// <summary>A kind in the form's picker.</summary>
public sealed class RuleKindOption
{
    public required ParcelItemRuleKind Value { get; init; }
    public string Name => Value == ParcelItemRuleKind.Restricted
        ? "Only if… (allowed with a condition)"
        : "Not allowed";
}

/// <summary>
/// Admin → What can be sent (route "adminparcelrules"): the list customers see on Send food to the UK (and the
/// website's Send a parcel), kept in the API's database.
/// <list type="bullet">
///   <item><description><b>Add</b> an item: not allowed, or allowed only with a condition (the detail says
///   which).</description></item>
///   <item><description><b>Tap</b> an item to change it; <b>✕</b> deletes it; <b>↑ ↓</b> move it.</description></item>
/// </list>
/// Changes show to customers straight away.
/// </summary>
public partial class AdminParcelRulesViewModel(ParcelRulesService rules) : ObservableObject
{
    public ObservableCollection<AdminParcelRuleRow> Rules { get; } = new();

    public IReadOnlyList<RuleKindOption> Kinds { get; } =
        Enum.GetValues<ParcelItemRuleKind>().Select(k => new RuleKindOption { Value = k }).ToList();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsError { get; set; }

    // ---------- Form (new item, or the one being changed) ----------

    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormTitle))]
    public partial ParcelItemRuleDto? EditingRule { get; set; }

    public string FormTitle => EditingRule is null ? "New item" : "Change item";

    [ObservableProperty]
    public partial RuleKindOption? Kind { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Detail { get; set; } = string.Empty;

    // ═════════════════════════════ Loading ═════════════════════════════

    /// <summary>Called each time the page appears (and after every change): the list as it is on the server.</summary>
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var result = await rules.GetLiveAsync();
            if (result.IsFailure)
            {
                ShowError(result.Error!.DisplayMessage);
                return;
            }

            var list = result.Value!;
            Rules.Clear();
            for (var i = 0; i < list.Count; i++)
                Rules.Add(new AdminParcelRuleRow { Rule = list[i], IsFirst = i == 0, IsLast = i == list.Count - 1 });

            if (Rules.Count == 0 && !IsEditing)
                ShowInfo("The list is empty. Tap \"+ Add item\" to add the first one.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Refresh() => await LoadAsync();

    // ═════════════════════════════ Add / change ═════════════════════════════

    [RelayCommand]
    private void Add()
    {
        EditingRule = null;
        Kind = Kinds[0];
        Name = Detail = string.Empty;
        IsEditing = true;
        Message = string.Empty;
    }

    [RelayCommand]
    private void Edit(AdminParcelRuleRow row)
    {
        EditingRule = row.Rule;
        Kind = Kinds.First(k => k.Value == row.Rule.Kind);
        Name = row.Rule.Name;
        Detail = row.Rule.Detail ?? string.Empty;
        IsEditing = true;
        Message = string.Empty;
    }

    [RelayCommand]
    private void Cancel() => IsEditing = false;

    [RelayCommand]
    private async Task Save()
    {
        if (IsBusy || Kind is null) return;
        if (string.IsNullOrWhiteSpace(Name))
        {
            ShowError("Enter the item, e.g. \"Fresh or frozen meat and fish\".");
            return;
        }
        if (Kind.Value == ParcelItemRuleKind.Restricted && string.IsNullOrWhiteSpace(Detail))
        {
            ShowError("Say when it's allowed, e.g. \"Sealed bottles, 1 litre or less each\".");
            return;
        }

        var body = new ParcelItemRuleRequest(Kind.Value, Name.Trim(),
                                             string.IsNullOrWhiteSpace(Detail) ? null : Detail.Trim());
        IsBusy = true;
        try
        {
            ApiResult result = EditingRule is { } existing
                ? await rules.UpdateAsync(existing.Id, body)
                : await rules.AddAsync(body);
            if (result.IsFailure)
            {
                ShowError(result.Error!.DisplayMessage);
                return;
            }

            var added = EditingRule is null;
            IsEditing = false;
            await LoadAsync();
            ShowInfo(added ? $"\"{body.Name}\" added at the end of the list." : "Saved.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ═════════════════════════════ Delete / move ═════════════════════════════

    [RelayCommand]
    private async Task Delete(AdminParcelRuleRow row)
    {
        var ok = await Shell.Current.DisplayAlertAsync("Remove this item?",
            $"{row.Name}\nCustomers won't see it on the list any more.", "Remove", "Keep");
        if (!ok) return;

        var result = await rules.DeleteAsync(row.Rule.Id);
        if (result.IsFailure)
        {
            ShowError(result.Error!.DisplayMessage);
            return;
        }

        if (EditingRule?.Id == row.Rule.Id) IsEditing = false;
        await LoadAsync();
        ShowInfo($"\"{row.Name}\" removed.");
    }

    [RelayCommand]
    private async Task MoveUp(AdminParcelRuleRow row) => await MoveAsync(row, -1);

    [RelayCommand]
    private async Task MoveDown(AdminParcelRuleRow row) => await MoveAsync(row, +1);

    /// <summary>Swaps the item with its neighbour and saves the whole order.</summary>
    private async Task MoveAsync(AdminParcelRuleRow row, int step)
    {
        if (IsBusy) return;
        var ids = Rules.Select(r => r.Rule.Id).ToList();
        var from = ids.IndexOf(row.Rule.Id);
        var to = from + step;
        if (from < 0 || to < 0 || to >= ids.Count) return;
        (ids[from], ids[to]) = (ids[to], ids[from]);

        IsBusy = true;
        try
        {
            var result = await rules.ReorderAsync(ids);
            if (result.IsFailure)
            {
                ShowError(result.Error!.DisplayMessage);   // e.g. someone else changed the list: it reloads below
            }
            else
            {
                Message = string.Empty;
            }
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowError(string message)
    {
        IsError = true;
        Message = message;
    }

    private void ShowInfo(string message)
    {
        IsError = false;
        Message = message;
    }
}

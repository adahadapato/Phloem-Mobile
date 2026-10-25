using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using Phloem.Models;
using Phloem.Services;
using Phloem.Services.Api;
using Phloem.Services.Api.Contracts;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;
using Phloem.Settings;

namespace Phloem.ViewModels;

/// <summary>
/// Admin dashboard (route "admin"), from the API:
/// <list type="bullet">
///   <item><description><b>Sellers:</b> shops waiting for approval (Approve / Reject), dish name suggestions, and every
///   shop with its logo (✓ badge, Block / Unblock, open its details and documents).</description></item>
///   <item><description><b>Users:</b> everyone who has signed up; Suspend (reason + how long) and Lift suspension.
///   A suspended person's shop is hidden with them.</description></item>
///   <item><description><b>Shipments:</b> Shipping routes and All shipments (their own pages).</description></item>
/// </list>
/// </summary>
public partial class AdminViewModel : ObservableObject
{
    private readonly AdminService _admin;
    private readonly ShipmentService _shipments;
    private readonly ApiSettings _api;

    public AdminViewModel(AdminService admin, ShipmentService shipments, IOptions<ApiSettings> api,
                          PagingFactory paging)
    {
        _admin = admin;
        _shipments = shipments;
        _api = api.Value;
        ShopsPager = paging.Create(PagedLists.AdminShops, () => RunAsync(LoadAllShopsAsync));
        UsersPager = paging.Create(PagedLists.AdminUsers, () => RunAsync(LoadUsersAsync));
    }

    /// <summary>Sellers tab → "All shops" pages.</summary>
    public Paging ShopsPager { get; }

    /// <summary>Users tab pages.</summary>
    public Paging UsersPager { get; }

    // ---------- Lists ----------

    public ObservableCollection<AdminShopCard> PendingShops { get; } = new();
    public ObservableCollection<AdminShopCard> Shops { get; } = new();
    public ObservableCollection<DishSuggestionCard> DishSuggestions { get; } = new();
    public ObservableCollection<AdminUserCard> Users { get; } = new();

    public bool HasPendingApprovals => PendingShops.Count > 0;
    public bool HasDishSuggestions => DishSuggestions.Count > 0;

    // ---------- Stat tiles ----------

    [ObservableProperty]
    public partial int TotalVendors { get; set; }

    [ObservableProperty]
    public partial int TotalUsers { get; set; }

    [ObservableProperty]
    public partial int PendingApprovalCount { get; set; }

    [ObservableProperty]
    public partial int BlockedCount { get; set; }

    /// <summary>Every parcel on the server (Shipments tile); 0 until loaded.</summary>
    [ObservableProperty]
    public partial int TotalShipments { get; set; }

    // ---------- Tabs, search, messages ----------

    [ObservableProperty]
    public partial string ActiveTab { get; set; } = "Sellers";

    [ObservableProperty]
    public partial string ShopSearch { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UserSearch { get; set; } = string.Empty;

    /// <summary>Users tab: only people who are suspended.</summary>
    [ObservableProperty]
    public partial bool SuspendedOnly { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsError { get; set; }

    [RelayCommand]
    private void ShowSellers() => ActiveTab = "Sellers";

    [RelayCommand]
    private void ShowUsers() => ActiveTab = "Users";

    [RelayCommand]
    private void ShowShipments() => ActiveTab = "Shipments";

    // ═════════════════════════════ Loading ═════════════════════════════

    /// <summary>Called by the page each time it appears (and by pull to refresh).</summary>
    public async void Reload()
    {
        try { await LoadAllAsync(); }
        catch (Exception) { ShowError("Couldn't load the dashboard. Pull down to try again."); }
    }

    [RelayCommand]
    private async Task Refresh() => await LoadAllAsync();

    private async Task LoadAllAsync()
    {
        IsLoading = true;
        try
        {
            await LoadStatsAsync();
            await LoadShopsAsync();
            await LoadDishSuggestionsAsync();
            await LoadUsersAsync();
            await LoadShipmentCountAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadStatsAsync()
    {
        var stats = await _admin.GetShopStatsAsync();
        if (stats.IsFailure)
        {
            ShowError(stats.Error!.DisplayMessage);
            return;
        }

        TotalVendors = stats.Value!.Total;
        PendingApprovalCount = stats.Value.PendingApproval;
        BlockedCount = stats.Value.Blocked;
    }

    private async Task LoadShopsAsync()
    {
        var pending = await _admin.GetPendingShopsAsync();
        PendingShops.Clear();
        if (pending.IsSuccess)
            foreach (var s in pending.Value!) PendingShops.Add(ToCard(s));
        OnPropertyChanged(nameof(HasPendingApprovals));

        await LoadAllShopsAsync();
    }

    /// <summary>"All shops": the pager's page (shops waiting for approval are listed above it instead).</summary>
    private async Task LoadAllShopsAsync()
    {
        var all = await _admin.GetShopsAsync(ShopSearch, ShopsPager.Page, ShopsPager.PageSize);
        Shops.Clear();
        if (all.IsFailure)
        {
            ShowError(all.Error!.DisplayMessage);
            return;
        }

        ShopsPager.Update(all.Value!.TotalCount);
        foreach (var s in all.Value.Items.Where(s => s.Status != VendorStatus.PendingApproval))
            Shops.Add(ToCard(s));
    }

    private async Task LoadDishSuggestionsAsync()
    {
        var result = await _admin.GetDishSuggestionsAsync();
        DishSuggestions.Clear();
        if (result.IsSuccess)
            foreach (var s in result.Value!) DishSuggestions.Add(new DishSuggestionCard { Suggestion = s });
        OnPropertyChanged(nameof(HasDishSuggestions));
    }

    private async Task LoadUsersAsync()
    {
        var result = await _admin.GetUsersAsync(UserSearch, SuspendedOnly, UsersPager.Page, UsersPager.PageSize);
        if (result.IsFailure)
        {
            ShowError(result.Error!.DisplayMessage);
            return;
        }

        UsersPager.Update(result.Value!.TotalCount);
        Users.Clear();
        foreach (var u in result.Value!.Items)
            Users.Add(new AdminUserCard { User = u, PhotoUrl = _api.ResolveFileUrl(u.PhotoUrl) });
        if (!SuspendedOnly && string.IsNullOrWhiteSpace(UserSearch)) TotalUsers = result.Value.TotalCount;
    }

    private async Task LoadShipmentCountAsync()
    {
        var result = await _shipments.GetAllAsync(status: null, page: 1, pageSize: 1);   // only the total
        if (result.IsSuccess) TotalShipments = result.Value!.TotalCount;
    }

    private AdminShopCard ToCard(AdminShopDto shop) => new()
    {
        Shop = shop,
        LogoUrl = _api.ResolveFileUrl(shop.LogoUrl) ?? Vendor.DefaultLogo
    };

    // ═════════════════════════════ Sellers ═════════════════════════════

    [RelayCommand]
    private async Task SearchShops()
    {
        ShopsPager.Reset();
        await RunAsync(LoadShopsAsync);
    }

    [RelayCommand]
    private async Task ApproveShop(AdminShopCard card) =>
        await DoAsync(() => _admin.ApproveShopAsync(card.Shop.Id), $"{card.Name} is live.");

    [RelayCommand]
    private async Task RejectShop(AdminShopCard card)
    {
        var reason = await Shell.Current.DisplayPromptAsync("Reject shop",
            $"Why can't {card.Name} be approved? The seller sees this and can fix it.", "Reject", "Cancel",
            placeholder: "e.g. The business email doesn't work");
        if (string.IsNullOrWhiteSpace(reason)) return;

        await DoAsync(() => _admin.RejectShopAsync(card.Shop.Id, reason.Trim()), $"{card.Name} was rejected.");
    }

    /// <summary>Block hides only the shop; its owner can still sign in and buy.</summary>
    [RelayCommand]
    private async Task ToggleBlock(AdminShopCard card)
    {
        var block = !card.IsBlocked;
        if (block)
        {
            var ok = await Shell.Current.DisplayAlertAsync("Block this shop?",
                $"{card.Name} will be hidden from buyers. Its owner can still use Phloem as a customer.",
                "Block", "Cancel");
            if (!ok) return;
        }

        await DoAsync(() => _admin.SetBlockedAsync(card.Shop.Id, block),
                      block ? $"{card.Name} is blocked." : $"{card.Name} is unblocked.");
    }

    [RelayCommand]
    private async Task ToggleVerified(AdminShopCard card) =>
        await DoAsync(() => _admin.SetVerifiedAsync(card.Shop.Id, !card.IsVerified),
                      card.IsVerified ? $"✓ removed from {card.Name}." : $"{card.Name} now has the ✓ badge.");

    /// <summary>The shop's details, pictures and documents.</summary>
    [RelayCommand]
    private async Task OpenShop(AdminShopCard card) =>
        await Shell.Current.GoToAsync($"adminshop?id={card.Shop.Id}");

    // ═════════════════════════════ Dish names ═════════════════════════════

    [RelayCommand]
    private async Task ApproveDishName(DishSuggestionCard card) =>
        await DoAsync(() => _admin.ApproveDishNameAsync(card.Suggestion.Id),
                      $"\"{card.Name}\" approved. Set its price range in Dish names & prices so sellers can use it.");

    /// <summary>Sellers tab → Dish names &amp; prices (each name's allowed price per country).</summary>
    [RelayCommand]
    private async Task OpenDishNames() => await Shell.Current.GoToAsync("admindishes");

    [RelayCommand]
    private async Task RejectDishName(DishSuggestionCard card)
    {
        var ok = await Shell.Current.DisplayAlertAsync("Reject dish name?",
            $"\"{card.Name}\" won't be added to the dish list.", "Reject", "Cancel");
        if (!ok) return;

        await DoAsync(() => _admin.RejectDishNameAsync(card.Suggestion.Id), $"\"{card.Name}\" rejected.");
    }

    // ═════════════════════════════ Users ═════════════════════════════

    [RelayCommand]
    private async Task SearchUsers()
    {
        UsersPager.Reset();
        await RunAsync(LoadUsersAsync);
    }

    partial void OnSuspendedOnlyChanged(bool value)
    {
        UsersPager.Reset();
        _ = RunAsync(LoadUsersAsync);
    }

    /// <summary>Suspend: how long, then why (shown to them). Their shop is hidden with them.</summary>
    [RelayCommand]
    private async Task Suspend(AdminUserCard card)
    {
        const string week = "7 days", month = "30 days", open = "Until further notice";
        var length = await Shell.Current.DisplayActionSheetAsync($"Suspend {card.FullName} for…", "Cancel", null,
                                                                 week, month, open);
        DateTime? until = length switch
        {
            week => DateTime.UtcNow.AddDays(7),
            month => DateTime.UtcNow.AddDays(30),
            open => null,
            _ => DateTime.MinValue
        };
        if (until == DateTime.MinValue) return;   // cancelled

        var shopNote = card.HasShop ? " Their shop will be hidden and its unfinished orders cancelled." : string.Empty;
        var reason = await Shell.Current.DisplayPromptAsync("Why?",
            $"{card.FullName} will see this reason.{shopNote}", "Suspend", "Cancel",
            placeholder: "e.g. Repeated fake orders", maxLength: 500);
        if (string.IsNullOrWhiteSpace(reason)) return;

        IsLoading = true;
        try
        {
            var result = await _admin.SuspendAsync(card.User.Id, reason.Trim(), until);
            if (result.IsFailure)
            {
                ShowError(result.Error!.DisplayMessage);
                return;
            }

            var done = result.Value!;
            var extra = done.ShopSuspended
                ? $" Their shop is hidden; {done.CancelledShopOrders} order(s) cancelled ({done.OrdersToRefund} to refund)."
                : string.Empty;
            await LoadAllAsync();
            ShowInfo($"{card.FullName} is suspended.{extra}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LiftSuspension(AdminUserCard card)
    {
        var ok = await Shell.Current.DisplayAlertAsync("Lift suspension?",
            $"{card.FullName} will be able to sign in again" + (card.HasShop ? ", and their shop comes back." : "."),
            "Lift", "Cancel");
        if (!ok) return;

        await DoAsync(() => _admin.ReinstateAsync(card.User.Id), $"{card.FullName} can sign in again.");
    }

    /// <summary>
    /// Roles: lists the roles an admin can give (✓ = they have it); tapping one gives it or takes it away after a
    /// "Sure?". Your own Admin role can't be taken away (another admin must do it); Seller follows the person's shop.
    /// </summary>
    [RelayCommand]
    private async Task ManageRoles(AdminUserCard card)
    {
        var name = card.User.FullName;
        var options = Roles.Assignable.ToDictionary(
            role => (card.Has(role) ? "✓ " : "") + Roles.DisplayName(role) + (card.IsYou && role == Roles.Admin ? " (you)" : ""),
            role => role);
        var picked = await Shell.Current.DisplayActionSheetAsync(
            $"Roles for {name}\n✓ = has it. Tap one to give it or take it away. Seller follows their shop.",
            "Close", null, options.Keys.ToArray());
        if (picked is null || !options.TryGetValue(picked, out var role)) return;

        var display = Roles.DisplayName(role);
        if (card.Has(role))
        {
            if (card.IsYou && role == Roles.Admin)
            {
                await Shell.Current.DisplayAlertAsync("This is your Admin role",
                    "You can't remove your own Admin role, so you can't lock yourself out. Ask another admin to do it.",
                    "OK");
                return;
            }

            var remove = await Shell.Current.DisplayAlertAsync($"Take {display} from {name}?",
                $"{Roles.Description(role)}\n\nThey'll be signed out everywhere so the change applies straight away.",
                "Take it away", "Keep it");
            if (!remove) return;
            await DoAsync(() => _admin.RemoveRoleAsync(card.User.Id, role), $"{name} is no longer {Article(display)}.");
        }
        else
        {
            var give = await Shell.Current.DisplayAlertAsync($"Make {name} {Article(display)}?",
                $"{Roles.Description(role)}" +
                (role == Roles.Admin ? "\n\nAdmins must use two-step sign-in. They'll be asked to turn it on." : string.Empty) +
                "\n\nIt applies the next time they sign in (or within a few minutes).",
                "Give role", "Cancel");
            if (!give) return;
            await DoAsync(() => _admin.GrantRoleAsync(card.User.Id, role), $"{name} is now {Article(display)}.");
        }
    }

    /// <summary>"an Admin", "a Support agent".</summary>
    private static string Article(string roleName) =>
        ("AEIOU".Contains(char.ToUpperInvariant(roleName[0])) ? "an " : "a ") + roleName;

    // ═════════════════════════════ Shipments tab ═════════════════════════════

    /// <summary>Shipments tab → Shipping routes (create Ghana → UK, rates, switch on).</summary>
    [RelayCommand]
    private async Task OpenShippingRoutes() => await Shell.Current.GoToAsync("adminroutes");

    /// <summary>Shipments tab → What can be sent (the list customers see before booking).</summary>
    [RelayCommand]
    private async Task OpenParcelRules() => await Shell.Current.GoToAsync("adminparcelrules");

    /// <summary>Shipments tab → All shipments (receive at hub, next step).</summary>
    [RelayCommand]
    private async Task OpenAllShipments() => await Shell.Current.GoToAsync("adminshipments");

    // ═════════════════════════════ Helpers ═════════════════════════════

    /// <summary>Runs an action on the API; on success reloads everything and shows <paramref name="done"/>.</summary>
    private async Task DoAsync(Func<Task<ApiResult>> action, string done)
    {
        IsLoading = true;
        try
        {
            var result = await action();
            if (result.IsFailure)
            {
                ShowError(result.Error!.DisplayMessage);
                return;
            }

            await LoadAllAsync();
            ShowInfo(done);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RunAsync(Func<Task> load)
    {
        IsLoading = true;
        try { await load(); }
        catch (Exception) { ShowError("Couldn't load. Please try again."); }
        finally { IsLoading = false; }
    }

    private void ShowError(string message)
    {
        IsError = true;
        StatusMessage = message;
    }

    private void ShowInfo(string message)
    {
        IsError = false;
        StatusMessage = message;
    }
}

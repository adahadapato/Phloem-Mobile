using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Models;
using Phloem.Services;
using Phloem.Services.Api.Contracts;

namespace Phloem.ViewModels;

/// <summary>One row in the saved-addresses list.</summary>
public partial class AddressOption : ObservableObject
{
    public required DeliveryAddress Address { get; init; }

    /// <summary>The account holder (their name and phone apply when the address has none).</summary>
    public User? Owner { get; init; }

    /// <summary>"For Grace Mensah · +447700900123": who receives it and who the seller calls.</summary>
    public string RecipientText
    {
        get
        {
            var name = Address.RecipientName ?? Owner?.FullName;
            var phone = Address.PhoneNumber ?? (string.IsNullOrWhiteSpace(Owner?.PhoneNumber) ? null : Owner!.PhoneNumber);
            return string.Join(" · ", new[] { name is null ? null : $"For {name}", phone }.Where(s => !string.IsNullOrWhiteSpace(s)));
        }
    }

    public bool HasRecipient => RecipientText.Length > 0;

    public string Label => string.IsNullOrWhiteSpace(Address.Label) ? "Address" : Address.Label;
    public string Details => string.IsNullOrWhiteSpace(Address.Country)
        ? Address.FullAddress
        : $"{Address.FullAddress}, {Address.Country}";
    public bool IsDefault => Address.IsDefault;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Delivery address page: pick one of the signed-in user's saved addresses or add a new one.
/// </summary>
public partial class DeliveryAddressViewModel : ObservableObject
{
    private readonly SessionService _session;
    private readonly CartService _cart;
    private readonly DeliveryAddressService _delivery;
    private readonly AccountService _account;
    private readonly AuthService _auth;

    public ObservableCollection<AddressOption> Addresses { get; } = new();
    public ObservableCollection<CountryOption> Countries { get; } = new(CountryOption.All);

    public bool IsLoggedIn => _session.IsLoggedIn && _session.CurrentUser is not null;
    public bool IsGuest => !IsLoggedIn;
    public bool HasAddresses => Addresses.Count > 0;
    public string Subtitle => IsLoggedIn
        ? $"Where should we deliver, {_session.CurrentUser!.FullName.Split(' ')[0]}?"
        : "Where should we deliver your order?";

    // ---------- New address form ----------
    [ObservableProperty]
    public partial bool IsAddingNew { get; set; }

    [ObservableProperty]
    public partial string NewLabel { get; set; } = "Home";

    [ObservableProperty]
    public partial string NewAddress { get; set; } = string.Empty;

    /// <summary>Who receives it; starts as the account holder's name.</summary>
    [ObservableProperty]
    public partial string NewRecipientName { get; set; } = string.Empty;

    /// <summary>Who the seller calls; starts as the account holder's phone.</summary>
    [ObservableProperty]
    public partial string NewPhone { get; set; } = string.Empty;

    [ObservableProperty]
    public partial CountryOption? NewCountry { get; set; }

    [ObservableProperty]
    public partial bool SaveAsDefault { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsError { get; set; }

    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    public DeliveryAddressViewModel(SessionService session, CartService cart, DeliveryAddressService delivery,
                                    AccountService account, AuthService auth)
    {
        _session = session;
        _cart = cart;
        _delivery = delivery;
        _account = account;
        _auth = auth;
    }

    /// <summary>Called each time the page appears.</summary>
    public void Load()
    {
        Addresses.Clear();
        StatusMessage = string.Empty;

        var user = IsLoggedIn ? _session.CurrentUser : null;
        var current = _delivery.Resolve(user);

        if (user is not null)
        {
            foreach (var a in user.Addresses.OrderByDescending(a => a.IsDefault))
                Addresses.Add(new AddressOption { Address = a, Owner = user, IsSelected = ReferenceEquals(a, current) });
        }
        else if (current is not null)
        {
            // A guest's one-off address
            Addresses.Add(new AddressOption { Address = current, IsSelected = true });
        }

        if (Addresses.Count > 0 && !Addresses.Any(a => a.IsSelected))
            Addresses[0].IsSelected = true;

        NewCountry ??= Countries.FirstOrDefault(c => c.Name == _cart.SelectedCountry?.Name) ?? Countries.FirstOrDefault();
        ResetRecipient();
        SaveAsDefault = Addresses.Count == 0;
        IsAddingNew = Addresses.Count == 0;   // nothing saved yet → open the form straight away

        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(IsGuest));
        OnPropertyChanged(nameof(HasAddresses));
        OnPropertyChanged(nameof(Subtitle));
    }

    [RelayCommand]
    private void Select(AddressOption option)
    {
        foreach (var a in Addresses) a.IsSelected = ReferenceEquals(a, option);
    }

    [RelayCommand]
    private void ChooseLabel(string label) => NewLabel = label;

    [RelayCommand]
    private void ShowAddForm()
    {
        IsAddingNew = true;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void CancelAdd()
    {
        IsAddingNew = false;
        NewAddress = string.Empty;
        ResetRecipient();
        StatusMessage = string.Empty;
    }

    /// <summary>Name and phone back to the account holder's own.</summary>
    private void ResetRecipient()
    {
        var user = IsLoggedIn ? _session.CurrentUser : null;
        NewRecipientName = user?.FullName ?? string.Empty;
        NewPhone = user?.PhoneNumber ?? string.Empty;
    }

    /// <summary>
    /// Saves the new address: to the account on the API when signed in (orders need a saved address),
    /// or just for this order for a guest.
    /// </summary>
    [RelayCommand]
    private async Task SaveNewAddress()
    {
        if (IsSaving) return;
        if (string.IsNullOrWhiteSpace(NewAddress))
        {
            IsError = true;
            StatusMessage = "Please enter the street, city and postcode.";
            return;
        }

        if (!IsLoggedIn)
        {
            // A guest's one-off address (they sign in before the order is placed)
            _delivery.SelectedAddress = new DeliveryAddress
            {
                Label = NewLabel,
                FullAddress = NewAddress.Trim(),
                Country = NewCountry?.Name ?? _cart.SelectedCountry?.Name ?? string.Empty,
                IsDefault = false
            };
            NewAddress = string.Empty;
            IsError = false;
            Load();
            StatusMessage = "Address added for this order.";
            return;
        }

        var country = NewCountry ?? _cart.SelectedCountry;
        if (country is null)
        {
            IsError = true;
            StatusMessage = "Choose the country.";
            return;
        }

        var me = _session.CurrentUser!;
        if (string.IsNullOrWhiteSpace(NewRecipientName))
        {
            IsError = true;
            StatusMessage = "Please enter the name of the person who will receive the food.";
            return;
        }

        // The account holder's own name / phone are saved as "use mine" (null), so they follow profile changes
        var name = NewRecipientName.Trim();
        var phone = NewPhone.Trim();
        string? recipientName = name == me.FullName ? null : name;
        string? phoneNumber = phone.Length == 0 || phone == me.PhoneNumber ? null : phone;

        IsSaving = true;
        try
        {
            var makeDefault = SaveAsDefault || me.Addresses.Count == 0;
            var saved = await _account.AddAddressAsync(
                new AddAddressCommand(NewLabel, NewAddress.Trim(), country.ApiCountry, makeDefault, recipientName,
                                      phoneNumber));
            if (saved.IsFailure)
            {
                IsError = true;
                StatusMessage = saved.Error!.DisplayMessage;
                return;
            }

            // Reload the profile so the new address (with its Id from the API) is in the list, then pick it
            await _auth.RefreshProfileAsync();
            _delivery.SelectedAddress = _session.CurrentUser?.Addresses.FirstOrDefault(a => a.Id == saved.Value.ToString());

            NewAddress = string.Empty;
            IsError = false;
            Load();   // rebuild the list with the new address selected (and the name / phone reset)
            StatusMessage = "Address saved.";
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>"Deliver to this address": remember the choice and go back.</summary>
    [RelayCommand]
    private async Task UseSelected()
    {
        var chosen = Addresses.FirstOrDefault(a => a.IsSelected);
        if (chosen is null)
        {
            IsError = true;
            StatusMessage = "Choose an address first.";
            return;
        }

        _delivery.SelectedAddress = chosen.Address;
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task GoToLogin() => await Shell.Current.GoToAsync("//account/login");
}

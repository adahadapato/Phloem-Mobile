using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Services;
using Phloem.Services.Biometrics;

namespace Phloem.ViewModels;

/// <summary>
/// My Account (the Account tab). Shows the signed-in customer's details and a Log out button,
/// or Log in / Create account when nobody is signed in.
/// </summary>
/// <remarks>Who is signed in comes from the Phloem API (<see cref="SessionService"/>, filled by
/// <see cref="AuthService"/>); Log out signs out on the server too.</remarks>
public partial class AccountViewModel : ObservableObject
{
    private readonly SessionService _session;
    private readonly AuthService _auth;
    private readonly OrderService _orders;
    private readonly ShipmentService _shipments;
    private readonly DeliveryAddressService _delivery;
    private readonly ScreenAwakeService _awake;
    private readonly BiometricSignIn _biometrics;

    public AccountViewModel(SessionService session, AuthService auth, OrderService orders, ShipmentService shipments,
                            DeliveryAddressService delivery, ScreenAwakeService awake, BiometricSignIn biometrics)
    {
        _biometrics = biometrics;
        _session = session;
        _auth = auth;
        _orders = orders;
        _shipments = shipments;
        _delivery = delivery;
        _awake = awake;
    }

    /// <summary>
    /// "Keep screen on": the phone won't dim or lock while an order, delivery, tracking or checkout screen is open.
    /// Saved on this phone.
    /// </summary>
    public bool KeepScreenOn
    {
        get => _awake.IsEnabled;
        set
        {
            if (_awake.IsEnabled == value) return;
            _awake.IsEnabled = value;
            OnPropertyChanged();
        }
    }

    // ---------- Sign in with Face ID / fingerprint (saved on this phone) ----------
    // Checked fresh each time the page appears and when the person comes back from the phone's settings, so a
    // fingerprint set up in the meantime is picked up straight away.

    private BiometricStatus _biometricStatus = BiometricStatus.NoSensor;
    private bool _biometricsOn;

    /// <summary>The row shows for signed-in people on phones with a sensor (even if nothing is set up yet).</summary>
    public bool ShowBiometricsRow => IsLoggedIn && _biometricStatus != BiometricStatus.NoSensor;

    /// <summary>The switch works only when a fingerprint / face is set up on the phone.</summary>
    public bool CanToggleBiometrics => _biometricStatus == BiometricStatus.Available;

    /// <summary>"Sign in with Face ID", "Sign in with fingerprint"…</summary>
    public string BiometricsTitle => $"Sign in with {_biometrics.DisplayName}";

    /// <summary>The line under the title: what it does, or what to do first.</summary>
    public string BiometricsSubtitle => _biometricStatus == BiometricStatus.Available
        ? "Open Phloem with a scan instead of your password"
        : _biometrics.CanOpenSetup
            ? $"Set up a {_biometrics.DisplayName} on your phone first. Tap here"
            : $"Set up {_biometrics.DisplayName} in your phone's Settings first";

    /// <summary>
    /// The switch. Turning it on asks for one scan to confirm; if that's cancelled or fails it flips back off.
    /// </summary>
    public bool BiometricsOn
    {
        get => _biometricsOn;
        set
        {
            if (_biometricsOn == value) return;
            _biometricsOn = value;
            OnPropertyChanged();
            _ = SetBiometricsAsync(value);
        }
    }

    /// <summary>Switches quick sign-in on (with a scan) or off.</summary>
    private async Task SetBiometricsAsync(bool on)
    {
        if (!on)
        {
            _biometrics.TurnOff();
            return;
        }

        if (_biometrics.IsOnFor(_session.Profile?.Id)) return;

        var outcome = await _biometrics.TurnOnAsync();
        if (outcome == BiometricOutcome.Success) return;

        _biometricsOn = false;
        OnPropertyChanged(nameof(BiometricsOn));
        if (outcome == BiometricOutcome.Failed)
            await Shell.Current.DisplayAlertAsync("Not switched on",
                $"We couldn't switch on {_biometrics.DisplayName} sign-in. Please try again.", "OK");
    }

    /// <summary>Tapping the row when nothing is set up yet: opens the phone's "add a fingerprint" screen (Android),
    /// and checks again when the person comes back to Phloem.</summary>
    [RelayCommand]
    private void SetUpBiometrics()
    {
        if (CanToggleBiometrics || !_biometrics.CanOpenSetup) return;

        if (Application.Current?.Windows.FirstOrDefault() is { } window)
        {
            void OnResumed(object? sender, EventArgs e)
            {
                window.Resumed -= OnResumed;
                LoadBiometrics();
            }
            window.Resumed += OnResumed;
        }

        _biometrics.OpenSetup();
    }

    /// <summary>Reads the phone's status and the switch.</summary>
    private void LoadBiometrics()
    {
        _biometricStatus = _biometrics.Status;
        _biometricsOn = IsLoggedIn && _biometrics.IsOnFor(_session.Profile?.Id);
        foreach (var name in new[]
                 {
                     nameof(ShowBiometricsRow), nameof(CanToggleBiometrics), nameof(BiometricsTitle),
                     nameof(BiometricsSubtitle), nameof(BiometricsOn)
                 })
            OnPropertyChanged(name);
    }

    // ---------- Who is signed in ----------
    public bool IsLoggedIn => _session.IsLoggedIn && _session.CurrentUser is not null;
    public bool IsGuest => !IsLoggedIn;

    public string FullName    => _session.CurrentUser?.FullName ?? string.Empty;
    public string Email       => _session.CurrentUser?.Email ?? string.Empty;
    public string PhoneNumber => string.IsNullOrWhiteSpace(_session.CurrentUser?.PhoneNumber)
                                 ? "Not added" : _session.CurrentUser!.PhoneNumber;
    public string Country     => _session.CurrentUser?.Country ?? string.Empty;
    public string MemberSince => _session.CurrentUser is null ? string.Empty
                                 : $"Member since {_session.CurrentUser.JoinedAt:MMMM yyyy}";

    /// <summary>"EO" for Efua Owusu.</summary>
    public string Initials
    {
        get
        {
            var parts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => "?",
                1 => parts[0][..1].ToUpper(),
                _ => $"{parts[0][0]}{parts[^1][0]}".ToUpper()
            };
        }
    }

    /// <summary>Profile photo (set on Edit profile). Initials show when there isn't one.</summary>
    public string? PhotoPath => _session.CurrentUser?.PhotoPath;
    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoPath);
    public bool HasNoPhoto => !HasPhoto;

    public bool IsAdmin => _session.IsAdmin;

    /// <summary>Owners and anyone the backend gave staff permissions: shows the admin / staff entry.</summary>
    public bool IsStaff => _session.IsStaff;

    /// <summary>"Admin dashboard" for owners, "Staff area" for other staff.</summary>
    public string AdminLinkText => IsAdmin ? "Admin dashboard" : "Staff area";

    /// <summary>The line under <see cref="AdminLinkText"/>.</summary>
    public string AdminLinkSubtitle => IsAdmin ? "Approve sellers, users and shipments" : "The parts of Phloem your job role looks after";

    /// <summary>True when this user has opened a shop.</summary>
    public bool IsSeller => _session.HasShop;

    public string RoleText => IsAdmin ? "Admin" : IsStaff ? "Staff" : IsSeller ? "Seller" : "Customer";
    public string SellerLinkText => IsSeller ? "Seller dashboard" : "Become a seller";
    public string SellerLinkSubtitle => IsSeller ? "Your shop, orders and dishes" : "Start selling your food on Phloem";

    // ---------- Summaries ----------
    public string AddressSummary
    {
        get
        {
            var count = _session.CurrentUser?.Addresses.Count ?? 0;
            return count == 0 ? "No saved addresses yet" : $"{count} saved · {_delivery.Describe(_session.CurrentUser)}";
        }
    }

    /// <summary>How many orders I've placed (from the server); 0 until counted.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOrders), nameof(OrdersSummary))]
    public partial int OrdersCount { get; set; }

    /// <summary>How many parcels I've sent (from the server); 0 until counted.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasShipments), nameof(ShipmentsSummary))]
    public partial int ShipmentsCount { get; set; }

    /// <summary>"My orders" shows only once there's an order (the Orders tab is always there).</summary>
    public bool HasOrders => IsLoggedIn && OrdersCount > 0;

    /// <summary>"My shipments" shows only once a parcel has been sent (the menu always has it).</summary>
    public bool HasShipments => IsLoggedIn && ShipmentsCount > 0;

    public string OrdersSummary => OrdersCount == 1 ? "1 order placed" : $"{OrdersCount} orders placed";
    public string ShipmentsSummary => ShipmentsCount == 1 ? "1 parcel sent" : $"{ShipmentsCount} parcels sent";

    public string NotificationsSummary
    {
        get
        {
            var u = _session.CurrentUser;
            if (u is null) return string.Empty;
            var on = new[] { u.NotifyOrderUpdates, u.NotifyShipmentUpdates, u.NotifyOffers, u.NotifyNewDishes }.Count(x => x);
            return on == 0 ? "All off" : on == 4 ? "All on" : $"{on} of 4 on";
        }
    }

    /// <summary>Called by the page each time it appears.</summary>
    public void Load()
    {
        foreach (var name in new[]
        {
            nameof(IsLoggedIn), nameof(IsGuest), nameof(FullName), nameof(Email), nameof(PhoneNumber),
            nameof(Country), nameof(MemberSince), nameof(Initials), nameof(IsAdmin), nameof(IsSeller),
            nameof(IsStaff), nameof(AdminLinkText), nameof(AdminLinkSubtitle),
            nameof(RoleText), nameof(SellerLinkText), nameof(SellerLinkSubtitle),
            nameof(AddressSummary), nameof(OrdersSummary),
            nameof(PhotoPath), nameof(HasPhoto), nameof(HasNoPhoto), nameof(NotificationsSummary),
            nameof(HasOrders), nameof(HasShipments)
        })
            OnPropertyChanged(name);

        LoadBiometrics();
        _ = CountAsync();
    }

    /// <summary>How many orders and parcels I have (one item each is enough: the server returns the total).</summary>
    private async Task CountAsync()
    {
        if (!IsLoggedIn)
        {
            OrdersCount = ShipmentsCount = 0;
            return;
        }

        try
        {
            var orders = _orders.GetMyOrdersAsync(page: 1, pageSize: 1);
            var parcels = _shipments.GetMineAsync(page: 1, pageSize: 1);
            await Task.WhenAll(orders, parcels);
            if (orders.Result.IsSuccess) OrdersCount = orders.Result.Value!.TotalCount;
            if (parcels.Result.IsSuccess) ShipmentsCount = parcels.Result.Value!.TotalCount;
        }
        catch (Exception)
        {
            // Couldn't reach the server: the rows show as they were
        }
    }

    // ---------- Navigation (pages open on top of the Account tab) ----------
    [RelayCommand] private async Task Login()        => await Shell.Current.GoToAsync("login");
    [RelayCommand] private async Task Register()     => await Shell.Current.GoToAsync("register");
    [RelayCommand] private async Task MyOrders()     => await Shell.Current.GoToAsync("//orders");
    [RelayCommand] private async Task MyShipments()  => await Shell.Current.GoToAsync("myshipments");
    [RelayCommand] private async Task Addresses()    => await Shell.Current.GoToAsync("address");
    [RelayCommand] private async Task SellerArea()   => await Shell.Current.GoToAsync(IsSeller ? "dashboard" : "sellersetup");
    [RelayCommand] private async Task AdminArea()    => await Shell.Current.GoToAsync("admin");
    [RelayCommand] private async Task SendToUk()     => await Shell.Current.GoToAsync("international");
    [RelayCommand] private async Task About()        => await Shell.Current.GoToAsync("about");

    // ---------- Profile settings (NEW) ----------
    [RelayCommand] private async Task EditProfile()    => await Shell.Current.GoToAsync("editprofile");
    [RelayCommand] private async Task ChangePassword() => await Shell.Current.GoToAsync("changepassword");
    [RelayCommand] private async Task Notifications()  => await Shell.Current.GoToAsync("notifications");

    /// <summary>Log out: asks first, clears the session and the chosen address, then shows the guest view.</summary>
    [RelayCommand]
    private async Task Logout()
    {
        var quick = _biometrics.IsOnFor(_session.Profile?.Id);
        var ok = await Shell.Current.DisplayAlertAsync("Log out?",
            quick
                ? $"You're signed in as {Email}. Next time you can sign back in with your {_biometrics.DisplayName}."
                : $"You're signed in as {Email}.",
            "Log out", "Stay signed in");
        if (!ok) return;

        // With quick sign-in on this only locks (the scan unlocks it again); otherwise it cancels the sign-in on the
        // server and on this device
        await _auth.LogoutAsync();
        _delivery.SelectedAddress = null;
        Load();
    }

    /// <summary>
    /// Delete account. Not available yet: the Phloem API doesn't offer it yet (it's on the backend list:
    /// anonymise the login and close the profile and shop in one go). Until then, tell the user how to ask for it.
    /// </summary>
    [RelayCommand]
    private async Task DeleteAccount()
    {
        if (_session.CurrentUser is null) return;

        await Shell.Current.DisplayAlertAsync("Delete your account",
            "Deleting your account from the app is coming soon. To delete it now, email support@phloemmarket.com " +
            "from your account's email address and we'll do it for you.", "OK");
    }
}

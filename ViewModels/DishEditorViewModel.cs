using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using Phloem.Services;
using Phloem.Services.Api;
using Phloem.Services.Api.Contracts.Commands;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;
using Phloem.Settings;

namespace Phloem.ViewModels;

/// <summary>One group in the dish picker, e.g. "🇬🇭 Ghana" with its dishes.</summary>
public class DishGroup : List<CatalogDish>
{
    public string Name { get; }
    public string Icon { get; }
    public DishGroup(string name, string icon, IEnumerable<CatalogDish> dishes) : base(dishes)
    {
        Name = name;
        Icon = icon;
    }
}

/// <summary>
/// Add / edit a dish (Seller dashboard), on the API. The name comes from the dish list (with the allowed price range
/// in the shop's country), plus an optional detail ("&amp; Chicken"); the photo from the camera or gallery.
///   Add:  GoToAsync("dishedit")
///   Edit: GoToAsync("dishedit", new Dictionary&lt;string, object&gt; { ["MyDish"] = myDishDto })
/// </summary>
public partial class DishEditorViewModel : ObservableObject, IQueryAttributable
{
    private readonly SellerService _seller;
    private readonly DishCatalogService _catalog;
    private readonly ApiSettings _api;

    private MyDishDto? _editing;          // null = adding a new dish
    private MyShopDto? _shop;
    private FileResult? _pickedPhoto;     // a new photo to upload on Save
    private Guid? _existingPhotoId;       // the dish's current photo (edit)
    private bool _removeExistingPhoto;
    private bool _saving;

    public DishEditorViewModel(SellerService seller, DishCatalogService catalog, IOptions<ApiSettings> api)
    {
        _seller = seller;
        _catalog = catalog;
        _api = api.Value;
    }

    // ---------- Page state ----------
    public bool IsEditing => _editing is not null;
    public string PageTitle => IsEditing ? "Edit dish" : "Add a dish";
    public string PageSubtitle => _shop is null ? "Your shop" : $"For {_shop.Name}";

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>Quick detail chips under the dish name.</summary>
    public IReadOnlyList<string> DetailChips { get; } = ["& Chicken", "& Fish", "& Beef", "& Goat", "Spicy", "Vegetarian", "Large"];

    // ---------- Name (from the list) ----------
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBaseDish), nameof(FullName), nameof(CategoryText), nameof(PreviewName),
                              nameof(PriceRangeText))]
    public partial CatalogDish? BaseDish { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullName), nameof(PreviewName))]
    public partial string Detail { get; set; } = string.Empty;

    public bool HasBaseDish => BaseDish is not null;
    public string FullName => BaseDish is null ? string.Empty
                              : string.IsNullOrWhiteSpace(Detail) ? BaseDish.Name : $"{BaseDish.Name} {Detail.Trim()}";
    public string CategoryText => BaseDish?.Category ?? string.Empty;

    // ---------- Photo ----------
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhoto), nameof(HasNoPhoto), nameof(PreviewImage))]
    public partial string? PhotoPath { get; set; }

    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoPath);
    public bool HasNoPhoto => !HasPhoto;

    // ---------- Price, description, availability ----------
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewPrice))]
    public partial string PriceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsAvailable { get; set; } = true;

    /// <summary>The shop's currency symbol, e.g. "GH₵".</summary>
    public string CurrencySymbol => _shop is null ? string.Empty : OrderService.SymbolOf(_shop.Currency);

    /// <summary>"Allowed: GH₵40 – GH₵90" for the chosen name.</summary>
    public string PriceRangeText => BaseDish is null
        ? "Choose the dish name to see the allowed price."
        : BaseDish.HasPriceRange
            ? $"Allowed: {BaseDish.RangeText}"
            : "Phloem hasn't set a price range for this name yet, so it can't be sold yet.";

    // ---------- Live preview card ----------
    public string PreviewName => HasBaseDish ? FullName : "Your dish name";
    public string PreviewPrice => $"{CurrencySymbol}{(TryGetPrice(out var p) ? p : 0m):0.00}";
    public string PreviewVendor => _shop?.Name ?? "Your shop";
    public string PreviewImage => HasPhoto ? PhotoPath! : "hero_food_photo.png";

    // ---------- Status banner ----------
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsError { get; set; }

    // ---------- Dish picker (overlay) ----------
    public ObservableCollection<DishGroup> PickerGroups { get; } = new();

    [ObservableProperty]
    public partial bool IsPickerOpen { get; set; }

    [ObservableProperty]
    public partial string PickerSearch { get; set; } = string.Empty;

    partial void OnPickerSearchChanged(string value) => BuildPickerGroups();

    // "Can't find your dish?" → suggest a new name
    [ObservableProperty]
    public partial bool IsSuggesting { get; set; }

    [ObservableProperty]
    public partial string SuggestName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SuggestCategory { get; set; } = "Local Meals";

    // =================================================================

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _editing = query.TryGetValue("MyDish", out var value) && value is MyDishDto dish ? dish : null;
        query.Clear();

        _pickedPhoto = null;
        _removeExistingPhoto = false;
        _existingPhotoId = _editing?.Photos.FirstOrDefault()?.Id;
        BaseDish = null;
        Detail = _editing?.Detail ?? string.Empty;
        PriceText = _editing?.Price.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty;
        Description = _editing?.Description ?? string.Empty;
        IsAvailable = _editing?.IsAvailable ?? true;
        PhotoPath = _api.ResolveFileUrl(_editing?.Photos.FirstOrDefault()?.Url);
        StatusMessage = string.Empty;
        IsError = false;

        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(PageTitle));
    }

    /// <summary>Called when the page appears: my shop (country, currency) and the dish list with its prices.</summary>
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var shop = await _seller.GetMyShopAsync();
            if (shop.IsFailure)
            {
                ShowError(shop.Error!.Status == 404
                    ? "Open your shop first (Become a Seller)."
                    : shop.Error.DisplayMessage);
                return;
            }

            _shop = shop.Value;
            foreach (var name in new[] { nameof(PageSubtitle), nameof(CurrencySymbol), nameof(PreviewPrice),
                                         nameof(PreviewVendor) })
                OnPropertyChanged(name);

            var problem = await _catalog.LoadAsync(_shop!.Country);
            if (problem is not null)
            {
                ShowError(problem);
                return;
            }

            BuildPickerGroups();
            if (_editing is not null && BaseDish is null)
                BaseDish = _catalog.Dishes.FirstOrDefault(d => d.Id == _editing.DishCatalogId);

            if (_editing?.NeedsPriceUpdate == true)
                ShowError($"Phloem changed the allowed price for {_editing.CatalogName}. Please set a new price.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void BuildPickerGroups()
    {
        var search = PickerSearch?.Trim() ?? string.Empty;
        var myGroup = _shop is null ? (CatalogGroup?)null : DishCatalogService.GroupFor(_shop.Country);

        var groups = _catalog.Dishes
            .Where(d => search.Length == 0 || d.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .GroupBy(d => d.ApiGroup)
            .OrderByDescending(g => g.Key == myGroup)   // the seller's own country first
            .ThenBy(g => g.Key)
            .Select(g => new DishGroup(g.First().Group, g.First().GroupIcon,
                                       g.OrderByDescending(d => d.HasPriceRange).ThenBy(d => d.Name)));

        PickerGroups.Clear();
        foreach (var g in groups) PickerGroups.Add(g);
    }

    /// <summary>The typed price ("8.50", "8,50", with or without the currency symbol); false when there's none.</summary>
    private bool TryGetPrice(out decimal price)
    {
        var text = PriceText ?? string.Empty;
        if (CurrencySymbol.Length > 0) text = text.Replace(CurrencySymbol, string.Empty);
        price = InternationalDeliveryViewModel.ParseDecimal(text.Trim()) ?? 0m;
        return price > 0;
    }

    // ---------- Picker ----------
    [RelayCommand]
    private void OpenPicker()
    {
        PickerSearch = string.Empty;
        IsSuggesting = false;
        BuildPickerGroups();
        IsPickerOpen = true;
    }

    [RelayCommand]
    private void ClosePicker() => IsPickerOpen = false;

    [RelayCommand]
    private void PickDish(CatalogDish dish)
    {
        BaseDish = dish;
        IsPickerOpen = false;
        if (IsError) { IsError = false; StatusMessage = string.Empty; }
    }

    [RelayCommand]
    private void ToggleDetail(string chip) => Detail = Detail == chip ? string.Empty : chip;

    // ---------- Suggest a new name ----------
    [RelayCommand]
    private void StartSuggest()
    {
        SuggestName = PickerSearch;
        IsSuggesting = true;
    }

    [RelayCommand]
    private void ChooseSuggestCategory(string category) => SuggestCategory = category;

    [RelayCommand]
    private async Task SendSuggestion()
    {
        if (string.IsNullOrWhiteSpace(SuggestName))
        {
            await Shell.Current.DisplayAlertAsync("Dish name", "Please type the name of your dish.", "OK");
            return;
        }

        var category = SuggestCategory switch
        {
            "Snacks" => DishCategory.Snacks,
            "Drinks" => DishCategory.Drinks,
            _ => DishCategory.LocalMeals
        };
        var group = category switch
        {
            DishCategory.Snacks => CatalogGroup.Snacks,
            DishCategory.Drinks => CatalogGroup.Drinks,
            _ => _shop is null ? CatalogGroup.WestAfrican : DishCatalogService.GroupFor(_shop.Country)
        };

        var result = await _catalog.SuggestAsync(SuggestName, category, group);
        if (result.IsFailure)
        {
            await Shell.Current.DisplayAlertAsync("Couldn't send", result.Error!.DisplayMessage, "OK");
            return;
        }

        IsSuggesting = false;
        IsPickerOpen = false;
        IsError = false;
        StatusMessage = $"Thanks! \"{SuggestName.Trim()}\" was sent to Phloem for approval. " +
                        "Once it's approved and priced you'll find it in the dish list.";
        SuggestName = string.Empty;
    }

    // ---------- Photo ----------
    [RelayCommand]
    private async Task TakePhoto()
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await Shell.Current.DisplayAlertAsync("Camera", "This device can't take photos. Choose one from your gallery instead.", "OK");
                return;
            }
            UsePhoto(await MediaPicker.Default.CapturePhotoAsync());
        }
        catch (PermissionException)
        {
            await Shell.Current.DisplayAlertAsync("Camera permission", "Please allow Phloem to use the camera in your phone settings.", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Couldn't take photo", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task ChoosePhoto()
    {
        try
        {
            UsePhoto((await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = 1 }))?.FirstOrDefault());
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Couldn't open gallery", ex.Message, "OK");
        }
    }

    /// <summary>A new photo replaces the current one (uploaded on Save).</summary>
    private void UsePhoto(FileResult? photo)
    {
        if (photo is null) return;
        _pickedPhoto = photo;
        if (_existingPhotoId is not null) _removeExistingPhoto = true;
        PhotoPath = photo.FullPath;
    }

    [RelayCommand]
    private async Task PhotoOptions()
    {
        var choice = await Shell.Current.DisplayActionSheetAsync("Dish photo", "Cancel", null,
            "📷  Take a photo", "🖼  Choose from gallery");
        if (choice?.Contains("Take") == true) await TakePhoto();
        else if (choice?.Contains("gallery") == true) await ChoosePhoto();
    }

    [RelayCommand]
    private void RemovePhoto()
    {
        _pickedPhoto = null;
        if (_existingPhotoId is not null) _removeExistingPhoto = true;
        PhotoPath = null;
    }

    // ---------- Save / delete ----------
    [RelayCommand]
    private async Task Save()
    {
        if (_saving) return;
        if (_shop is null)
        {
            ShowError("Open your shop first (Become a Seller).");
            return;
        }
        if (BaseDish is null)
        {
            ShowError("Please choose a dish name from the list.");
            return;
        }
        if (!BaseDish.HasPriceRange)
        {
            ShowError($"{BaseDish.Name} can't be sold yet: Phloem hasn't set its price range. Please choose another name.");
            return;
        }
        if (!TryGetPrice(out var price) || price <= 0)
        {
            ShowError($"Please enter a price, e.g. {BaseDish.MinPrice:0.##}");
            return;
        }
        if (price < BaseDish.MinPrice || price > BaseDish.MaxPrice)
        {
            ShowError($"The price for {BaseDish.Name} must be between {BaseDish.RangeText}.");
            return;
        }
        if (!HasPhoto)
        {
            var go = await Shell.Current.DisplayAlertAsync("No photo yet",
                "Dishes with a photo sell much better. Save without a photo?", "Save anyway", "Add photo");
            if (!go) { await PhotoOptions(); return; }
        }

        _saving = true;
        IsLoading = true;
        try
        {
            var detail = string.IsNullOrWhiteSpace(Detail) ? null : Detail.Trim();
            var description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();

            Guid dishId;
            if (_editing is null)
            {
                var created = await _seller.CreateDishAsync(
                    new CreateDishCommand(BaseDish.Id, detail, price, description, IsAvailable));
                if (created.IsFailure)
                {
                    ShowError(created.Error!.DisplayMessage);
                    if (created.Error.Code == AppNav.TermsNotAcceptedCode) await AppNav.OpenTermsUpdateAsync();
                    return;
                }
                dishId = created.Value;
            }
            else
            {
                dishId = _editing.Id;
                var updated = await _seller.UpdateDishAsync(dishId,
                    new DishRequest(BaseDish.Id, detail, price, description, IsAvailable));
                if (updated.IsFailure)
                {
                    ShowError(updated.Error!.DisplayMessage);
                    return;
                }
            }

            // The photo: remove the old one if it was replaced or removed, then upload the new one
            if (_removeExistingPhoto && _existingPhotoId is { } oldPhoto)
                await _seller.RemoveDishPhotoAsync(dishId, oldPhoto);

            if (_pickedPhoto is not null)
            {
                await using var stream = await _pickedPhoto.OpenReadAsync();
                var contentType = string.IsNullOrWhiteSpace(_pickedPhoto.ContentType) ? "image/jpeg" : _pickedPhoto.ContentType;
                var photo = await _seller.AddDishPhotoAsync(dishId, stream, _pickedPhoto.FileName, contentType);
                if (photo.IsFailure)
                {
                    await Shell.Current.DisplayAlertAsync("Dish saved, photo not",
                        $"The dish was saved, but its photo couldn't be: {photo.Error!.DisplayMessage}", "OK");
                }
            }

            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            _saving = false;
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (_editing is null) return;
        var ok = await Shell.Current.DisplayAlertAsync("Delete this dish?",
            $"{_editing.Name} will be removed from your shop. Past orders still show it.", "Delete", "Keep");
        if (!ok) return;

        var result = await _seller.DeleteDishAsync(_editing.Id);
        if (result.IsFailure)
        {
            ShowError(result.Error!.DisplayMessage);
            return;
        }

        await Shell.Current.GoToAsync("..");
    }

    private void ShowError(string message)
    {
        IsError = true;
        StatusMessage = message;
    }
}

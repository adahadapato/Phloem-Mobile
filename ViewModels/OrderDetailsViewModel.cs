using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Services;
using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.ViewModels;

/// <summary>
/// Order details page. Receives the order via navigation:
///   GoToAsync("orderdetails", new Dictionary&lt;string, object&gt; { ["Order"] = order })
/// </summary>
public partial class OrderDetailsViewModel : ObservableObject, IQueryAttributable
{
    private readonly OrderHistoryService _history;
    private readonly CartService _cart;

    [ObservableProperty]
    public partial PlacedOrder? Order { get; set; }

    public OrderDetailsViewModel(OrderHistoryService history, CartService cart, ReviewService reviews)
    {
        _history = history;
        _cart = cart;
        Rate = new RateCardViewModel(reviews);
    }

    /// <summary>"How was your food?" once delivered.</summary>
    public RateCardViewModel Rate { get; }

    /// <summary>Shown while the order's dishes are loading, or why they couldn't be.</summary>
    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Order", out var value) && value is PlacedOrder order)
        {
            order.RefreshTiming();
            Order = order;               // the row from the list, straight away
            LoadDetails(order);          // then the full order (dishes, fees, address) from the server
        }
    }

    /// <summary>Replaces the list row with the full order from the API.</summary>
    private async void LoadDetails(PlacedOrder row)
    {
        Message = "Loading the order…";
        try
        {
            var (order, error) = await _history.LoadDetailsAsync(row.OrderNumber);
            if (Order != row) return;   // another order was opened meanwhile

            if (order is null)
            {
                Message = error ?? string.Empty;
                return;
            }

            Order = order;
            Message = string.Empty;
            await Rate.LoadAsync(ReviewKind.Order, order.OrderNumber, order.Status == OrderStatus.Delivered,
                                 $"How was your food from {order.ShopName}?");
        }
        catch (Exception)
        {
            Message = "We couldn't load this order. Please try again.";
        }
    }

    /// <summary>Re-check the cancel window each time the page appears.</summary>
    public void Refresh() => Order?.RefreshTiming();

    [RelayCommand]
    private async Task CancelOrder()
    {
        if (Order is not null && await _history.CancelAsync(Order))
            LoadDetails(Order);   // the server's view after cancelling (refund, times)
    }

    [RelayCommand]
    private async Task BuyAgain()
    {
        if (Order is not null)
            await _history.BuyAgainAsync(Order, _cart);
    }
}

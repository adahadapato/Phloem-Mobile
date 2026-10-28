using Phloem.Services;
using Phloem.Services.Api.Contracts.Dtos;

namespace Phloem.Models;

/// <summary>One order card in the Seller dashboard lists (waiting, being prepared, out for delivery).</summary>
public sealed class ShopOrderCard
{
    public required ShopOrderSummaryDto Order { get; init; }

    public string OrderNumber => Order.OrderNumber;
    public string CustomerName => Order.CustomerName;
    public string ItemsSummary => Order.ItemsSummary;
    public string TotalText => $"{OrderService.SymbolOf(Order.Currency)}{Order.Total:N2}";

    /// <summary>"Paid 14:05 · As soon as possible", and who's delivering it once it's sent out.</summary>
    public string WhenText
    {
        get
        {
            var when = Order.PaidAt is { } paid
                ? $"Paid {paid.ToLocalTime():HH:mm} · {DeliveryTimeOption.TextOf(Order.DeliveryWindow)}"
                : DeliveryTimeOption.TextOf(Order.DeliveryWindow);
            return Order.CourierName is null ? when
                 : Order.DeliveringMyself ? $"{when} · 🛵 you"
                 : $"{when} · 🛵 {Order.CourierName}";
        }
    }

    /// <summary>"Home · 31 Camden High St, London", with "For Grace Mensah" first when it's for someone else.</summary>
    public string AddressText
    {
        get
        {
            var where = string.IsNullOrWhiteSpace(Order.DeliveryLabel)
                ? Order.DeliveryAddress
                : $"{Order.DeliveryLabel} · {Order.DeliveryAddress}";
            var forSomeoneElse = Order.DeliveryRecipientName is { } name && name != Order.CustomerName;
            return forSomeoneElse ? $"For {Order.DeliveryRecipientName} · {where}" : where;
        }
    }
}

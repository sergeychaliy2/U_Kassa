using System.Linq;
using UKassaDemo.Domain;
using UKassaDemo.Payments;

namespace UKassaDemo.Application
{
    /// <summary>
    /// Maps domain cart snapshot into a YooKassa backend request DTO.
    /// </summary>
    public sealed class PaymentRequestMapper
    {
        public PaymentCreateRequest Map(OrderId orderId, string returnUrl, Cart cart)
        {
            var lines = cart.GetLines();
            var mappedItems = lines
                .Select(l => new PaymentItemRequest(l.Sku, l.Title, l.Quantity, l.UnitPriceRub))
                .ToList();

            return new PaymentCreateRequest(orderId.Value, mappedItems, cart.TotalRub, returnUrl);
        }
    }
}


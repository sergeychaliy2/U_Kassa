using System;

namespace UKassaDemo.Domain
{
    /// <summary>
    /// Immutable representation of a catalog product used to build a cart and payment.
    /// </summary>
    [Serializable]
    public sealed class CatalogProduct
    {
        public string Sku { get; }
        public string Title { get; }
        public int UnitPriceRub { get; }

        public CatalogProduct(string sku, string title, int unitPriceRub)
        {
            if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("sku is required", nameof(sku));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("title is required", nameof(title));
            if (unitPriceRub <= 0) throw new ArgumentOutOfRangeException(nameof(unitPriceRub));

            Sku = sku;
            Title = title;
            UnitPriceRub = unitPriceRub;
        }
    }
}


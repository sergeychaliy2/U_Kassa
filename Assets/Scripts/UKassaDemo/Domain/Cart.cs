using System;
using System.Collections.Generic;
using System.Linq;

namespace UKassaDemo.Domain
{
    /// <summary>
    /// Domain cart with quantity management and total calculation.
    /// </summary>
    public sealed class Cart
    {
        #pragma region Fields
        private readonly IReadOnlyDictionary<string, CatalogProduct> _catalogBySku;
        private readonly Dictionary<string, int> _quantitiesBySku = new();
        private int _totalRub;
        #pragma endregion

        #pragma region Constructor
        public Cart(IReadOnlyCollection<CatalogProduct> products)
        {
            if (products == null) throw new ArgumentNullException(nameof(products));

            _catalogBySku = products.ToDictionary(p => p.Sku, p => p, StringComparer.Ordinal);
            foreach (var sku in _catalogBySku.Keys)
            {
                _quantitiesBySku[sku] = 0;
            }
        }
        #pragma endregion

        #pragma region Public API
        public IReadOnlyDictionary<string, int> QuantitiesBySku => _quantitiesBySku;
        public int TotalRub => _totalRub;

        public void ChangeQuantity(string sku, int delta)
        {
            if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("sku is required", nameof(sku));
            if (delta == 0) return;

            if (!_catalogBySku.TryGetValue(sku, out var product))
            {
                // Unknown sku - ignore to keep cart consistent.
                return;
            }

            var current = _quantitiesBySku[sku];
            var next = Math.Max(0, current + delta);
            if (next == current) return;

            _quantitiesBySku[sku] = next;
            RecalculateTotal();
        }

        public IReadOnlyList<CartLine> GetLines()
        {
            var lines = new List<CartLine>();
            foreach (var kvp in _quantitiesBySku)
            {
                var sku = kvp.Key;
                var qty = kvp.Value;
                if (qty <= 0) continue;

                var product = _catalogBySku[sku];
                lines.Add(new CartLine(product.Sku, product.Title, qty, product.UnitPriceRub));
            }

            return lines;
        }
        #pragma endregion
        #pragma region Private Helpers
        private void RecalculateTotal()
        {
            var total = 0;
            foreach (var kvp in _quantitiesBySku)
            {
                var sku = kvp.Key;
                var qty = kvp.Value;
                if (qty <= 0) continue;

                total += qty * _catalogBySku[sku].UnitPriceRub;
            }

            _totalRub = total;
        }
        #pragma endregion
    }

    /// <summary>
    /// Immutable line item used to build payment requests.
    /// </summary>
    public readonly struct CartLine
    {
        public string Sku { get; }
        public string Title { get; }
        public int Quantity { get; }
        public int UnitPriceRub { get; }

        public CartLine(string sku, string title, int quantity, int unitPriceRub)
        {
            Sku = sku;
            Title = title;
            Quantity = quantity;
            UnitPriceRub = unitPriceRub;
        }
    }
}


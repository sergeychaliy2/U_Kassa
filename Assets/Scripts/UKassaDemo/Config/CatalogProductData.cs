using System;
using UnityEngine;
using UKassaDemo.Domain;

namespace UKassaDemo.Config
{
    /// <summary>
    /// Serializable catalog entry for editor/config usage.
    /// </summary>
    [Serializable]
    public sealed class CatalogProductData
    {
        [SerializeField] private string sku;
        [SerializeField] private string title;
        [SerializeField] private int unitPriceRub;

        public string Sku => sku;
        public string Title => title;
        public int UnitPriceRub => unitPriceRub;

        public CatalogProductData(string sku, string title, int unitPriceRub)
        {
            this.sku = sku;
            this.title = title;
            this.unitPriceRub = unitPriceRub;
        }

        // Unity serialization
        public CatalogProductData() { }

        public CatalogProduct ToDomainProduct()
        {
            return new CatalogProduct(sku, title, unitPriceRub);
        }
    }
}


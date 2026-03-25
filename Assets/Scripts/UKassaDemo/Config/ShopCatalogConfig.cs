using System.Collections.Generic;
using UnityEngine;

namespace UKassaDemo.Config
{
    /// <summary>
    /// Data container for shop catalog (demo).
    /// </summary>
    [CreateAssetMenu(menuName = "UKassa Demo/Shop Catalog Config")]
    public sealed class ShopCatalogConfig : ScriptableObject
    {
        [SerializeField] private List<CatalogProductData> products = new()
        {
            new CatalogProductData("item_01", "Товар 1", 1200)
        };

        public IReadOnlyList<CatalogProductData> Products => products;
    }
}


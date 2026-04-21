using System.Collections.Generic;
using System.Linq;
using UKassaDemo.Config;
using UKassaDemo.Domain;
using UKassaDemo.Payments;
using UnityEngine;

namespace UKassaDemo.Application
{
    /// <summary>
    /// Composition root for the demo: assembles the object graph
    /// (catalog → cart → gateway → mapper → ViewModel) from configuration.
    /// Kept as a plain C# class so that presenters don't have to know
    /// about concrete implementations.
    /// </summary>
    public static class ShopDemoInstaller
    {
        /// <summary>
        /// Aggregates everything the presenter needs.
        /// </summary>
        public sealed class Result
        {
            public ShopDemoViewModel ViewModel { get; }
            public IReadOnlyList<CatalogProduct> Catalog { get; }

            public Result(ShopDemoViewModel viewModel, IReadOnlyList<CatalogProduct> catalog)
            {
                ViewModel = viewModel;
                Catalog = catalog;
            }
        }

        public static Result Install(
            ShopCatalogConfig catalogConfig,
            IReadOnlyList<CatalogProductData> fallbackCatalog,
            UKassaDemoBackendConfig backendConfig,
            MonoBehaviour coroutineRunner)
        {
            var catalog = BuildCatalog(catalogConfig, fallbackCatalog);
            var cart = new Cart(catalog);
            var gateway = BuildGateway(backendConfig, coroutineRunner);
            var mapper = new PaymentRequestMapper();
            var returnUrl = backendConfig != null
                ? backendConfig.ReturnUrl
                : "http://localhost:3000/return";

            var viewModel = new ShopDemoViewModel(cart, gateway, mapper, returnUrl);
            return new Result(viewModel, catalog);
        }

        private static List<CatalogProduct> BuildCatalog(
            ShopCatalogConfig catalogConfig,
            IReadOnlyList<CatalogProductData> fallbackCatalog)
        {
            var entries = catalogConfig != null && catalogConfig.Products.Count > 0
                ? catalogConfig.Products
                : (IReadOnlyList<CatalogProductData>)fallbackCatalog;

            return entries
                .Select(e => e.ToDomainProduct())
                .ToList();
        }

        private static IPaymentGateway BuildGateway(
            UKassaDemoBackendConfig backendConfig,
            MonoBehaviour coroutineRunner)
        {
            var useBackend = backendConfig == null || backendConfig.UseBackendGateway;
            if (!useBackend)
            {
                return new MockPaymentGateway();
            }

            var createEndpoint = backendConfig != null
                ? backendConfig.BackendCreatePaymentEndpoint
                : "http://localhost:3000/api/payments/create";

            var statusEndpoint = backendConfig != null
                ? backendConfig.BackendGetPaymentStatusEndpointTemplate
                : "http://localhost:3000/api/payments/status";

            var clientKey = backendConfig != null
                ? backendConfig.BackendClientKey
                : string.Empty;

            return new BackendPaymentGateway(coroutineRunner, createEndpoint, statusEndpoint, clientKey);
        }
    }
}

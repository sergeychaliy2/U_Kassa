using System.Collections.Generic;
using UKassaDemo.Application;
using UKassaDemo.Config;
using UKassaDemo.Domain;
using UKassaDemo.Payments;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UKassaDemo
{
    /// <summary>
    /// Thin Unity presenter for the demo shop: draws IMGUI windows
    /// and forwards UI actions to the application layer.
    /// The presenter itself does not construct the object graph — that is
    /// delegated to <see cref="ShopDemoInstaller"/>.
    /// </summary>
    public sealed class UKassaShopDemoController : MonoBehaviour
    {
        #region Fields
        [Header("Presenter UI")]
        [SerializeField, Range(0.55f, 1.15f)] private float uiScale = 0.9f;

        [SerializeField] private bool verboseLogs = true;

        [Header("Config (Data-Driven)")]
        [SerializeField] private ShopCatalogConfig catalogConfig;
        [SerializeField] private UKassaDemoBackendConfig backendConfig;

        [Header("Fallback Catalog (Demo)")]
        [SerializeField] private List<CatalogProductData> fallbackCatalogProducts = new()
        {
            new CatalogProductData("item_01", "Товар 1", 1200),
            new CatalogProductData("item_02", "Товар 2", 350),
            new CatalogProductData("item_03", "Товар 3", 890),
        };

        private Rect _catalogWindowRect;
        private Rect _paymentWindowRect;
        private Vector2 _catalogScroll;
        private ShopDemoViewModel _viewModel;
        private IReadOnlyList<CatalogProduct> _catalog;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            Log($"Awake() scene={SceneManager.GetActiveScene().name}");
            if (SceneManager.GetActiveScene().name != "UKassa")
            {
                enabled = false;
                Log("Disabled: active scene is not UKassa.");
                return;
            }

            var screenW = Mathf.Max(640, Screen.width);
            var screenH = Mathf.Max(360, Screen.height);

            _catalogWindowRect = new Rect(40f, 40f, screenW * 0.58f, screenH * 0.62f);
            _paymentWindowRect = new Rect(screenW * 0.62f, 40f, screenW * 0.36f, screenH * 0.45f);

            BuildApplication();
            Log("Presenter initialized.");
        }

        private void OnDestroy()
        {
            if (_viewModel != null)
            {
                _viewModel.OnPaymentCreated -= OnPaymentCreated;
                _viewModel.OnPaymentStateChanged -= OnPaymentStateChangedLog;
                _viewModel.Dispose();
                _viewModel = null;
            }
            Log("Presenter destroyed.");
        }

        private void OnGUI()
        {
            if (_viewModel == null)
            {
                return;
            }

            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));

            _catalogWindowRect = GUI.Window(6101, _catalogWindowRect, DrawCatalogWindow, "Каталог товаров");
            _paymentWindowRect = GUI.Window(6102, _paymentWindowRect, DrawPaymentWindow, "Корзина и оплата");

            GUI.matrix = oldMatrix;
        }
        #endregion

        #region Presenter Rendering
        private void DrawCatalogWindow(int windowId)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Выберите количество для каждого товара.");
            GUILayout.Space(6);

            _catalogScroll = GUILayout.BeginScrollView(_catalogScroll, GUILayout.Height(_catalogWindowRect.height * 0.66f));

            for (var i = 0; i < _catalog.Count; i++)
            {
                var p = _catalog[i];
                _viewModel.Cart.QuantitiesBySku.TryGetValue(p.Sku, out var qty);

                GUILayout.BeginHorizontal("box");
                GUILayout.Label(p.Title, GUILayout.Width(260));

                if (GUILayout.Button("-", GUILayout.Width(42)))
                {
                    _viewModel.ChangeQuantity(p.Sku, -1);
                }

                GUILayout.Label(qty.ToString(), GUILayout.Width(42));

                if (GUILayout.Button("+", GUILayout.Width(42)))
                {
                    _viewModel.ChangeQuantity(p.Sku, 1);
                }

                GUILayout.Label($"{p.UnitPriceRub} RUB", GUILayout.Width(120));
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            GUILayout.Space(6);
            GUILayout.Label("Перетаскивай окно за заголовок.");
            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, 10000, 24));
        }

        private void DrawPaymentWindow(int windowId)
        {
            GUILayout.BeginVertical();
            GUILayout.Label($"Итого: {_viewModel.TotalRub} RUB");
            GUILayout.Space(6);
            GUILayout.Label(_viewModel.StatusText, GUILayout.Height(52));
            GUILayout.Space(10);

            GUI.enabled = _viewModel.TotalRub > 0 && _viewModel.PaymentState != PaymentState.Creating;
            if (GUILayout.Button("Оплатить тестово через ЮKassa", GUILayout.Height(40)))
            {
                _viewModel.CreatePayment();
            }
            GUI.enabled = true;

            GUI.enabled = !string.IsNullOrWhiteSpace(_viewModel.LastPaymentId) && _viewModel.PaymentState != PaymentState.Creating;
            if (GUILayout.Button("Проверить статус последнего платежа", GUILayout.Height(34)))
            {
                _viewModel.CheckPaymentStatus();
            }
            GUI.enabled = true;

            GUILayout.Space(6);
            GUILayout.Label("Перетаскивай окно за заголовок.");
            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, 10000, 24));
        }
        #endregion

        #region Application Wiring
        private void BuildApplication()
        {
            var result = ShopDemoInstaller.Install(catalogConfig, fallbackCatalogProducts, backendConfig, this);
            _viewModel = result.ViewModel;
            _catalog = result.Catalog;

            _viewModel.OnPaymentCreated += OnPaymentCreated;
            _viewModel.OnPaymentStateChanged += OnPaymentStateChangedLog;
        }

        private void OnPaymentCreated(PaymentCreateResponse response)
        {
            if (response == null || string.IsNullOrWhiteSpace(response.ConfirmationUrl))
            {
                Log("PaymentCreated: confirmationUrl is empty.");
                return;
            }

            Log($"Opening YooKassa payment confirmation: paymentId={response.PaymentId}");
            UnityEngine.Application.OpenURL(response.ConfirmationUrl);
        }

        private void OnPaymentStateChangedLog(PaymentStateChanged evt)
        {
            Log($"PaymentState changed → state={evt.PaymentState}, message=\"{evt.Message}\"");
        }
        #endregion

        #region Helpers
        private void Log(string message)
        {
            if (!verboseLogs) return;
            Debug.Log($"[UKassaShopDemoController] {message}");
        }
        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UKassaDemo.Payments;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UKassaDemo
{
    public class UKassaShopDemoController : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool verboseLogs = true;
        [SerializeField, Range(0.55f, 1.15f)] private float uiScale = 0.9f;

        [Header("Security/Backend")]
        [SerializeField] private bool useBackendGateway = true;
        [SerializeField] private string backendCreatePaymentEndpoint = "http://localhost:3000/api/payments/create";
        [SerializeField] private string backendGetPaymentStatusEndpointTemplate = "http://localhost:3000/api/payments/status";
        [SerializeField] private string backendClientKey = "";
        [SerializeField] private string returnUrl = "http://localhost:3000/return";

        [Header("Catalog")]
        [SerializeField] private List<Product> products = new()
        {
            new Product("rod_01", "Удочка спиннинг", 3200),
            new Product("reel_01", "Катушка безынерц.", 2800),
            new Product("line_01", "Леска 0.22", 450),
            new Product("lure_01", "Воблер minnow", 780),
            new Product("hooks_01", "Набор крючков", 390),
        };

        private readonly Dictionary<string, int> _quantities = new();
        private IPaymentGateway _paymentGateway;
        private string _statusTextValue = "Статус: готово";
        private string _lastPaymentId;
        private Rect _catalogWindowRect = new(40, 40, 760, 480);
        private Rect _paymentWindowRect = new(830, 40, 420, 290);
        private Vector2 _catalogScroll;

        private void Awake()
        {
            Log($"Awake() scene={SceneManager.GetActiveScene().name}");
            if (SceneManager.GetActiveScene().name != "UKassa")
            {
                enabled = false;
                Log("Disabled: active scene is not UKassa.");
                return;
            }

            SetupGateway();
            foreach (var product in products)
            {
                if (!_quantities.ContainsKey(product.sku))
                {
                    _quantities[product.sku] = 0;
                }
            }

            Log("IMGUI windows are active. Drag windows by title bars.");
        }

        private void SetupGateway()
        {
            _paymentGateway = useBackendGateway
                ? new BackendPaymentGateway(this, backendCreatePaymentEndpoint, backendGetPaymentStatusEndpointTemplate, backendClientKey)
                : new MockPaymentGateway();
            Log($"Gateway selected: {(useBackendGateway ? "BackendPaymentGateway" : "MockPaymentGateway")}");
        }

        private void OnGUI()
        {
            if (SceneManager.GetActiveScene().name != "UKassa")
            {
                return;
            }

            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));

            _catalogWindowRect = GUI.Window(5101, _catalogWindowRect, DrawCatalogWindow, "Товары");
            _paymentWindowRect = GUI.Window(5102, _paymentWindowRect, DrawPaymentWindow, "Корзина и оплата");

            GUI.matrix = oldMatrix;
        }

        private void DrawCatalogWindow(int windowId)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Магазин рыболовных товаров");
            GUILayout.Space(6);

            _catalogScroll = GUILayout.BeginScrollView(_catalogScroll, GUILayout.Height(370));
            foreach (var product in products)
            {
                _quantities.TryGetValue(product.sku, out var qty);
                GUILayout.BeginHorizontal("box");
                GUILayout.Label($"{product.title} ({product.priceRub} RUB)", GUILayout.Width(430));
                if (GUILayout.Button("-", GUILayout.Width(42)))
                {
                    ChangeQty(product.sku, -1);
                }

                GUILayout.Label(qty.ToString(), GUILayout.Width(42));
                if (GUILayout.Button("+", GUILayout.Width(42)))
                {
                    ChangeQty(product.sku, 1);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUILayout.Label("Перетаскивай окно за заголовок.");
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 24));
        }

        private void DrawPaymentWindow(int windowId)
        {
            GUILayout.BeginVertical();
            GUILayout.Label($"Итого: {CalcTotalRub()} RUB");
            GUILayout.Space(6);
            GUILayout.Label(_statusTextValue, GUILayout.Height(52));
            GUILayout.Space(8);

            GUI.enabled = CalcTotalRub() > 0;
            if (GUILayout.Button("Оплатить тестово через ЮKassa", GUILayout.Height(40)))
            {
                HandlePayClicked();
            }

            GUI.enabled = !string.IsNullOrWhiteSpace(_lastPaymentId);
            if (GUILayout.Button("Проверить статус платежа", GUILayout.Height(34)))
            {
                HandleCheckStatusClicked();
            }

            GUI.enabled = true;
            GUILayout.Space(6);
            GUILayout.Label("Перетаскивай окно за заголовок.");
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 24));
        }

        private void ChangeQty(string sku, int delta)
        {
            _quantities.TryGetValue(sku, out var current);
            _quantities[sku] = Mathf.Max(0, current + delta);
        }

        private int CalcTotalRub()
        {
            var total = 0;
            foreach (var p in products)
            {
                _quantities.TryGetValue(p.sku, out var q);
                total += q * p.priceRub;
            }

            return total;
        }

        private void HandlePayClicked()
        {
            var request = BuildPaymentRequest();
            if (request.items.Count == 0)
            {
                _statusTextValue = "Статус: корзина пуста";
                return;
            }

            _statusTextValue = "Статус: создаем платеж...";
            Log($"CreatePayment started. items={request.items.Count}, totalRub={request.totalRub}");

            _paymentGateway.CreatePayment(
                request,
                onSuccess: response =>
                {
                    _statusTextValue = $"Статус: paymentId={response.paymentId}, открываем оплату";
                    _lastPaymentId = response.paymentId;
                    Log($"CreatePayment success. paymentId={response.paymentId}, status={response.status}");
                    Application.OpenURL(response.confirmationUrl);
                },
                onError: error =>
                {
                    _statusTextValue = $"Статус: ошибка - {error}";
                    Log($"CreatePayment error: {error}");
                });
        }

        private PaymentCreateRequest BuildPaymentRequest()
        {
            var items = products
                .Select(p =>
                {
                    _quantities.TryGetValue(p.sku, out var q);
                    return new { Product = p, Quantity = q };
                })
                .Where(x => x.Quantity > 0)
                .Select(x => new PaymentItemRequest
                {
                    sku = x.Product.sku,
                    title = x.Product.title,
                    quantity = x.Quantity,
                    unitPriceRub = x.Product.priceRub
                })
                .ToList();

            return new PaymentCreateRequest
            {
                orderId = $"order_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                returnUrl = returnUrl,
                items = items,
                totalRub = items.Sum(i => i.quantity * i.unitPriceRub)
            };
        }

        private void HandleCheckStatusClicked()
        {
            if (string.IsNullOrWhiteSpace(_lastPaymentId))
            {
                _statusTextValue = "Статус: paymentId еще не создан";
                return;
            }

            _statusTextValue = "Статус: получаем платеж...";
            Log($"GetPaymentStatus started. paymentId={_lastPaymentId}");

            _paymentGateway.GetPaymentStatus(
                _lastPaymentId,
                onSuccess: r =>
                {
                    _statusTextValue = $"Статус: paymentId={r.paymentId}, status={r.status}";
                    Log($"GetPaymentStatus success. paymentId={r.paymentId}, status={r.status}");
                },
                onError: error =>
                {
                    _statusTextValue = $"Статус: ошибка статуса - {error}";
                    Log($"GetPaymentStatus error: {error}");
                });
        }

        private void Log(string message)
        {
            if (!verboseLogs)
            {
                return;
            }

            Debug.Log($"[UKassaShopDemoController] {message}");
        }

        [Serializable]
        public class Product
        {
            public string sku;
            public string title;
            public int priceRub;

            public Product(string sku, string title, int priceRub)
            {
                this.sku = sku;
                this.title = title;
                this.priceRub = priceRub;
            }
        }
    }
}

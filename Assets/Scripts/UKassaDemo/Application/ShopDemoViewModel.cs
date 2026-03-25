using System;
using UKassaDemo.Domain;
using UKassaDemo.Payments;

namespace UKassaDemo.Application
{
    /// <summary>
    /// Application layer state + commands for the shop/payment UI.
    /// </summary>
    public sealed class ShopDemoViewModel
    {
        #pragma region Fields
        private readonly Cart _cart;
        private readonly IPaymentGateway _paymentGateway;
        private readonly PaymentRequestMapper _mapper;
        private readonly string _returnUrl;

        private PaymentState _paymentState = PaymentState.Idle;
        private string _statusText = "Статус: готово";
        private string _lastPaymentId;
        #pragma endregion
        #pragma region Events
        public event Action<PaymentStateChanged> OnPaymentStateChanged;
        public event Action<PaymentCreateResponse> OnPaymentCreated;
        #pragma endregion

        #pragma region Constructor
        public ShopDemoViewModel(Cart cart, IPaymentGateway paymentGateway, PaymentRequestMapper mapper, string returnUrl)
        {
            _cart = cart ?? throw new ArgumentNullException(nameof(cart));
            _paymentGateway = paymentGateway ?? throw new ArgumentNullException(nameof(paymentGateway));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _returnUrl = returnUrl ?? throw new ArgumentNullException(nameof(returnUrl));

            SyncIdleState();
        }
        #pragma endregion

        #pragma region Public Properties
        public int TotalRub => _cart.TotalRub;
        public string StatusText => _statusText;
        public PaymentState PaymentState => _paymentState;

        public Cart Cart => _cart;
        public string LastPaymentId => _lastPaymentId;
        #pragma endregion

        #pragma region Commands
        public void ChangeQuantity(string sku, int delta)
        {
            _cart.ChangeQuantity(sku, delta);
            // keep UI text in sync
            _statusText = _paymentState == PaymentState.Idle || _paymentState == PaymentState.Error
                ? "Статус: готово"
                : _statusText;
        }

        public void CreatePayment()
        {
            if (_cart.TotalRub <= 0)
            {
                SetError("Статус: корзина пуста");
                return;
            }

            SetState(PaymentState.Creating, "Статус: создаем платеж...");

            var orderId = OrderId.CreateNewUtc();
            var request = _mapper.Map(orderId, _returnUrl, _cart);

            _paymentGateway.CreatePayment(
                request,
                onSuccess: response =>
                {
                    _lastPaymentId = response.PaymentId;
                    SetState(PaymentState.Pending, $"Статус: paymentId={response.PaymentId}, ожидаем завершение");
                    // confirmationUrl is handled by presentation
                    OnPaymentCreated?.Invoke(response);
                },
                onError: error =>
                {
                    SetError($"Статус: ошибка - {error}");
                });
        }

        public void CheckPaymentStatus()
        {
            if (string.IsNullOrWhiteSpace(_lastPaymentId))
            {
                SetError("Статус: paymentId еще не создан");
                return;
            }

            SetState(PaymentState.Creating, "Статус: получаем платеж...");

            _paymentGateway.GetPaymentStatus(
                _lastPaymentId,
                onSuccess: response =>
                {
                    if (response.Status == "succeeded")
                    {
                        SetState(PaymentState.Succeeded, $"Статус: paymentId={response.PaymentId}, status=succeeded");
                    }
                    else if (response.Status == "pending" || response.Status == "waiting_for_capture")
                    {
                        SetState(PaymentState.Pending, $"Статус: paymentId={response.PaymentId}, status={response.Status}");
                    }
                    else
                    {
                        SetError($"Статус: paymentId={response.PaymentId}, status={response.Status}");
                    }
                },
                onError: error =>
                {
                    SetError($"Статус: ошибка статуса - {error}");
                });
        }
        #pragma endregion

        #pragma region State Helpers
        private void SyncIdleState()
        {
            _paymentState = PaymentState.Idle;
            _statusText = "Статус: готово";
        }

        private void SetState(PaymentState state, string statusText)
        {
            _paymentState = state;
            _statusText = statusText;
            OnPaymentStateChanged?.Invoke(new PaymentStateChanged(statusText, (int)state));
        }

        private void SetError(string statusText)
        {
            SetState(PaymentState.Error, statusText);
        }
        #pragma endregion
    }
}


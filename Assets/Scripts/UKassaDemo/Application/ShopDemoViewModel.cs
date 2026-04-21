using System;
using System.Threading;
using UKassaDemo.Domain;
using UKassaDemo.Payments;

namespace UKassaDemo.Application
{
    /// <summary>
    /// Application layer state + commands for the shop/payment UI (MVVM ViewModel).
    /// Owns a <see cref="CancellationTokenSource"/> that cancels any in-flight payment
    /// request when the ViewModel is disposed (e.g. scene unload / controller destroyed).
    /// </summary>
    public sealed class ShopDemoViewModel : IDisposable
    {
        #region Fields
        private readonly Cart _cart;
        private readonly IPaymentGateway _paymentGateway;
        private readonly PaymentRequestMapper _mapper;
        private readonly string _returnUrl;

        private readonly CancellationTokenSource _lifetimeCts = new();
        private PaymentState _paymentState = PaymentState.Idle;
        private string _statusText = "Статус: готово";
        private string _lastPaymentId;
        private bool _disposed;
        #endregion

        #region Events
        public event Action<PaymentStateChanged> OnPaymentStateChanged;
        public event Action<PaymentCreateResponse> OnPaymentCreated;
        #endregion

        #region Constructor
        public ShopDemoViewModel(Cart cart, IPaymentGateway paymentGateway, PaymentRequestMapper mapper, string returnUrl)
        {
            _cart = cart ?? throw new ArgumentNullException(nameof(cart));
            _paymentGateway = paymentGateway ?? throw new ArgumentNullException(nameof(paymentGateway));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _returnUrl = returnUrl ?? throw new ArgumentNullException(nameof(returnUrl));

            SyncIdleState();
        }
        #endregion

        #region Public Properties
        public int TotalRub => _cart.TotalRub;
        public string StatusText => _statusText;
        public PaymentState PaymentState => _paymentState;
        public Cart Cart => _cart;
        public string LastPaymentId => _lastPaymentId;
        #endregion

        #region Commands
        public void ChangeQuantity(string sku, int delta)
        {
            if (_disposed) return;

            _cart.ChangeQuantity(sku, delta);
            if (_paymentState == PaymentState.Idle || _paymentState == PaymentState.Error)
            {
                SetState(PaymentState.Idle, "Статус: готово");
            }
        }

        public void CreatePayment()
        {
            if (_disposed) return;

            if (_cart.TotalRub <= 0)
            {
                SetError(PaymentErrorKind.EmptyCart, "корзина пуста");
                return;
            }

            SetState(PaymentState.Creating, "Статус: создаем платеж...");

            var orderId = OrderId.CreateNewUtc();
            var request = _mapper.Map(orderId, _returnUrl, _cart);

            _paymentGateway.CreatePayment(
                request,
                onSuccess: response =>
                {
                    if (_disposed) return;
                    _lastPaymentId = response.PaymentId;
                    SetState(PaymentState.Pending, $"Статус: paymentId={response.PaymentId}, ожидаем завершение");
                    OnPaymentCreated?.Invoke(response);
                },
                onError: HandlePaymentError,
                cancellationToken: _lifetimeCts.Token);
        }

        public void CheckPaymentStatus()
        {
            if (_disposed) return;

            if (string.IsNullOrWhiteSpace(_lastPaymentId))
            {
                SetError(PaymentErrorKind.Validation, "paymentId еще не создан");
                return;
            }

            SetState(PaymentState.Creating, "Статус: получаем статус платежа...");

            _paymentGateway.GetPaymentStatus(
                _lastPaymentId,
                onSuccess: response =>
                {
                    if (_disposed) return;

                    switch (response.Status)
                    {
                        case "succeeded":
                            SetState(PaymentState.Succeeded, $"Статус: paymentId={response.PaymentId}, status=succeeded");
                            break;
                        case "pending":
                        case "waiting_for_capture":
                            SetState(PaymentState.Pending, $"Статус: paymentId={response.PaymentId}, status={response.Status}");
                            break;
                        default:
                            SetError(PaymentErrorKind.Unknown, $"paymentId={response.PaymentId}, status={response.Status}");
                            break;
                    }
                },
                onError: HandlePaymentError,
                cancellationToken: _lifetimeCts.Token);
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                if (!_lifetimeCts.IsCancellationRequested)
                {
                    _lifetimeCts.Cancel();
                }
            }
            finally
            {
                _lifetimeCts.Dispose();
            }

            OnPaymentStateChanged = null;
            OnPaymentCreated = null;
        }
        #endregion

        #region State Helpers
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

        private void SetError(PaymentErrorKind kind, string message)
        {
            SetState(PaymentState.Error, $"Статус: ошибка ({kind}) — {message}");
        }

        private void HandlePaymentError(PaymentError error)
        {
            if (_disposed) return;

            // Cancellation is expected on scene exit — don't surface as a user error.
            if (error.Kind == PaymentErrorKind.Cancelled)
            {
                return;
            }

            SetError(error.Kind, error.Message);
        }
        #endregion
    }
}

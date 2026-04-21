using System;
using System.Collections.Generic;
using System.Threading;

namespace UKassaDemo.Payments
{
    /// <summary>
    /// In-memory payment gateway used to exercise the full flow without a running backend.
    /// After 3 seconds from creation a payment switches to <c>succeeded</c>.
    /// </summary>
    public sealed class MockPaymentGateway : IPaymentGateway
    {
        private sealed class MockPaymentState
        {
            public string PaymentId;
            public DateTime CreatedAtUtc;
        }

        private readonly Dictionary<string, MockPaymentState> _payments = new();

        public void CreatePayment(
            PaymentCreateRequest request,
            Action<PaymentCreateResponse> onSuccess,
            Action<PaymentError> onError,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                onError?.Invoke(new PaymentError(PaymentErrorKind.Cancelled, "Cancelled before start"));
                return;
            }

            if (request == null || request.Items == null || request.Items.Count == 0 || request.TotalRub <= 0)
            {
                onError?.Invoke(new PaymentError(PaymentErrorKind.EmptyCart, "Cart is empty or total is invalid"));
                return;
            }

            var response = new PaymentCreateResponse(
                paymentId: $"test_{Guid.NewGuid():N}",
                confirmationUrl: "https://yoomoney.ru/",
                status: "pending");

            _payments[response.PaymentId] = new MockPaymentState
            {
                PaymentId = response.PaymentId,
                CreatedAtUtc = DateTime.UtcNow,
            };

            onSuccess?.Invoke(response);
        }

        public void GetPaymentStatus(
            string paymentId,
            Action<PaymentStatusResponse> onSuccess,
            Action<PaymentError> onError,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                onError?.Invoke(new PaymentError(PaymentErrorKind.Cancelled, "Cancelled before start"));
                return;
            }

            if (string.IsNullOrWhiteSpace(paymentId))
            {
                onError?.Invoke(new PaymentError(PaymentErrorKind.Validation, "paymentId is empty"));
                return;
            }

            if (!_payments.TryGetValue(paymentId, out var state))
            {
                onError?.Invoke(new PaymentError(PaymentErrorKind.NotFound, "paymentId not found in mock gateway"));
                return;
            }

            var elapsed = DateTime.UtcNow - state.CreatedAtUtc;
            var status = elapsed.TotalSeconds >= 3 ? "succeeded" : "pending";

            onSuccess?.Invoke(new PaymentStatusResponse(paymentId, status));
        }
    }
}

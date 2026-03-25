using System;
using System.Collections.Generic;
using UnityEngine;

namespace UKassaDemo.Payments
{
    public class MockPaymentGateway : IPaymentGateway
    {
        private class MockPaymentState
        {
            public string paymentId;
            public DateTime createdAtUtc;
        }

        private readonly Dictionary<string, MockPaymentState> _payments = new();

        public void CreatePayment(PaymentCreateRequest request, Action<PaymentCreateResponse> onSuccess, Action<string> onError)
        {
            if (request == null || request.items == null || request.items.Count == 0 || request.totalRub <= 0)
            {
                onError?.Invoke("Корзина пуста или сумма некорректна.");
                return;
            }

            var response = new PaymentCreateResponse
            {
                paymentId = $"test_{Guid.NewGuid():N}",
                status = "pending",
                confirmationUrl = "https://yoomoney.ru/"
            };

            _payments[response.paymentId] = new MockPaymentState
            {
                paymentId = response.paymentId,
                createdAtUtc = DateTime.UtcNow
            };

            onSuccess?.Invoke(response);
        }

        public void GetPaymentStatus(string paymentId, Action<PaymentStatusResponse> onSuccess, Action<string> onError)
        {
            if (string.IsNullOrWhiteSpace(paymentId))
            {
                onError?.Invoke("Некорректный paymentId.");
                return;
            }

            if (!_payments.TryGetValue(paymentId, out var state))
            {
                onError?.Invoke("paymentId не найден в mock-кассе.");
                return;
            }

            var elapsed = DateTime.UtcNow - state.createdAtUtc;
            var status = elapsed.TotalSeconds >= 3 ? "succeeded" : "pending";

            onSuccess?.Invoke(new PaymentStatusResponse
            {
                paymentId = paymentId,
                status = status
            });
        }
    }
}

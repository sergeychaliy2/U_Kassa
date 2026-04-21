using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace UKassaDemo.Payments
{
    [Serializable]
    public class PaymentItemRequest
    {
        [SerializeField] private string sku;
        [SerializeField] private string title;
        [SerializeField] private int quantity;
        [SerializeField] private int unitPriceRub;

        public string Sku => sku;
        public string Title => title;
        public int Quantity => quantity;
        public int UnitPriceRub => unitPriceRub;

        public PaymentItemRequest(string sku, string title, int quantity, int unitPriceRub)
        {
            this.sku = sku;
            this.title = title;
            this.quantity = quantity;
            this.unitPriceRub = unitPriceRub;
        }

        // JsonUtility needs an empty constructor.
        public PaymentItemRequest() { }
    }

    [Serializable]
    public class PaymentCreateRequest
    {
        [SerializeField] private string orderId;
        [SerializeField] private string currency = "RUB";
        [SerializeField] private List<PaymentItemRequest> items = new();
        [SerializeField] private int totalRub;
        [SerializeField] private string returnUrl;

        public string OrderId => orderId;
        public string Currency => currency;
        public List<PaymentItemRequest> Items => items;
        public int TotalRub => totalRub;
        public string ReturnUrl => returnUrl;

        public PaymentCreateRequest(string orderId, List<PaymentItemRequest> items, int totalRub, string returnUrl)
        {
            this.orderId = orderId;
            this.items = items;
            this.totalRub = totalRub;
            this.returnUrl = returnUrl;
        }

        // JsonUtility needs an empty constructor.
        public PaymentCreateRequest() { }
    }

    [Serializable]
    public class PaymentCreateResponse
    {
        [SerializeField] private string paymentId;
        [SerializeField] private string confirmationUrl;
        [SerializeField] private string status;

        public string PaymentId => paymentId;
        public string ConfirmationUrl => confirmationUrl;
        public string Status => status;

        public PaymentCreateResponse(string paymentId, string confirmationUrl, string status)
        {
            this.paymentId = paymentId;
            this.confirmationUrl = confirmationUrl;
            this.status = status;
        }

        public PaymentCreateResponse() { }
    }

    [Serializable]
    public class PaymentStatusResponse
    {
        [SerializeField] private string paymentId;
        [SerializeField] private string status;

        public string PaymentId => paymentId;
        public string Status => status;

        public PaymentStatusResponse(string paymentId, string status)
        {
            this.paymentId = paymentId;
            this.status = status;
        }

        public PaymentStatusResponse() { }
    }

    /// <summary>
    /// Port for payment transport. Domain/Application never depend on a concrete
    /// implementation — this allows swapping Mock ↔ Backend without touching callers.
    /// </summary>
    public interface IPaymentGateway
    {
        /// <summary>
        /// Creates a payment. Exactly one of <paramref name="onSuccess"/> / <paramref name="onError"/>
        /// will be invoked. If <paramref name="cancellationToken"/> is cancelled before completion,
        /// <paramref name="onError"/> is called with <see cref="PaymentErrorKind.Cancelled"/>.
        /// </summary>
        void CreatePayment(
            PaymentCreateRequest request,
            Action<PaymentCreateResponse> onSuccess,
            Action<PaymentError> onError,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves current status for a previously created payment.
        /// Contract for cancellation and callbacks is identical to <see cref="CreatePayment"/>.
        /// </summary>
        void GetPaymentStatus(
            string paymentId,
            Action<PaymentStatusResponse> onSuccess,
            Action<PaymentError> onError,
            CancellationToken cancellationToken = default);
    }
}

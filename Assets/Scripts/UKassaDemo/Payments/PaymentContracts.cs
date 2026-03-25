using System;
using System.Collections.Generic;
using UnityEngine;

namespace UKassaDemo.Payments
{
    [Serializable]
    public class PaymentItemRequest
    {
        public string sku;
        public string title;
        public int quantity;
        public int unitPriceRub;
    }

    [Serializable]
    public class PaymentCreateRequest
    {
        public string orderId;
        public string currency = "RUB";
        public List<PaymentItemRequest> items = new();
        public int totalRub;
        public string returnUrl;
    }

    [Serializable]
    public class PaymentCreateResponse
    {
        public string paymentId;
        public string confirmationUrl;
        public string status;
    }

    [Serializable]
    public class PaymentStatusResponse
    {
        public string paymentId;
        public string status;
    }

    public interface IPaymentGateway
    {
        void CreatePayment(PaymentCreateRequest request, Action<PaymentCreateResponse> onSuccess, Action<string> onError);
        void GetPaymentStatus(string paymentId, Action<PaymentStatusResponse> onSuccess, Action<string> onError);
    }
}

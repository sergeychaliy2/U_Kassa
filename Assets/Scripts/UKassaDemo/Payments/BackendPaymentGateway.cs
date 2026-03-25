using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace UKassaDemo.Payments
{
    public class BackendPaymentGateway : IPaymentGateway
    {
        private readonly MonoBehaviour _runner;
        private readonly string _endpoint;
        private readonly string _statusEndpointTemplate;
        private readonly string _apiKey;

        public BackendPaymentGateway(MonoBehaviour runner, string endpoint, string statusEndpointTemplate, string apiKey)
        {
            _runner = runner;
            _endpoint = endpoint;
            _statusEndpointTemplate = statusEndpointTemplate;
            _apiKey = apiKey;
        }

        public void CreatePayment(PaymentCreateRequest request, Action<PaymentCreateResponse> onSuccess, Action<string> onError)
        {
            _runner.StartCoroutine(SendRequest(request, onSuccess, onError));
        }

        public void GetPaymentStatus(string paymentId, Action<PaymentStatusResponse> onSuccess, Action<string> onError)
        {
            if (string.IsNullOrWhiteSpace(paymentId))
            {
                onError?.Invoke("paymentId пустой.");
                return;
            }

            _runner.StartCoroutine(SendStatusRequest(paymentId, onSuccess, onError));
        }

        private System.Collections.IEnumerator SendRequest(PaymentCreateRequest request, Action<PaymentCreateResponse> onSuccess, Action<string> onError)
        {
            var json = JsonUtility.ToJson(request);
            var bodyRaw = Encoding.UTF8.GetBytes(json);
            using var req = new UnityWebRequest(_endpoint, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                req.SetRequestHeader("X-Client-Key", _apiKey);
            }

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Ошибка backend: {req.error}");
                yield break;
            }

            var text = req.downloadHandler.text;
            if (string.IsNullOrWhiteSpace(text))
            {
                onError?.Invoke("Backend вернул пустой ответ.");
                yield break;
            }

            PaymentCreateResponse response;
            try
            {
                response = JsonUtility.FromJson<PaymentCreateResponse>(text);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Невалидный JSON от backend: {ex.Message}");
                yield break;
            }

            if (response == null || string.IsNullOrWhiteSpace(response.confirmationUrl))
            {
                onError?.Invoke("В ответе нет confirmationUrl.");
                yield break;
            }

            onSuccess?.Invoke(response);
        }

        private System.Collections.IEnumerator SendStatusRequest(string paymentId, Action<PaymentStatusResponse> onSuccess, Action<string> onError)
        {
            var url = _statusEndpointTemplate.EndsWith("/")
                ? _statusEndpointTemplate + Uri.EscapeDataString(paymentId)
                : _statusEndpointTemplate + "/" + Uri.EscapeDataString(paymentId);

            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Accept", "application/json");

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                req.SetRequestHeader("X-Client-Key", _apiKey);
            }

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Ошибка backend(status): {req.error}");
                yield break;
            }

            var text = req.downloadHandler.text;
            if (string.IsNullOrWhiteSpace(text))
            {
                onError?.Invoke("Backend вернул пустой ответ(status).");
                yield break;
            }

            PaymentStatusResponse response;
            try
            {
                response = JsonUtility.FromJson<PaymentStatusResponse>(text);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Невалидный JSON от backend(status): {ex.Message}");
                yield break;
            }

            if (response == null || string.IsNullOrWhiteSpace(response.status))
            {
                onError?.Invoke("Backend(status): не удалось распарсить status.");
                yield break;
            }

            onSuccess?.Invoke(response);
        }
    }
}

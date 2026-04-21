using System;
using System.Collections;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace UKassaDemo.Payments
{
    /// <summary>
    /// Adapter that talks to our own backend (which owns YooKassa secrets).
    /// Unity client never touches YooKassa API directly.
    /// </summary>
    public sealed class BackendPaymentGateway : IPaymentGateway
    {
        private const int DefaultTimeoutSeconds = 20;

        private readonly MonoBehaviour _coroutineRunner;
        private readonly string _createEndpoint;
        private readonly string _statusEndpointTemplate;
        private readonly string _clientKey;
        private readonly int _timeoutSeconds;

        public BackendPaymentGateway(
            MonoBehaviour coroutineRunner,
            string createEndpoint,
            string statusEndpointTemplate,
            string clientKey,
            int timeoutSeconds = DefaultTimeoutSeconds)
        {
            _coroutineRunner = coroutineRunner != null
                ? coroutineRunner
                : throw new ArgumentNullException(nameof(coroutineRunner));
            _createEndpoint = createEndpoint;
            _statusEndpointTemplate = statusEndpointTemplate;
            _clientKey = clientKey;
            _timeoutSeconds = Mathf.Max(1, timeoutSeconds);
        }

        public void CreatePayment(
            PaymentCreateRequest request,
            Action<PaymentCreateResponse> onSuccess,
            Action<PaymentError> onError,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                onError?.Invoke(new PaymentError(PaymentErrorKind.Validation, "request is null"));
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                onError?.Invoke(new PaymentError(PaymentErrorKind.Cancelled, "Cancelled before start"));
                return;
            }

            _coroutineRunner.StartCoroutine(SendCreateRequest(request, onSuccess, onError, cancellationToken));
        }

        public void GetPaymentStatus(
            string paymentId,
            Action<PaymentStatusResponse> onSuccess,
            Action<PaymentError> onError,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(paymentId))
            {
                onError?.Invoke(new PaymentError(PaymentErrorKind.Validation, "paymentId is empty"));
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                onError?.Invoke(new PaymentError(PaymentErrorKind.Cancelled, "Cancelled before start"));
                return;
            }

            _coroutineRunner.StartCoroutine(SendStatusRequest(paymentId, onSuccess, onError, cancellationToken));
        }

        private IEnumerator SendCreateRequest(
            PaymentCreateRequest request,
            Action<PaymentCreateResponse> onSuccess,
            Action<PaymentError> onError,
            CancellationToken ct)
        {
            var json = JsonUtility.ToJson(request);
            var body = Encoding.UTF8.GetBytes(json);

            using var req = new UnityWebRequest(_createEndpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(body),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = _timeoutSeconds,
            };
            req.SetRequestHeader("Content-Type", "application/json");
            ApplyClientKey(req);

            yield return WaitForRequest(req, ct);

            if (TryHandleFailure(req, ct, onError))
            {
                yield break;
            }

            PaymentCreateResponse response;
            var raw = req.downloadHandler.text;
            if (!TryParseJson(raw, onError, out response))
            {
                yield break;
            }

            if (response == null || string.IsNullOrWhiteSpace(response.ConfirmationUrl))
            {
                onError?.Invoke(new PaymentError(PaymentErrorKind.InvalidResponse, "confirmationUrl missing", raw));
                yield break;
            }

            onSuccess?.Invoke(response);
        }

        private IEnumerator SendStatusRequest(
            string paymentId,
            Action<PaymentStatusResponse> onSuccess,
            Action<PaymentError> onError,
            CancellationToken ct)
        {
            var url = _statusEndpointTemplate != null && _statusEndpointTemplate.EndsWith("/")
                ? _statusEndpointTemplate + Uri.EscapeDataString(paymentId)
                : _statusEndpointTemplate + "/" + Uri.EscapeDataString(paymentId);

            using var req = UnityWebRequest.Get(url);
            req.timeout = _timeoutSeconds;
            req.SetRequestHeader("Accept", "application/json");
            ApplyClientKey(req);

            yield return WaitForRequest(req, ct);

            if (TryHandleFailure(req, ct, onError))
            {
                yield break;
            }

            PaymentStatusResponse response;
            var raw = req.downloadHandler.text;
            if (!TryParseJson(raw, onError, out response))
            {
                yield break;
            }

            if (response == null || string.IsNullOrWhiteSpace(response.Status))
            {
                onError?.Invoke(new PaymentError(PaymentErrorKind.InvalidResponse, "status missing", raw));
                yield break;
            }

            onSuccess?.Invoke(response);
        }

        private static IEnumerator WaitForRequest(UnityWebRequest req, CancellationToken ct)
        {
            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                if (ct.IsCancellationRequested)
                {
                    req.Abort();
                    yield break;
                }
                yield return null;
            }
        }

        private static bool TryHandleFailure(UnityWebRequest req, CancellationToken ct, Action<PaymentError> onError)
        {
            if (ct.IsCancellationRequested)
            {
                onError?.Invoke(new PaymentError(PaymentErrorKind.Cancelled, "Request cancelled"));
                return true;
            }

#if UNITY_2020_2_OR_NEWER
            if (req.result == UnityWebRequest.Result.Success)
            {
                return false;
            }

            var kind = req.result == UnityWebRequest.Result.ConnectionError
                ? PaymentErrorKind.Network
                : PaymentErrorKind.InvalidResponse;
#else
            if (!req.isHttpError && !req.isNetworkError) return false;
            var kind = req.isNetworkError ? PaymentErrorKind.Network : PaymentErrorKind.InvalidResponse;
#endif

            onError?.Invoke(new PaymentError(kind, req.error ?? "transport error", req.downloadHandler?.text));
            return true;
        }

        private static bool TryParseJson<T>(string raw, Action<PaymentError> onError, out T response) where T : class
        {
            response = null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                onError?.Invoke(new PaymentError(PaymentErrorKind.InvalidResponse, "empty response body"));
                return false;
            }

            try
            {
                response = JsonUtility.FromJson<T>(raw);
            }
            catch (Exception ex)
            {
                onError?.Invoke(new PaymentError(PaymentErrorKind.InvalidResponse, $"bad json: {ex.Message}", raw));
                return false;
            }

            if (response == null)
            {
                onError?.Invoke(new PaymentError(PaymentErrorKind.InvalidResponse, "failed to deserialize", raw));
                return false;
            }

            return true;
        }

        private void ApplyClientKey(UnityWebRequest req)
        {
            if (!string.IsNullOrWhiteSpace(_clientKey))
            {
                req.SetRequestHeader("X-Client-Key", _clientKey);
            }
        }
    }
}

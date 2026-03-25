using UnityEngine;

namespace UKassaDemo.Config
{
    /// <summary>
    /// Backend endpoints and credentials reference (without secrets in client build in production).
    /// </summary>
    [CreateAssetMenu(menuName = "UKassa Demo/Backend Config")]
    public sealed class UKassaDemoBackendConfig : ScriptableObject
    {
        [SerializeField] private bool useBackendGateway = true;
        [SerializeField] private string backendCreatePaymentEndpoint = "http://localhost:3000/api/payments/create";
        [SerializeField] private string backendGetPaymentStatusEndpointTemplate = "http://localhost:3000/api/payments/status";
        [SerializeField] private string backendClientKey = "";
        [SerializeField] private string returnUrl = "http://localhost:3000/return";

        public bool UseBackendGateway => useBackendGateway;
        public string BackendCreatePaymentEndpoint => backendCreatePaymentEndpoint;
        public string BackendGetPaymentStatusEndpointTemplate => backendGetPaymentStatusEndpointTemplate;
        public string BackendClientKey => backendClientKey;
        public string ReturnUrl => returnUrl;
    }
}


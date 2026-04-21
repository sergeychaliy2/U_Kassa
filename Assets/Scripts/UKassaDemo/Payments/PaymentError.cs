namespace UKassaDemo.Payments
{
    /// <summary>
    /// Classification of payment-flow failures. Allows callers to react differently
    /// to transport errors, validation, cancellation, etc., without string-parsing.
    /// </summary>
    public enum PaymentErrorKind
    {
        Unknown = 0,
        Network = 1,
        InvalidResponse = 2,
        Validation = 3,
        Cancelled = 4,
        EmptyCart = 5,
        NotFound = 6,
        Timeout = 7,
    }

    /// <summary>
    /// Structured error passed to <see cref="IPaymentGateway"/> callbacks.
    /// Replaces lossy <c>Action&lt;string&gt;</c>.
    /// </summary>
    public readonly struct PaymentError
    {
        public PaymentErrorKind Kind { get; }
        public string Message { get; }
        public string RawPayload { get; }

        public PaymentError(PaymentErrorKind kind, string message, string rawPayload = null)
        {
            Kind = kind;
            Message = message ?? string.Empty;
            RawPayload = rawPayload;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Message) ? Kind.ToString() : $"{Kind}: {Message}";
        }
    }
}

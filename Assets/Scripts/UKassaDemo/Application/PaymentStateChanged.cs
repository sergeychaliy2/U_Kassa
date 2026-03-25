namespace UKassaDemo.Application
{
    /// <summary>
    /// Event payload for payment state transitions.
    /// </summary>
    public readonly struct PaymentStateChanged
    {
        public string Message { get; }
        public int PaymentState { get; }

        public PaymentStateChanged(string message, int paymentState)
        {
            Message = message;
            PaymentState = paymentState;
        }
    }
}


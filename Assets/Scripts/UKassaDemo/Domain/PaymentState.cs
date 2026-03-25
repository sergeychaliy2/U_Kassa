namespace UKassaDemo.Domain
{
    /// <summary>
    /// Payment state for UI/state-machine display.
    /// </summary>
    public enum PaymentState
    {
        Idle = 0,
        Creating = 1,
        Pending = 2,
        Succeeded = 3,
        Error = 4
    }
}


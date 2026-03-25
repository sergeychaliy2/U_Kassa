using System;

namespace UKassaDemo.Domain
{
    /// <summary>
    /// Value object for idempotent payment order id.
    /// </summary>
    public readonly struct OrderId
    {
        public string Value { get; }

        public OrderId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("order id is required", nameof(value));
            Value = value;
        }

        public static OrderId CreateNewUtc()
        {
            var ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return new OrderId($"order_{ms}");
        }
    }
}


namespace BookingSystem.Shared.Contracts.Events;

public static class KafkaTopics
{
    /// <summary>Published by BookingService when a booking is created.</summary>
    public const string BookingCreated = "booking.created";

    /// <summary>Published by BookingService when a booking is cancelled.</summary>
    public const string BookingCancelled = "booking.cancelled";

    /// <summary>Published by BookingService when a captured payment cannot be confirmed against its booking (compensation signal).</summary>
    public const string BookingConfirmationFailed = "booking.confirmation.failed";

    /// <summary>Published by PaymentService when a payment succeeds.</summary>
    public const string PaymentSucceeded = "payment.succeeded";

    /// <summary>Published by PaymentService when a payment fails.</summary>
    public const string PaymentFailed = "payment.failed";

    /// <summary>Published by PaymentService when a captured payment is reversed (compensation for a failed booking confirmation).</summary>
    public const string PaymentRefunded = "payment.refunded";

    /// <summary>Published by PaymentService when a refund is permanently declined and needs manual intervention.</summary>
    public const string PaymentRefundFailed = "payment.refund.failed";
}

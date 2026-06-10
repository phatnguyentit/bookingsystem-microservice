namespace BookingSystem.Shared.Messaging;

public sealed class KafkaServerSettings
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";
}

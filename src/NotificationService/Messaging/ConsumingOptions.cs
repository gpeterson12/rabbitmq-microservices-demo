namespace NotificationService.Messaging;

public sealed class ConsumingOptions
{
    public const string SectionName = "Consuming";

    /// <summary>Messages fetched per consumer before acks; keep >= dispatch concurrency.</summary>
    public ushort PrefetchCount { get; set; } = 16;

    /// <summary>Concurrent deliveries dispatched per channel; 1 restores strict sequential processing.</summary>
    public ushort ConsumerDispatchConcurrency { get; set; } = 8;
}

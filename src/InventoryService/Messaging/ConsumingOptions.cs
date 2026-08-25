namespace InventoryService.Messaging;

public sealed class ConsumingOptions
{
    public const string SectionName = "Consuming";

    /// <summary>Messages fetched per consumer before acks; keep >= dispatch concurrency.</summary>
    public ushort PrefetchCount { get; set; } = 16;

    /// <summary>Concurrent deliveries dispatched per channel; 1 restores strict sequential processing.</summary>
    public ushort ConsumerDispatchConcurrency { get; set; } = 8;

    /// <summary>
    /// Demo-only artificial latency that makes round-robin splitting visible in logs.
    /// Disable for load testing.
    /// </summary>
    public bool SimulatedProcessingDelayEnabled { get; set; } = true;

    public int MinProcessingDelayMilliseconds { get; set; } = 300;

    public int MaxProcessingDelayMilliseconds { get; set; } = 800;
}

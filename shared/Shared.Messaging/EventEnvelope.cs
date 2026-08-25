namespace Shared.Messaging;

public record EventEnvelope
{
    public required Guid EventId { get; init; }
    public required string EventType { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
}

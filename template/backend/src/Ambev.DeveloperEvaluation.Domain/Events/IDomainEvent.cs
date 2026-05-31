namespace Ambev.DeveloperEvaluation.Domain.Events;

public interface IDomainEvent
{
    Guid EventId { get; }
    string EventType { get; }
    Guid AggregateId { get; }
    string AggregateType { get; }
    DateTime OccurredAt { get; }
}

using Ambev.DeveloperEvaluation.Domain.Entities;

namespace Ambev.DeveloperEvaluation.Domain.Events;

public record ItemCancelledEvent(Sale Sale, Guid ItemId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public string EventType => "ItemCancelled";
    public Guid AggregateId => Sale.Id;
    public string AggregateType => "Sale";
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

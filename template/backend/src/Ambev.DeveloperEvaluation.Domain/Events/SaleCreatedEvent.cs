using Ambev.DeveloperEvaluation.Domain.Entities;

namespace Ambev.DeveloperEvaluation.Domain.Events;

public record SaleCreatedEvent(Sale Sale) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public string EventType => "SaleCreated";
    public Guid AggregateId => Sale.Id;
    public string AggregateType => "Sale";
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

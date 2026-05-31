using Ambev.DeveloperEvaluation.Domain.Events;

namespace Ambev.DeveloperEvaluation.Domain.Repositories;

public interface IEventStoreRepository
{
    Task AppendAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
    Task<IEnumerable<SaleEventRecord>> GetByAggregateIdAsync(Guid aggregateId, CancellationToken cancellationToken = default);
}

public record SaleEventRecord(
    Guid EventId,
    string EventType,
    Guid AggregateId,
    string AggregateType,
    DateTime OccurredAt,
    string Payload
);

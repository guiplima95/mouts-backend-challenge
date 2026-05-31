using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;

namespace Ambev.DeveloperEvaluation.ORM.EventStore;

public class MongoEventStoreRepository : IEventStoreRepository
{
    private readonly IMongoCollection<SaleEventDocument> _collection;

    public MongoEventStoreRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<SaleEventDocument>("sale_events");
    }

    public async Task AppendAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var payloadJson = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        var document = new SaleEventDocument
        {
            EventId = domainEvent.EventId,
            EventType = domainEvent.EventType,
            AggregateId = domainEvent.AggregateId,
            AggregateType = domainEvent.AggregateType,
            OccurredAt = domainEvent.OccurredAt,
            Payload = BsonDocument.Parse(payloadJson)
        };

        await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
    }

    public async Task<IEnumerable<SaleEventRecord>> GetByAggregateIdAsync(Guid aggregateId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<SaleEventDocument>.Filter.Eq(d => d.AggregateId, aggregateId);
        var sort = Builders<SaleEventDocument>.Sort.Ascending(d => d.OccurredAt);

        var documents = await _collection
            .Find(filter)
            .Sort(sort)
            .ToListAsync(cancellationToken);

        return documents.Select(d => new SaleEventRecord(
            d.EventId,
            d.EventType,
            d.AggregateId,
            d.AggregateType,
            d.OccurredAt,
            d.Payload.ToJson()
        ));
    }
}

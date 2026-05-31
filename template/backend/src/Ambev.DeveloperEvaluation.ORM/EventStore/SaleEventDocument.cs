using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Ambev.DeveloperEvaluation.ORM.EventStore;

public class SaleEventDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid EventId { get; set; }

    [BsonElement("eventType")]
    public string EventType { get; set; } = string.Empty;

    [BsonElement("aggregateId")]
    [BsonRepresentation(BsonType.String)]
    public Guid AggregateId { get; set; }

    [BsonElement("aggregateType")]
    public string AggregateType { get; set; } = string.Empty;

    [BsonElement("occurredAt")]
    public DateTime OccurredAt { get; set; }

    [BsonElement("payload")]
    public BsonDocument Payload { get; set; } = new();
}

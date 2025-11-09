using Bermondsey.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Bermondsey;

public record NotificationStatus(
    [property: BsonId]
    [property: BsonRepresentation(BsonType.String)]
    Guid Id,
    NotificationSentBy NotificationSentBy
);

using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using TalentCampus.Core.Roles;

namespace TalentCampus.Providers.MongoDb;

/// <summary>Durable role storage backed by the Talent institution's own Mongo database.</summary>
public sealed class MongoTalentSteward : ITalentSteward
{
    private readonly IMongoCollection<RoleDocument> roles;

    public MongoTalentSteward(IMongoDatabase database, string collectionName = "roles")
    {
        ArgumentNullException.ThrowIfNull(database);
        roles = database.GetCollection<RoleDocument>(collectionName);
    }

    public RoleDefinition DefineRole(string name, string purpose, IEnumerable<string> requiredCapabilities,
        string responsibilities = "", string successCriteria = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responsibilities);
        ArgumentException.ThrowIfNullOrWhiteSpace(successCriteria);
        var role = new RoleDefinition(RoleId.New(), name, purpose, requiredCapabilities, responsibilities, successCriteria);
        roles.InsertOne(RoleDocument.From(role));
        return role;
    }

    public IReadOnlyCollection<RoleDefinition> ListRoles() =>
        roles.Find(FilterDefinition<RoleDocument>.Empty).ToList().Select(ToRole).ToArray();

    private static RoleDefinition ToRole(RoleDocument document) => new(
        new RoleId(document.Id), document.Name, document.Purpose, document.Capabilities,
        document.Responsibilities, document.SuccessCriteria);

    private sealed class RoleDocument
    {
        [BsonId]
        public Guid Id { get; set; }

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("purpose")]
        public string Purpose { get; set; } = string.Empty;

        [BsonElement("capabilities")]
        public string[] Capabilities { get; set; } = [];

        [BsonElement("responsibilities")]
        public string Responsibilities { get; set; } = string.Empty;

        [BsonElement("successCriteria")]
        public string SuccessCriteria { get; set; } = string.Empty;

        public static RoleDocument From(RoleDefinition role) => new()
        {
            Id = role.Id.Value,
            Name = role.Name,
            Purpose = role.Purpose,
            Capabilities = role.RequiredCapabilities.ToArray(),
            Responsibilities = role.Responsibilities,
            SuccessCriteria = role.SuccessCriteria
        };
    }
}

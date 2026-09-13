using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using TalentCampus.Core.Personas;

namespace TalentCampus.Providers.MongoDb;

/// <summary>Durable persona storage backed by the Talent institution's own Mongo database.</summary>
public sealed class MongoPersonaDirectory : IPersonaDirectory
{
    private readonly IMongoCollection<PersonaDocument> personas;

    public MongoPersonaDirectory(IMongoDatabase database, string collectionName = "personas")
    {
        ArgumentNullException.ThrowIfNull(database);
        personas = database.GetCollection<PersonaDocument>(collectionName);
    }

    public PersonaDefinition Create(string name, string purpose, string workingStyle, string strengths, string boundaries)
    {
        var persona = new PersonaDefinition(Guid.NewGuid(), name, purpose, workingStyle, strengths, boundaries);
        personas.InsertOne(PersonaDocument.From(persona));
        return persona;
    }

    public IReadOnlyCollection<PersonaDefinition> List() =>
        personas.Find(FilterDefinition<PersonaDocument>.Empty).ToList().Select(ToPersona).ToArray();

    private static PersonaDefinition ToPersona(PersonaDocument document) => new(
        document.Id, document.Name, document.Purpose, document.WorkingStyle, document.Strengths, document.Boundaries);

    private sealed class PersonaDocument
    {
        [BsonId]
        public Guid Id { get; set; }

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("purpose")]
        public string Purpose { get; set; } = string.Empty;

        [BsonElement("workingStyle")]
        public string WorkingStyle { get; set; } = string.Empty;

        [BsonElement("strengths")]
        public string Strengths { get; set; } = string.Empty;

        [BsonElement("boundaries")]
        public string Boundaries { get; set; } = string.Empty;

        public static PersonaDocument From(PersonaDefinition persona) => new()
        {
            Id = persona.Id,
            Name = persona.Name,
            Purpose = persona.Purpose,
            WorkingStyle = persona.WorkingStyle,
            Strengths = persona.Strengths,
            Boundaries = persona.Boundaries
        };
    }
}

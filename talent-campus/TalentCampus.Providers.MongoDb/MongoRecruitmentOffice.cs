using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using TalentCampus.Core.Personas;
using TalentCampus.Core.Recruitment;
using TalentCampus.Core.Roles;

namespace TalentCampus.Providers.MongoDb;

/// <summary>
/// Durable recruitment storage backed by the Talent institution's own Mongo database.
/// Human candidates and considerations share one collection, distinguished by the
/// document discriminator, matching the file-backed office's single-store shape.
/// </summary>
public sealed class MongoRecruitmentOffice : IRecruitmentOffice
{
    private readonly IMongoCollection<RecruitmentRecordDocument> records;
    private readonly ITalentSteward roles;
    private readonly IPersonaDirectory personas;
    private readonly DecisionsDestination[] destinations;

    public MongoRecruitmentOffice(IMongoDatabase database, ITalentSteward roles, IPersonaDirectory personas,
        IEnumerable<DecisionsDestination> destinations, string collectionName = "recruitment")
    {
        ArgumentNullException.ThrowIfNull(database);
        records = database.GetCollection<RecruitmentRecordDocument>(collectionName);
        this.roles = roles;
        this.personas = personas;
        this.destinations = destinations.ToArray();
        if (this.destinations.Any(d => d is null || string.IsNullOrWhiteSpace(d.Id) || string.IsNullOrWhiteSpace(d.Name)) ||
            this.destinations.Select(d => d.Id).Distinct(StringComparer.Ordinal).Count() != this.destinations.Length)
            throw new ArgumentException("Decisions offices require unique IDs and names.", nameof(destinations));
    }

    public HumanCandidate RegisterHuman(string name, string background)
    {
        var candidate = new HumanCandidate(Guid.NewGuid(), Required(name, "Candidate name"), Required(background, "Background"));
        records.InsertOne(HumanCandidateDocument.From(candidate));
        return candidate;
    }

    public IReadOnlyCollection<HumanCandidate> ListHumans() =>
        records.OfType<HumanCandidateDocument>().Find(FilterDefinition<HumanCandidateDocument>.Empty)
            .ToList().Select(d => new HumanCandidate(d.Id, d.Name, d.Background)).ToArray();

    public IReadOnlyCollection<DecisionsDestination> ListDestinations() => destinations.ToArray();

    public IReadOnlyCollection<Consideration> ListConsiderations() =>
        records.OfType<ConsiderationDocument>().Find(FilterDefinition<ConsiderationDocument>.Empty)
            .ToList().Select(ToConsideration).ToArray();

    public Consideration Prepare(Guid roleId, ParticipantKind kind, Guid participantId,
        string evidence, string recommendation, string destinationId)
    {
        evidence = Required(evidence, "Evidence");
        recommendation = Required(recommendation, "Recommendation");
        var destination = destinations.SingleOrDefault(d => d.Id == destinationId)
            ?? throw new ArgumentException("Select a configured Decisions office.");
        var role = roles.ListRoles().SingleOrDefault(r => r.Id.Value == roleId)
            ?? throw new ArgumentException("Select an existing role.");

        ParticipantSnapshot participant;
        if (kind == ParticipantKind.Human)
        {
            var human = ListHumans().SingleOrDefault(h => h.Id == participantId)
                ?? throw new ArgumentException("Select a registered human candidate.");
            participant = new(human.Id, kind, human.Name, human.Background, "", "", "", "");
        }
        else if (kind == ParticipantKind.AiPersona)
        {
            var persona = personas.List().SingleOrDefault(p => p.Id == participantId)
                ?? throw new ArgumentException("Select an existing AI persona.");
            participant = new(persona.Id, kind, persona.Name, "", persona.Purpose,
                persona.WorkingStyle, persona.Strengths, persona.Boundaries);
        }
        else throw new ArgumentException("Select a supported participant type.");

        var consideration = new Consideration(Guid.NewGuid(), DateTimeOffset.UtcNow,
            new(role.Id.Value, role.Name, role.Purpose, role.Responsibilities,
                role.RequiredCapabilities.ToArray(), role.SuccessCriteria),
            participant, evidence, recommendation, destination);
        records.InsertOne(ConsiderationDocument.From(consideration));
        return consideration;
    }

    private static Consideration ToConsideration(ConsiderationDocument d) => new(
        d.Id, d.PreparedAt,
        new RoleSnapshot(d.RoleId, d.RoleName, d.RolePurpose, d.RoleResponsibilities, d.RoleCapabilities, d.RoleSuccessCriteria),
        new ParticipantSnapshot(d.ParticipantId, d.ParticipantKind, d.ParticipantName, d.ParticipantBackground,
            d.ParticipantPurpose, d.ParticipantWorkingStyle, d.ParticipantStrengths, d.ParticipantBoundaries),
        d.Evidence, d.Recommendation, new DecisionsDestination(d.DestinationId, d.DestinationName));

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);
    private static string Required(string value, string label) => !Blank(value) ? value.Trim() :
        throw new ArgumentException($"{label} is required.");

    [BsonDiscriminator(RootClass = true)]
    [BsonKnownTypes(typeof(HumanCandidateDocument), typeof(ConsiderationDocument))]
    private abstract class RecruitmentRecordDocument
    {
        [BsonId]
        public Guid Id { get; set; }
    }

    private sealed class HumanCandidateDocument : RecruitmentRecordDocument
    {
        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("background")]
        public string Background { get; set; } = string.Empty;

        public static HumanCandidateDocument From(HumanCandidate candidate) => new()
        {
            Id = candidate.Id,
            Name = candidate.Name,
            Background = candidate.Background
        };
    }

    private sealed class ConsiderationDocument : RecruitmentRecordDocument
    {
        [BsonElement("preparedAt")]
        public DateTimeOffset PreparedAt { get; set; }

        [BsonElement("roleId")]
        public Guid RoleId { get; set; }

        [BsonElement("roleName")]
        public string RoleName { get; set; } = string.Empty;

        [BsonElement("rolePurpose")]
        public string RolePurpose { get; set; } = string.Empty;

        [BsonElement("roleResponsibilities")]
        public string RoleResponsibilities { get; set; } = string.Empty;

        [BsonElement("roleCapabilities")]
        public string[] RoleCapabilities { get; set; } = [];

        [BsonElement("roleSuccessCriteria")]
        public string RoleSuccessCriteria { get; set; } = string.Empty;

        [BsonElement("participantId")]
        public Guid ParticipantId { get; set; }

        [BsonElement("participantKind")]
        public ParticipantKind ParticipantKind { get; set; }

        [BsonElement("participantName")]
        public string ParticipantName { get; set; } = string.Empty;

        [BsonElement("participantBackground")]
        public string ParticipantBackground { get; set; } = string.Empty;

        [BsonElement("participantPurpose")]
        public string ParticipantPurpose { get; set; } = string.Empty;

        [BsonElement("participantWorkingStyle")]
        public string ParticipantWorkingStyle { get; set; } = string.Empty;

        [BsonElement("participantStrengths")]
        public string ParticipantStrengths { get; set; } = string.Empty;

        [BsonElement("participantBoundaries")]
        public string ParticipantBoundaries { get; set; } = string.Empty;

        [BsonElement("evidence")]
        public string Evidence { get; set; } = string.Empty;

        [BsonElement("recommendation")]
        public string Recommendation { get; set; } = string.Empty;

        [BsonElement("destinationId")]
        public string DestinationId { get; set; } = string.Empty;

        [BsonElement("destinationName")]
        public string DestinationName { get; set; } = string.Empty;

        public static ConsiderationDocument From(Consideration consideration) => new()
        {
            Id = consideration.Id,
            PreparedAt = consideration.PreparedAt,
            RoleId = consideration.Role.Id,
            RoleName = consideration.Role.Name,
            RolePurpose = consideration.Role.Purpose,
            RoleResponsibilities = consideration.Role.Responsibilities,
            RoleCapabilities = consideration.Role.RequiredCapabilities,
            RoleSuccessCriteria = consideration.Role.SuccessCriteria,
            ParticipantId = consideration.Participant.Id,
            ParticipantKind = consideration.Participant.Kind,
            ParticipantName = consideration.Participant.Name,
            ParticipantBackground = consideration.Participant.Background,
            ParticipantPurpose = consideration.Participant.Purpose,
            ParticipantWorkingStyle = consideration.Participant.WorkingStyle,
            ParticipantStrengths = consideration.Participant.Strengths,
            ParticipantBoundaries = consideration.Participant.Boundaries,
            Evidence = consideration.Evidence,
            Recommendation = consideration.Recommendation,
            DestinationId = consideration.Destination.Id,
            DestinationName = consideration.Destination.Name
        };
    }
}

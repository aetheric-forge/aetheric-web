using System.Text.Json;
using Forge.Primitives.MongoDb;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;

namespace AethericForge.Web.Marketing;

public sealed class MongoCampaignCatalog : ICampaignCatalog
{
    private readonly IMongoCollection<BsonDocument> publications;
    private readonly IMongoCollection<BsonDocument> offerings;
    private Dictionary<string, CampaignFeaturedOffering> offeringSnapshot = [];
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly TimeSpan refreshInterval;
    private Dictionary<string, CampaignPageContent>? snapshot;
    private DateTimeOffset loadedAt;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public MongoCampaignCatalog(MongoOptions options) : this(new MongoClient(options.ToConnectionString()).GetDatabase(options.DatabaseName)) { }
    public MongoCampaignCatalog(IMongoDatabase database, TimeSpan? refreshInterval = null)
    {
        publications = database.GetCollection<BsonDocument>("marketingPublications");
        offerings = database.GetCollection<BsonDocument>("offerings");
        this.refreshInterval = refreshInterval ?? TimeSpan.FromSeconds(30);
    }
    public async Task<CampaignPageContent?> GetActiveAsync(string intakePath, CancellationToken cancellationToken = default)
    {
        var path = intakePath == "/" ? "/" : intakePath.TrimEnd('/').ToLowerInvariant();
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (snapshot is null || DateTimeOffset.UtcNow - loadedAt >= refreshInterval)
            {
                var filter = Builders<BsonDocument>.Filter.Eq("websiteId", "aetheric-forge") &
                    Builders<BsonDocument>.Filter.Eq("schemaVersion", 1) & Builders<BsonDocument>.Filter.Eq("isActive", true);
                var documents = await publications.Find(filter)
                    .Project(Builders<BsonDocument>.Projection.Include("intakePath").Include("content").Exclude("_id"))
                    .ToListAsync(cancellationToken);
                var next = new Dictionary<string, CampaignPageContent>(StringComparer.Ordinal);
                foreach (var document in documents)
                {
                    var content = DecodeContent(document["content"].AsBsonDocument);
                    next[document["intakePath"].AsString] = content;
                }
                var ids = next.Values.SelectMany(content => content.OfferingIds ?? []).Distinct().ToArray();
                var offeringDocuments = ids.Length == 0 ? new List<BsonDocument>() : await offerings
                    .Find(Builders<BsonDocument>.Filter.In("_id", ids))
                    .Project(Builders<BsonDocument>.Projection.Include("name").Include("summary").Include("deliverables"))
                    .ToListAsync(cancellationToken);
                var nextOfferings = new Dictionary<string, CampaignFeaturedOffering>();
                foreach (var item in offeringDocuments)
                {
                    var deliverables = item.GetValue("deliverables", new BsonArray()).AsBsonArray.Select(value => value.AsString).ToArray();
                    nextOfferings[item["_id"].AsString] = new(item["name"].AsString, item["summary"].AsString, deliverables);
                }
                offeringSnapshot = nextOfferings;
                snapshot = next; loadedAt = DateTimeOffset.UtcNow;
            }
            return snapshot.GetValueOrDefault(path);
        }
        finally { gate.Release(); }
    }
    public async Task<IReadOnlyList<CampaignFeaturedOffering>> GetFeaturedOfferingsAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
    {
        await GetActiveAsync("/projects", cancellationToken);
        return ids.Where(offeringSnapshot.ContainsKey).Select(id => offeringSnapshot[id]).ToArray();
    }
    public static CampaignPageContent DecodeContent(BsonDocument document)
    {
        var content = JsonSerializer.Deserialize<CampaignPageContent>(document.ToJson(new JsonWriterSettings
            { OutputMode = JsonOutputMode.RelaxedExtendedJson }), JsonOptions) ?? throw new InvalidDataException("Campaign content is missing.");
        if (string.IsNullOrWhiteSpace(content.Headline) || string.IsNullOrWhiteSpace(content.Summary) ||
            string.IsNullOrWhiteSpace(content.Seo?.Title) || string.IsNullOrWhiteSpace(content.Seo?.Description) ||
            content.CallToAction is null || string.IsNullOrWhiteSpace(content.CallToAction.Label) || !SafeUrl(content.CallToAction.Href) ||
            !SafeImage(content.HeroImage) || !SafeImage(content.Seo?.SocialImage))
            throw new InvalidDataException("The published campaign has invalid page content.");
        return content;
    }
    private static bool SafeImage(CampaignPageImage? image) => image is null ||
        (SafeUrl(image.Url) && !string.IsNullOrWhiteSpace(image.AlternativeText));
    private static bool SafeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c == '\u005c')) return false;
        if (value.StartsWith('/') && !value.StartsWith("//")) return true;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo);
    }
}

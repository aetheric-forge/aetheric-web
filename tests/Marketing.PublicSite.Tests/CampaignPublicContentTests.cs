using System.Text.Json;
using AethericForge.Web.Marketing;
using AethericForge.Web.Components.Shared;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AethericForge.Web.Tests;
public sealed class CampaignPublicContentTests
{
    [Fact]
    public void StructuredDataCannotCloseItsScriptElement()
    {
        var input = JsonSerializer.Serialize(new { name = "</script><script>alert(1)</script>" });
        var output = CampaignHead.SafeStructuredData(input)!;
        Assert.DoesNotContain("</script>", output);
        Assert.Equal("</script><script>alert(1)</script>", JsonDocument.Parse(output).RootElement.GetProperty("name").GetString());
        Assert.Null(CampaignHead.SafeStructuredData("invalid json"));
    }
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("//example.com")]
    [InlineData("https://user:password@example.com")]
    public void UnsafeLinksAreRejectedAtPublicBoundary(string href)
    {
        var document = BsonDocument.Parse(JsonSerializer.Serialize(new {
            headline="Headline", summary="Summary", callToAction=new { label="Contact", href }, seo=new { title="Title", description="Description" }
        }));
        Assert.Throws<InvalidDataException>(() => MongoCampaignCatalog.DecodeContent(document));
    }
    [Fact]
    public async Task EmptyCatalogPreservesBaselineWithoutMongo()
    {
        Assert.Null(await new EmptyCampaignCatalog().GetActiveAsync("/projects"));
    }
}

public sealed class CampaignReaderIntegrationTests
{
    [Fact]
    public async Task ReaderFiltersByWebsiteAndStateAndEndingRestoresEmptyResult()
    {
        var client = new MongoDB.Driver.MongoClient(Environment.GetEnvironmentVariable("MONGO_TEST_CONNECTION") ?? "mongodb://localhost:27217");
        var database = client.GetDatabase($"marketing-reader-tests-{Guid.NewGuid():N}");
        try
        {
            var collection = database.GetCollection<BsonDocument>("marketingPublications");
            BsonDocument Publication(string id, string website, bool active) => BsonDocument.Parse(JsonSerializer.Serialize(new {
                _id=id, schemaVersion=1, websiteId=website, intakePath="/projects", isActive=active,
                notes="PRIVATE ROOT VALUE", content=new {
                    headline="Published",summary="Public summary",offeringIds=new[]{"featured","deleted"},
                    callToAction=new {label="Contact",href="https://example.com/contact"},seo=new{title="Title",description="Description"}
                }
            }));
            await collection.InsertOneAsync(Publication("other", "blackcircuit", true));
            await collection.InsertOneAsync(Publication("ended", "aetheric-forge", false));
            await collection.InsertOneAsync(Publication("live", "aetheric-forge", true));
            await database.GetCollection<BsonDocument>("offerings").InsertOneAsync(new BsonDocument {
                ["_id"]="featured",["name"]="Featured offering",["summary"]="Available now",["deliverables"]=new BsonArray {"Consultation"}
            });
            var catalog = new MongoCampaignCatalog(database, TimeSpan.Zero);
            var content = await catalog.GetActiveAsync("/PROJECTS/");
            Assert.Equal("Published", content!.Headline);
            Assert.Equal("Featured offering", Assert.Single(await catalog.GetFeaturedOfferingsAsync(content.OfferingIds!)).Name);
            await collection.UpdateOneAsync(new BsonDocument("_id","live"), new BsonDocument("$set",new BsonDocument("isActive",false)));
            Assert.Null(await catalog.GetActiveAsync("/projects"));
        }
        finally { await client.DropDatabaseAsync(database.DatabaseNamespace.DatabaseName); }
    }
}

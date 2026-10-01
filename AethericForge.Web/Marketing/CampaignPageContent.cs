namespace AethericForge.Web.Marketing;

// Host-owned read model. Only published content crosses this boundary.
public sealed record CampaignPageContent(string? Headline, string? Summary, string? Body,
    CampaignPageAction? CallToAction, CampaignPageImage? HeroImage, IReadOnlyList<string>? OfferingIds, CampaignPageSeo? Seo);
public sealed record CampaignPageAction(string Label, string Href);
public sealed record CampaignPageImage(string Url, string AlternativeText);
public sealed record CampaignPageSeo(string? Title, string? Description, string? SocialTitle,
    string? SocialDescription, CampaignPageImage? SocialImage, string? StructuredDataJson);
public sealed record CampaignFeaturedOffering(string Name, string Summary, IReadOnlyList<string> Deliverables);
public interface ICampaignCatalog
{
    Task<CampaignPageContent?> GetActiveAsync(string intakePath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CampaignFeaturedOffering>> GetFeaturedOfferingsAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CampaignFeaturedOffering>>([]);
}
public sealed class EmptyCampaignCatalog : ICampaignCatalog
{
    public Task<CampaignPageContent?> GetActiveAsync(string intakePath, CancellationToken cancellationToken = default) =>
        Task.FromResult<CampaignPageContent?>(null);
}

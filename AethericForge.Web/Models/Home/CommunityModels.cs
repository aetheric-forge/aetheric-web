namespace AethericForge.Web.Models.Home;

public sealed class CommunityAudienceCardModel
{
    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;
}

public sealed class CommunitySectionModel
{
    public SectionHeaderModel Header { get; init; } = new();

    public IReadOnlyList<CommunityAudienceCardModel> Audiences { get; init; } = new List<CommunityAudienceCardModel>();
}
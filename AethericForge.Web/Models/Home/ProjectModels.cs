namespace AethericForge.Web.Models.Home;

public sealed class ProjectCardModel
{
    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;
}

public sealed class ProjectsSectionModel
{
    public SectionHeaderModel Header { get; init; } = new();

    public IReadOnlyList<ProjectCardModel> Projects { get; init; } = new List<ProjectCardModel>();
}
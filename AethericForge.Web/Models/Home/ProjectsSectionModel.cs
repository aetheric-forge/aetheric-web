namespace AethericForge.Web.Models.Home;

using AethericForge.Web.Models.Shared;

public sealed class ProjectsSectionModel
{
    public SectionHeaderModel Header { get; init; } = new();

    public IReadOnlyList<CardModel> Projects { get; init; } = new List<CardModel>();
}
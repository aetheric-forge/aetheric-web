namespace AethericForge.Web.Models.Home;

using AethericForge.Web.Models.Shared;

public sealed class ResourcesSectionModel
{
    public SectionHeaderModel Header { get; init; } = new();

    public IReadOnlyList<CardModel> Cards { get; init; }
        = new List<CardModel>();
}
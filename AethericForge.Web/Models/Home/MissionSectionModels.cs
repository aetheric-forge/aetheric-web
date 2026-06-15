namespace AethericForge.Web.Models.Home;

public sealed class MissionSectionModel
{
    public SectionHeaderModel Header { get; init; } = new();
    public string Description { get; init; } = string.Empty;

    public IReadOnlyList<MissionPillarModel> Pillars { get; init; } = new List<MissionPillarModel>();
}

public sealed class MissionPillarModel
{
    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
}
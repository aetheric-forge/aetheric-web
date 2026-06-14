namespace AethericForge.Web.Models.Home;

public sealed class HomePageModel
{
    public HeroModel Hero { get; init; } = new();
    public MissionSectionModel Mission { get; init; } = new();
    public ProjectsSectionModel Projects { get; init; } = new();
    public SectionHeaderModel CommunityHeader { get; init; } = new();
}
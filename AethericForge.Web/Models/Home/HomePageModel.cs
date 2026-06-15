namespace AethericForge.Web.Models.Home;

using AethericForge.Web.Models.Shared;

public sealed class HomePageModel
{
    public HeroModel Hero { get; init; } = new();
    public MissionSectionModel Mission { get; init; } = new();
    public ProjectsSectionModel Projects { get; init; } = new();
    public CommunitySectionModel Community { get; init; } = new();
    public CallToActionSectionModel CallToAction { get; init; } = new();
}
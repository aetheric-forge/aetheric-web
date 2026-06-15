namespace AethericForge.Web.Models.Home;

using AethericForge.Web.Models.Shared;

public sealed class HomePageModel
{
    public HeroModel Hero { get; init; } = new();
    public ProjectsSectionModel ProjectsSection { get; init; } = new();
    public MissionSectionModel MissionSection { get; init; } = new();
    public ResourcesSectionModel ResourcesSection { get; init; } = new();
    public CallToActionSectionModel CallToActionSection { get; init; } = new();
}
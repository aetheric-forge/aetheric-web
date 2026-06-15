namespace AethericForge.Web.Models.Shared;

public sealed class HeroModel
{
    public string Title { get; init; } = string.Empty;

    public string? Eyebrow { get; init; }

    public string Subtitle { get; init; } = string.Empty;

    public ActionLinkModel PrimaryAction { get; init; } = new();

    public ActionLinkModel? SecondaryAction { get; init; }
}

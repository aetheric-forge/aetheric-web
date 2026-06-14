namespace AethericForge.Web.Models;

public sealed class HeroModel
{
    public string Title { get; init; } = string.Empty;

    public string? Eyebrow { get; init; }

    public string Subtitle { get; init; } = string.Empty;

    public HeroActionModel? PrimaryAction { get; init; }

    public HeroActionModel? SecondaryAction { get; init; }
}

public sealed class HeroActionModel
{
    public string Text { get; init; } = string.Empty;

    public string Href { get; init; } = string.Empty;
}
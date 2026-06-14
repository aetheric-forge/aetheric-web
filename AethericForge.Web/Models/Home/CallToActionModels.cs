namespace AethericForge.Web.Models.Home;

public sealed class CallToActionSectionModel
{
    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public ActionLinkModel PrimaryAction { get; init; } = new();

    public ActionLinkModel? SecondaryAction { get; init; }
}

public sealed class ActionLinkModel
{
    public string Label { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public bool IsPrimary { get; init; }
}
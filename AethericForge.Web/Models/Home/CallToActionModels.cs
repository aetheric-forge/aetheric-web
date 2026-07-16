namespace AethericForge.Web.Models.Home;

using AethericForge.Web.Models.Shared;

public sealed class CallToActionSectionModel
{
    public SectionHeaderModel Header { get; init; } = new();

    public string Description { get; init; } = string.Empty;

    public ActionLinkModel PrimaryAction { get; init; } = new();

    public ActionLinkModel? SecondaryAction { get; init; }
}

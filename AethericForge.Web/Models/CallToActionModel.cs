namespace AethericForge.Web.Models;

public sealed class CallToActionModel
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    public string PrimaryText { get; init; } = string.Empty;
    public string PrimaryLink { get; init; } = string.Empty;
}
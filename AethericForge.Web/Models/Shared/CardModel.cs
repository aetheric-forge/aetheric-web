namespace AethericForge.Web.Models.Shared;

public sealed class CardModel
{
    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public ActionLinkModel? Action { get; init; }
}
namespace AethericForge.Web.Hosting;

/// <summary>What the shell actually hosts, so navigation never links to a disabled route.</summary>
public sealed record ShellNavigation(bool FullCampus, IReadOnlySet<string> EnabledModules)
{
    public bool HasModule(string id) => EnabledModules.Contains(id);
}

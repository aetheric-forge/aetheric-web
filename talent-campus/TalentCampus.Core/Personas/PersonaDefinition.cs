namespace TalentCampus.Core.Personas;

/// <summary>A reusable description of AI talent, independent of roles and agent execution.</summary>
public sealed class PersonaDefinition
{
    public PersonaDefinition(Guid id, string name, string purpose, string workingStyle,
        string strengths, string boundaries)
    {
        if (id == Guid.Empty) throw new ArgumentException("A persona requires an identity.", nameof(id));
        Id = id;
        Name = Required(name, nameof(name));
        Purpose = Required(purpose, nameof(purpose));
        WorkingStyle = Required(workingStyle, nameof(workingStyle));
        Strengths = Required(strengths, nameof(strengths));
        Boundaries = Required(boundaries, nameof(boundaries));
    }

    public Guid Id { get; }
    public string Name { get; }
    public string Purpose { get; }
    public string WorkingStyle { get; }
    public string Strengths { get; }
    public string Boundaries { get; }

    private static string Required(string value, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        return value.Trim();
    }
}

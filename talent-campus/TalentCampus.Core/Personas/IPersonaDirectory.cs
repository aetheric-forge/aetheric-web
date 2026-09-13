namespace TalentCampus.Core.Personas;

public interface IPersonaDirectory
{
    PersonaDefinition Create(string name, string purpose, string workingStyle, string strengths, string boundaries);
    IReadOnlyCollection<PersonaDefinition> List();
}

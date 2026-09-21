namespace TalentCampus.Core.Recruitment;

public enum ParticipantKind { Human, AiPersona }
public sealed record HumanCandidate(Guid Id, string Name, string Background);
public sealed record DecisionsDestination(string Id, string Name);
public sealed record RoleSnapshot(Guid Id, string Name, string Purpose, string Responsibilities,
    string[] RequiredCapabilities, string SuccessCriteria);
public sealed record ParticipantSnapshot(Guid Id, ParticipantKind Kind, string Name,
    string Background, string Purpose, string WorkingStyle, string Strengths, string Boundaries);

/// <summary>A frozen submission prepared locally; postal custody begins in M4.</summary>
public sealed record Consideration(Guid Id, DateTimeOffset PreparedAt, RoleSnapshot Role,
    ParticipantSnapshot Participant, string Evidence, string Recommendation, DecisionsDestination Destination);

public interface IRecruitmentOffice
{
    HumanCandidate RegisterHuman(string name, string background);
    IReadOnlyCollection<HumanCandidate> ListHumans();
    IReadOnlyCollection<DecisionsDestination> ListDestinations();
    Consideration Prepare(Guid roleId, ParticipantKind kind, Guid participantId,
        string evidence, string recommendation, string destinationId);
    IReadOnlyCollection<Consideration> ListConsiderations();
}

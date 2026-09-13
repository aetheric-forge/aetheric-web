using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using TalentCampus.Core.Roles;

namespace TalentCampus.Institutions.Talent;

public interface ITalent : IInstitution
{
    ITalentSteward Steward { get; }
}

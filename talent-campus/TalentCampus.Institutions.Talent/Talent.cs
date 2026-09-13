using AethericForge.Runtime.Models.Institutions;
using TalentCampus.Core.Roles;

namespace TalentCampus.Institutions.Talent;

public sealed class Talent(ITalentContext context, ITalentSteward steward)
    : InstitutionBase(context), ITalent
{
    public new ITalentContext Context => (ITalentContext)base.Context;

    public ITalentSteward Steward { get; } =
        steward ?? throw new ArgumentNullException(nameof(steward));
}

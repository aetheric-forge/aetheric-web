using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;
using AethericForge.Runtime.Models.Institutions;

namespace TalentCampus.Institutions.Talent;

public sealed class TalentContext(
    IInstitutionTemplate template,
    IServiceProvider services,
    IInstitution parent)
    : InstitutionContext(template, services, parent), ITalentContext;

using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Web.Decisions.Institution;

public sealed class DecisionsContext(
    IInstitutionTemplate template,
    IServiceProvider services,
    IInstitution parent)
    : InstitutionContext(template, services, parent);

public sealed class DecisionsInstitution(DecisionsContext context)
    : InstitutionBase(context), IDecisions;

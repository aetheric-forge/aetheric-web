using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;
using AethericForge.Runtime.Models.Institutions;
using ParallelYou.Abstractions;

namespace ParallelYou.Services;

public sealed class ParallelYouContext(
    IInstitutionTemplate template,
    IServiceProvider services,
    IInstitution parent)
    : InstitutionContext(template, services, parent), IParallelYouContext;

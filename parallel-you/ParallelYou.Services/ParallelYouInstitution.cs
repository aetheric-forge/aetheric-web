using AethericForge.Runtime.Models.Institutions;
using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Capture;
using ParallelYou.Abstractions.Intention;
using ParallelYou.Abstractions.Person;
using ParallelYou.Abstractions.Plan;
using ParallelYou.Abstractions.Recommendation;
using ParallelYou.Abstractions.Reflection;
using ParallelYou.Abstractions.Tracking;

namespace ParallelYou.Services;

public sealed class ParallelYouInstitution(
    IParallelYouContext context,
    ICaptureService capture,
    IPersonService people,
    IReflectionService reflection,
    ITrackingService tracking,
    IIntentionService intentions,
    IPlanningService planning,
    IRecommendationService recommendations)
    : InstitutionBase(context), IParallelYou
{
    public new IParallelYouContext Context { get; } = context;
    public ICaptureService Capture { get; } = capture;
    public IPersonService People { get; } = people;
    public IReflectionService Reflection { get; } = reflection;
    public ITrackingService Tracking { get; } = tracking;
    public IIntentionService Intentions { get; } = intentions;
    public IPlanningService Planning { get; } = planning;
    public IRecommendationService Recommendations { get; } = recommendations;
}

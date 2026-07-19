using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using ParallelYou.Abstractions.Capture;
using ParallelYou.Abstractions.Intention;
using ParallelYou.Abstractions.Person;
using ParallelYou.Abstractions.Plan;
using ParallelYou.Abstractions.Recommendation;
using ParallelYou.Abstractions.Reflection;
using ParallelYou.Abstractions.Tracking;

namespace ParallelYou.Abstractions;

/// <summary>
/// The Parallel You institution and the capabilities it contributes to a campus.
/// </summary>
public interface IParallelYou : IInstitution
{
    ICaptureService Capture { get; }
    IPersonService People { get; }
    IReflectionService Reflection { get; }
    ITrackingService Tracking { get; }
    IIntentionService Intentions { get; }
    IPlanningService Planning { get; }
    IRecommendationService Recommendations { get; }
}

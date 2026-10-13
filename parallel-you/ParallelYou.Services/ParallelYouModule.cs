using AethericForge.Runtime.Abstractions.Interfaces.Library.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Workbench.Services;
using Microsoft.Extensions.DependencyInjection;
using AethericForge.Runtime.Institutions.Abstractions.Builders;
using AethericForge.Runtime.Institutions.Abstractions.Models;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;
using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Capture;
using ParallelYou.Abstractions.Intention;
using ParallelYou.Abstractions.Person;
using ParallelYou.Abstractions.Plan;
using ParallelYou.Abstractions.Recommendation;
using ParallelYou.Abstractions.Reflection;
using ParallelYou.Abstractions.Tracking;
using ParallelYou.Services.Capture;
using ParallelYou.Services.Intention;
using ParallelYou.Services.Person;
using ParallelYou.Services.Plan;
using ParallelYou.Services.Recommendation;
using ParallelYou.Services.Reflection;
using ParallelYou.Services.Tracking;

namespace ParallelYou.Services;

/// <summary>Composes the institution using capabilities supplied by its deployment host.</summary>
public static class ParallelYouModule
{
    public static IInstitutionTemplate CreateTemplate(string name = "ParallelYou", string version = "1.0.0",
        string description = "A private practice for preserving observations, reflection, and chosen direction.") => InstitutionTemplateBuilder.Create()
        .WithDescriptor(name, Version.Parse(version), description)
        .AddResource(new ResourceDefinition("Personal Library", "knowledge", "Durable personal artifacts and their representations."))
        .Build();

    public static readonly IReadOnlyList<string> Stages = Array.AsReadOnly(new[]
    {
        "ReflectionMapping", "TrackingCurrent", "IntentionCurrent", "PlanCurrent", "RecommendationCurrent"
    });

    public static IParallelYou Create(IParallelYouContext context, ILibrarian librarian, IArtificer artificer)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(librarian);
        ArgumentNullException.ThrowIfNull(artificer);
        var intentions = new IntentionService(librarian, artificer);
        var planning = new PlanningService(librarian, artificer, intentions);
        return new ParallelYouInstitution(context,
            new CaptureService(librarian), new PersonService(librarian),
            new ReflectionService(librarian, artificer), new TrackingService(librarian, artificer),
            intentions, planning, new RecommendationService(librarian, artificer, planning));
    }

    // Export only the institution's capabilities. Its Library and Workbench never replace
    // the host's shared services, so another module cannot redirect Parallel You's writes.
    public static IServiceCollection AddParallelYouModule(this IServiceCollection services,
        Func<IServiceProvider, IParallelYou> factory)
    {
        services.AddSingleton(factory);
        services.AddSingleton<ICaptureService>(sp => sp.GetRequiredService<IParallelYou>().Capture);
        services.AddSingleton<IPersonService>(sp => sp.GetRequiredService<IParallelYou>().People);
        services.AddSingleton<IReflectionService>(sp => sp.GetRequiredService<IParallelYou>().Reflection);
        services.AddSingleton<ITrackingService>(sp => sp.GetRequiredService<IParallelYou>().Tracking);
        services.AddSingleton<IIntentionService>(sp => sp.GetRequiredService<IParallelYou>().Intentions);
        services.AddSingleton<IPlanningService>(sp => sp.GetRequiredService<IParallelYou>().Planning);
        services.AddSingleton<IRecommendationService>(sp => sp.GetRequiredService<IParallelYou>().Recommendations);
        return services;
    }
}

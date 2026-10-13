using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Library.Services;
using AethericForge.Runtime.Institutions.Abstractions.Builders;
using AethericForge.Runtime.Models.Identity.Primitives;
using AethericForge.Runtime.Models.Knowledge.Authorities;
using AethericForge.Runtime.Providers.Knowledge.InMemory;
using AethericForge.Runtime.Providers.Staging.InMemory;
using AethericForge.Web.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Capture;
using ParallelYou.Abstractions.Intention;
using ParallelYou.Abstractions.Plan;
using ParallelYou.Abstractions.Recommendation;
using ParallelYou.Abstractions.Reflection;
using ParallelYou.Abstractions.Tracking;
using ParallelYou.Models.Capture;
using ParallelYou.Models.Intention;
using ParallelYou.Models.Plan;
using ParallelYou.Models.Recommendation;
using ParallelYou.Models.Reflection;
using ParallelYou.Models.Tracking;
using ParallelYou.Services;
using Xunit;

namespace AethericForge.Web.Tests;

public class ParallelYouModuleTests
{
    private static IParallelYou Create(IServiceProvider services, InMemoryKnowledgeProvider knowledge,
        InMemoryStagingProvider[] staging) => ParallelYouModule.Create(new ParallelYouContext(
            InstitutionTemplateBuilder.Create().WithDescriptor("ParallelYou", new Version(1, 0, 0), "Test institution").Build(),
            services), ParallelYouHosting.CreateLibrarian(knowledge), ParallelYouHosting.CreateArtificer(staging));

    [Fact]
    public async Task Module_uses_its_own_library_and_preserves_identity_isolation_after_recomposition()
    {
        var moduleKnowledge = new InMemoryKnowledgeProvider("parallel-you");
        var campusKnowledge = new InMemoryKnowledgeProvider("ForgeCampus");
        var staging = ParallelYouModule.Stages.Select(x => new InMemoryStagingProvider(x)).ToArray();
        var services = new ServiceCollection();
        var campusLibrarian = ParallelYouHosting.CreateLibrarian(campusKnowledge);
        services.AddSingleton<ILibrarian>(campusLibrarian);
        services.AddParallelYouModule(sp => Create(sp, moduleKnowledge, staging));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var module = provider.GetRequiredService<IParallelYou>();
        var owner = new IdentitySubject("owner", IdentityScheme.OpenIdConnect);
        var other = new IdentitySubject("other", IdentityScheme.OpenIdConnect);
        var authority = new KnowledgeAuthority(owner, CaptureAuthority.Context);
        var artifact = await provider.GetRequiredService<ICaptureService>().CaptureAsync(new CaptureEvidence("Recording", "Conversations At The Forge"), authority);
        Assert.Equal("parallel-you", artifact.Reference.Scheme);
        Assert.Empty(await campusLibrarian.FindArtifactsAsync(authority));
        Assert.Empty(await module.Capture.GetCapturesAsync(new KnowledgeAuthority(other, CaptureAuthority.Context)));
        Assert.Same(campusLibrarian, provider.GetRequiredService<ILibrarian>());
        Assert.Same(module.Capture, provider.GetRequiredService<ICaptureService>());
        var person = await module.People.GetOrCreatePersonAsync(owner);
        var recomposed = Create(provider, moduleKnowledge, staging);
        Assert.Equal(person.Id, (await recomposed.People.GetOrCreatePersonAsync(owner)).Id);
        Assert.Single(await recomposed.Capture.GetCapturesAsync(authority));
    }

    [Fact]
    public async Task Planning_composes_the_same_intention_service_and_retains_workbench_state()
    {
        var knowledge = new InMemoryKnowledgeProvider("parallel-you");
        var staging = ParallelYouModule.Stages.Select(x => new InMemoryStagingProvider(x)).ToArray();
        var services = new ServiceCollection().AddParallelYouModule(sp => Create(sp, knowledge, staging));
        using var provider = services.BuildServiceProvider();
        var owner = new IdentitySubject("owner", IdentityScheme.OpenIdConnect);
        var module = provider.GetRequiredService<IParallelYou>();
        var person = await module.People.GetOrCreatePersonAsync(owner);
        var provenance = new Provenance(ProvenanceKind.Observed, "recording", DateTimeOffset.UtcNow);
        var intentionAuthority = new KnowledgeAuthority(owner, IntentionAuthority.Context);
        var intention = await provider.GetRequiredService<IIntentionService>().CreateIntentionAsync(person.Id,
            new IntentionSubmission("Record Conversations At The Forge", provenance, true), intentionAuthority);
        var planAuthority = new KnowledgeAuthority(owner, PlanAuthority.Context);
        var plan = await provider.GetRequiredService<IPlanningService>().CreatePlanAsync(person.Id,
            new PlanSubmission("Episode", "Prepare and record", [intention.Id], provenance, true), planAuthority);
        var reflectionAuthority = new KnowledgeAuthority(owner, ReflectionAuthority.Context);
        var reflection = await module.Reflection.StartReflectionAsync(person.Id,
            new ReflectionSubject { Id = intention.Id, Type = "intention", Description = "The recording" },
            ["What is ready?"], reflectionAuthority);
        var trackingAuthority = new KnowledgeAuthority(owner, TrackingAuthority.Context);
        var subject = new TrackedSubject(Guid.NewGuid(), "recording", "Episode readiness");
        await module.Tracking.TrackAsync(new TrackedState(Guid.NewGuid(), person.Id, subject, "ready", provenance, 1m), trackingAuthority);
        var recommendationAuthority = new KnowledgeAuthority(owner, RecommendationAuthority.Context);
        await module.Recommendations.PresentRecommendationAsync(person.Id,
            new RecommendationSubmission(new RecommendationSubject(plan.Id, "Record the episode"),
                [new RecommendationCandidate(Guid.NewGuid(), plan.Id, RecommendationCandidateKind.ConsiderPlan, "Review the plan")],
                "The preparation is ready", [], 1m, provenance), recommendationAuthority);
        var recomposed = Create(provider, knowledge, staging);
        Assert.Single(await recomposed.Intentions.GetIntentionsAsync(person.Id, intentionAuthority));
        Assert.Single(await recomposed.Planning.GetPlansAsync(person.Id, planAuthority));
        Assert.Equal(person.Id, plan.PersonId);
        Assert.Equal("ready", (await recomposed.Tracking.GetCurrentStateAsync(person.Id, subject.Id, trackingAuthority))?.Value);
        Assert.Single(await recomposed.Recommendations.GetRecommendationsAsync(person.Id, recommendationAuthority));
        Assert.True(await recomposed.Reflection.AddEvidenceAsync(reflection.Id,
            new ReflectionEvidence { Id = Guid.NewGuid(), Content = "The module is ready", Provenance = provenance }, reflectionAuthority));
    }
}

using System.Text.Json;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Library.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Workbench.Services;
using AethericForge.Runtime.Models.Knowledge.Primitives;
using AethericForge.Runtime.Models.Knowledge.Authorities;
using AethericForge.Runtime.Models.Knowledge.Representations;
using ParallelYou.Abstractions.Intention;
using ParallelYou.Abstractions.Plan;
using ParallelYou.Models.Plan;

namespace ParallelYou.Services.Plan;

public sealed class PlanningService(
    ILibrarian librarian,
    IArtificer artificer,
    IIntentionService intentionService) : ServiceBase, IPlanningService
{
    private const string CurrentPlanStage = "PlanCurrent";

    public async Task<IPlan> CreatePlanAsync(
        Guid personId,
        IPlanSubmission submission,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default)
    {
        ValidatePersonId(personId);
        ValidateSubmission(submission);
        ValidateAuthority(authority);
        await ValidateRelatedIntentionsAsync(
            personId,
            submission.IntentionIds,
            authority,
            cancellationToken);

        var planId = Guid.NewGuid();
        var plan = CreateModel(personId, planId, submission);
        await PublishPlanAsync(
            plan,
            authority,
            previous: null,
            cancellationToken);

        return plan;
    }

    public async Task<IPlan?> RevisePlanAsync(
        Guid personId,
        Guid planId,
        IPlanSubmission submission,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default)
    {
        ValidateIds(personId, planId);
        ValidateSubmission(submission);
        ValidateAuthority(authority);
        await ValidateRelatedIntentionsAsync(
            personId,
            submission.IntentionIds,
            authority,
            cancellationToken);

        var previous = await GetCurrentArtifactAsync(
            personId,
            planId,
            authority,
            cancellationToken);
        if (previous is null)
        {
            return null;
        }

        var current = await DeserializePlanAsync(previous, cancellationToken);
        if (current is null ||
            current.PersonId != personId ||
            current.Id != planId)
        {
            return null;
        }

        var revision = CreateModel(personId, planId, submission);
        await PublishPlanAsync(
            revision,
            authority,
            previous,
            cancellationToken);

        return revision;
    }

    public async Task<IReadOnlyCollection<IPlan>> GetPlansAsync(
        Guid personId,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default)
    {
        ValidatePersonId(personId);
        ValidateAuthority(authority);
        var plans = await GetPlanArtifactsAsync(authority, cancellationToken);

        return plans
            .Where(item => item.Plan.PersonId == personId)
            .GroupBy(item => item.Plan.Id)
            .Select(group => group
                .OrderByDescending(item => item.UpdatedAtUtc)
                .First())
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => (IPlan)item.Plan)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<IPlanRepresentation>> GetHistoryAsync(
        Guid personId,
        Guid planId,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default)
    {
        ValidateIds(personId, planId);
        ValidateAuthority(authority);
        var plans = await GetPlanArtifactsAsync(authority, cancellationToken);

        return plans
            .Where(item =>
                item.Plan.PersonId == personId &&
                item.Plan.Id == planId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => (IPlanRepresentation)item.Plan.Representation)
            .ToArray();
    }

    private async Task<IKnowledgeArtifact?> GetCurrentArtifactAsync(
        Guid personId,
        Guid planId,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken)
    {
        var current = await CurrentArtifactResolver.ResolveAsync(
            librarian,
            artificer,
            CurrentPlanStage,
            GetCurrentPlanKey(authority, personId, planId),
            authority,
            DeserializePlanAsync,
            plan => plan.PersonId == personId && plan.Id == planId,
            cancellationToken);

        return current?.Artifact;
    }

    private async Task PublishPlanAsync(
        ParallelYou.Models.Plan.Plan plan,
        IKnowledgeAuthority authority,
        IKnowledgeArtifact? previous,
        CancellationToken cancellationToken)
    {
        var content = JsonSerializer.SerializeToUtf8Bytes(plan);
        var artifact = await librarian.PublishArtifactAsync(
            new KnowledgeDescriptor($"Plan_{plan.Id}"),
            [new KnowledgeRepresentation(
                "application/json",
                content.LongLength,
                _ => Task.FromResult<Stream>(new MemoryStream(content)),
                encoding: "utf-8")],
            previous is null ? null : [previous.Reference],
            authority,
            cancellationToken);

        await CurrentArtifactResolver.TryStoreReferenceAsync(
            artificer,
            CurrentPlanStage,
            GetCurrentPlanKey(authority, plan.PersonId, plan.Id),
            artifact.Reference,
            cancellationToken);
    }

    private async Task<List<PlanArtifact>> GetPlanArtifactsAsync(
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken)
    {
        var artifacts = await librarian.FindArtifactsAsync(
            authority,
            cancellationToken);
        var plans = new List<PlanArtifact>();

        foreach (var artifact in artifacts)
        {
            var plan = await DeserializePlanAsync(artifact, cancellationToken);
            if (plan is not null)
            {
                plans.Add(new PlanArtifact(plan, artifact.UpdatedAtUtc));
            }
        }

        return plans;
    }

    private static async Task<ParallelYou.Models.Plan.Plan?> DeserializePlanAsync(
        IKnowledgeArtifact artifact,
        CancellationToken cancellationToken)
    {
        var representation = artifact.Representations.FirstOrDefault(candidate =>
            string.Equals(
                candidate.ContentType,
                "application/json",
                StringComparison.OrdinalIgnoreCase));
        if (representation is null)
        {
            return null;
        }

        await using var stream = await representation.OpenStreamAsync(
            cancellationToken);
        return await JsonSerializer.DeserializeAsync<ParallelYou.Models.Plan.Plan>(
            stream,
            cancellationToken: cancellationToken);
    }

    private static ParallelYou.Models.Plan.Plan CreateModel(
        Guid personId,
        Guid planId,
        IPlanSubmission submission)
        => new(
            planId,
            personId,
            new PlanRepresentation(
                Guid.NewGuid(),
                planId,
                submission.Provenance,
                submission.DisplayName.Trim(),
                submission.Course.Trim(),
                submission.IntentionIds.Distinct().ToArray(),
                submission.IsAccepted));

    private async Task ValidateRelatedIntentionsAsync(
        Guid personId,
        IReadOnlyCollection<Guid> intentionIds,
        IKnowledgeAuthority planAuthority,
        CancellationToken cancellationToken)
    {
        var intentionAuthority = new KnowledgeAuthority(
            planAuthority.Identity,
            IntentionAuthority.Context);
        var availableIntentions = await intentionService.GetIntentionsAsync(
            personId,
            intentionAuthority,
            cancellationToken);
        var requestedIds = intentionIds.Distinct().ToHashSet();
        var adoptedIds = availableIntentions
            .Where(intention => intention.Representation.IsAdopted)
            .Select(intention => intention.Id)
            .ToHashSet();

        if (!requestedIds.IsSubsetOf(adoptedIds))
        {
            throw new ArgumentException(
                "A Plan may only serve adopted Intentions belonging to the Person.",
                nameof(intentionIds));
        }
    }

    private static void ValidateSubmission(IPlanSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(submission.Provenance);
        ArgumentNullException.ThrowIfNull(submission.IntentionIds);
        if (string.IsNullOrWhiteSpace(submission.DisplayName))
        {
            throw new ArgumentException(
                "Plan display name cannot be empty.",
                nameof(submission));
        }

        if (string.IsNullOrWhiteSpace(submission.Course))
        {
            throw new ArgumentException(
                "Proposed course cannot be empty.",
                nameof(submission));
        }

        if (submission.IntentionIds.Count == 0 ||
            submission.IntentionIds.Any(id => id == Guid.Empty))
        {
            throw new ArgumentException(
                "A Plan must serve at least one valid Intention.",
                nameof(submission));
        }
    }

    private static void ValidatePersonId(Guid personId)
    {
        if (personId == Guid.Empty)
        {
            throw new ArgumentException(
                "Person ID cannot be empty.",
                nameof(personId));
        }
    }

    private static void ValidateIds(Guid personId, Guid planId)
    {
        ValidatePersonId(personId);
        if (planId == Guid.Empty)
        {
            throw new ArgumentException(
                "Plan ID cannot be empty.",
                nameof(planId));
        }
    }

    private static void ValidateAuthority(IKnowledgeAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
        if (!string.Equals(
                authority.Context,
                PlanAuthority.Context,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Plan authority context must be '{PlanAuthority.Context}'.",
                nameof(authority));
        }
    }

    private static string GetCurrentPlanKey(
        IKnowledgeAuthority authority,
        Guid personId,
        Guid planId)
        => $"{authority.Identity.Scheme}:{authority.Identity.SubjectId}:{personId:N}:{planId:N}";

    private sealed record PlanArtifact(
        ParallelYou.Models.Plan.Plan Plan,
        DateTimeOffset UpdatedAtUtc);
}

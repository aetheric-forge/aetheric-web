using System.Text.Json;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Library.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Workbench.Services;
using AethericForge.Runtime.Models.Knowledge.Authorities;
using AethericForge.Runtime.Models.Knowledge.Primitives;
using AethericForge.Runtime.Models.Knowledge.Representations;
using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Plan;
using ParallelYou.Abstractions.Recommendation;
using ParallelYou.Models.Recommendation;

namespace ParallelYou.Services.Recommendation;

public sealed class RecommendationService(
    ILibrarian librarian,
    IArtificer artificer,
    IPlanningService planningService) : ServiceBase, IRecommendationService
{
    private const string CurrentRecommendationStage = "RecommendationCurrent";

    public async Task<IRecommendation> PresentRecommendationAsync(
        Guid personId,
        IRecommendationSubmission submission,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default)
    {
        ValidatePersonId(personId);
        ValidateSubmission(submission);
        ValidateAuthority(authority);
        await ValidatePlansAsync(
            personId,
            submission,
            authority,
            cancellationToken);

        var recommendationId = Guid.NewGuid();
        var recommendation = CreateModel(
            personId,
            recommendationId,
            submission);
        await PublishRecommendationAsync(
            recommendation,
            authority,
            previous: null,
            cancellationToken);

        return recommendation;
    }

    public async Task<IRecommendation?> ReviseRecommendationAsync(
        Guid personId,
        Guid recommendationId,
        IRecommendationSubmission submission,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default)
    {
        ValidateIds(personId, recommendationId);
        ValidateSubmission(submission);
        ValidateAuthority(authority);
        await ValidatePlansAsync(
            personId,
            submission,
            authority,
            cancellationToken);

        var loaded = await GetCurrentRecommendationAsync(
            personId,
            recommendationId,
            authority,
            cancellationToken);
        if (loaded is null)
        {
            return null;
        }

        var revision = CreateModel(personId, recommendationId, submission);
        await PublishRecommendationAsync(
            revision,
            authority,
            loaded.Artifact,
            cancellationToken);

        return revision;
    }

    public async Task<IRecommendation?> RecordResponseAsync(
        Guid personId,
        Guid recommendationId,
        RecommendationResponse response,
        IReadOnlyCollection<Guid> selectedCandidateIds,
        Provenance responseProvenance,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default)
    {
        ValidateIds(personId, recommendationId);
        ArgumentNullException.ThrowIfNull(selectedCandidateIds);
        ArgumentNullException.ThrowIfNull(responseProvenance);
        ValidateAuthority(authority);
        if (response == RecommendationResponse.AwaitingResponse)
        {
            throw new ArgumentException(
                "A recorded response cannot be AwaitingResponse.",
                nameof(response));
        }

        var loaded = await GetCurrentRecommendationAsync(
            personId,
            recommendationId,
            authority,
            cancellationToken);
        if (loaded is null)
        {
            return null;
        }

        var current = loaded.Recommendation;
        var availableCandidateIds = current.Representation.Candidates
            .Select(candidate => candidate.Id)
            .ToHashSet();
        var selectedIds = selectedCandidateIds.Distinct().ToArray();
        if (response == RecommendationResponse.Accepted &&
            (selectedIds.Length == 0 ||
             !selectedIds.ToHashSet().IsSubsetOf(availableCandidateIds)))
        {
            throw new ArgumentException(
                "Accepted responses must identify valid selected Candidates.",
                nameof(selectedCandidateIds));
        }

        if (response != RecommendationResponse.Accepted && selectedIds.Length > 0)
        {
            throw new ArgumentException(
                "Only an Accepted response may select Candidates.",
                nameof(selectedCandidateIds));
        }

        var representation = current.Representation with
        {
            Id = Guid.NewGuid(),
            Response = response,
            SelectedCandidateIds = selectedIds,
            ResponseProvenance = responseProvenance
        };
        var responded = current with { Representation = representation };
        await PublishRecommendationAsync(
            responded,
            authority,
            loaded.Artifact,
            cancellationToken);

        return responded;
    }

    public async Task<IReadOnlyCollection<IRecommendation>> GetRecommendationsAsync(
        Guid personId,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default)
    {
        ValidatePersonId(personId);
        ValidateAuthority(authority);
        var recommendations = await GetRecommendationArtifactsAsync(
            authority,
            cancellationToken);

        return recommendations
            .Where(item => item.Recommendation.PersonId == personId)
            .GroupBy(item => item.Recommendation.Id)
            .Select(group => group
                .OrderByDescending(item => item.UpdatedAtUtc)
                .First())
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => (IRecommendation)item.Recommendation)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<IRecommendationRepresentation>>
        GetHistoryAsync(
            Guid personId,
            Guid recommendationId,
            IKnowledgeAuthority authority,
            CancellationToken cancellationToken = default)
    {
        ValidateIds(personId, recommendationId);
        ValidateAuthority(authority);
        var recommendations = await GetRecommendationArtifactsAsync(
            authority,
            cancellationToken);

        return recommendations
            .Where(item =>
                item.Recommendation.PersonId == personId &&
                item.Recommendation.Id == recommendationId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item =>
                (IRecommendationRepresentation)item.Recommendation.Representation)
            .ToArray();
    }

    private async Task<LoadedRecommendation?> GetCurrentRecommendationAsync(
        Guid personId,
        Guid recommendationId,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken)
    {
        var current = await CurrentArtifactResolver.ResolveAsync(
            librarian,
            artificer,
            CurrentRecommendationStage,
            GetCurrentRecommendationKey(
                authority,
                personId,
                recommendationId),
            authority,
            DeserializeRecommendationAsync,
            recommendation =>
                recommendation.PersonId == personId &&
                recommendation.Id == recommendationId,
            cancellationToken);

        return current is null
            ? null
            : new LoadedRecommendation(current.Model, current.Artifact);
    }

    private async Task PublishRecommendationAsync(
        ParallelYou.Models.Recommendation.Recommendation recommendation,
        IKnowledgeAuthority authority,
        IKnowledgeArtifact? previous,
        CancellationToken cancellationToken)
    {
        var content = JsonSerializer.SerializeToUtf8Bytes(recommendation);
        var artifact = await librarian.PublishArtifactAsync(
            new KnowledgeDescriptor($"Recommendation_{recommendation.Id}"),
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
            CurrentRecommendationStage,
            GetCurrentRecommendationKey(
                authority,
                recommendation.PersonId,
                recommendation.Id),
            artifact.Reference,
            cancellationToken);
    }

    private async Task<List<RecommendationArtifact>>
        GetRecommendationArtifactsAsync(
            IKnowledgeAuthority authority,
            CancellationToken cancellationToken)
    {
        var artifacts = await librarian.FindArtifactsAsync(
            authority,
            cancellationToken);
        var recommendations = new List<RecommendationArtifact>();

        foreach (var artifact in artifacts)
        {
            var recommendation = await DeserializeRecommendationAsync(
                artifact,
                cancellationToken);
            if (recommendation is not null)
            {
                recommendations.Add(new RecommendationArtifact(
                    recommendation,
                    artifact.UpdatedAtUtc));
            }
        }

        return recommendations;
    }

    private static async Task<ParallelYou.Models.Recommendation.Recommendation?>
        DeserializeRecommendationAsync(
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
        return await JsonSerializer
            .DeserializeAsync<ParallelYou.Models.Recommendation.Recommendation>(
                stream,
                cancellationToken: cancellationToken);
    }

    private async Task ValidatePlansAsync(
        Guid personId,
        IRecommendationSubmission submission,
        IKnowledgeAuthority recommendationAuthority,
        CancellationToken cancellationToken)
    {
        var planAuthority = new KnowledgeAuthority(
            recommendationAuthority.Identity,
            PlanAuthority.Context);
        var plans = await planningService.GetPlansAsync(
            personId,
            planAuthority,
            cancellationToken);
        var availablePlanIds = plans.Select(plan => plan.Id).ToHashSet();
        var referencedPlanIds = submission.Candidates
            .Select(candidate => candidate.PlanId)
            .Append(submission.Subject.PlanId)
            .ToHashSet();

        if (!referencedPlanIds.IsSubsetOf(availablePlanIds))
        {
            throw new ArgumentException(
                "Recommendation Plans must belong to the Person.",
                nameof(submission));
        }
    }

    private static ParallelYou.Models.Recommendation.Recommendation CreateModel(
        Guid personId,
        Guid recommendationId,
        IRecommendationSubmission submission)
        => new(
            recommendationId,
            personId,
            new RecommendationRepresentation(
                Guid.NewGuid(),
                recommendationId,
                new RecommendationSubject(
                    submission.Subject.PlanId,
                    submission.Subject.Description.Trim()),
                submission.Candidates
                    .Select(candidate => new RecommendationCandidate(
                        candidate.Id,
                        candidate.PlanId,
                        candidate.Kind,
                        candidate.Description.Trim()))
                    .ToArray(),
                submission.Rationale.Trim(),
                submission.Assumptions
                    .Where(assumption => !string.IsNullOrWhiteSpace(assumption))
                    .Select(assumption => assumption.Trim())
                    .Distinct()
                    .ToArray(),
                submission.Confidence,
                submission.Provenance,
                RecommendationResponse.AwaitingResponse,
                SelectedCandidateIds: [],
                ResponseProvenance: null));

    private static void ValidateSubmission(IRecommendationSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(submission.Subject);
        ArgumentNullException.ThrowIfNull(submission.Candidates);
        ArgumentNullException.ThrowIfNull(submission.Assumptions);
        ArgumentNullException.ThrowIfNull(submission.Provenance);
        if (submission.Subject.PlanId == Guid.Empty ||
            string.IsNullOrWhiteSpace(submission.Subject.Description))
        {
            throw new ArgumentException(
                "Recommendation Subject must identify and describe a Plan.",
                nameof(submission));
        }

        if (submission.Candidates.Count == 0 ||
            submission.Candidates.Any(candidate =>
                candidate is null ||
                candidate.Id == Guid.Empty ||
                candidate.PlanId == Guid.Empty ||
                string.IsNullOrWhiteSpace(candidate.Description)))
        {
            throw new ArgumentException(
                "A Recommendation must contain at least one valid Candidate.",
                nameof(submission));
        }

        if (submission.Candidates
                .Select(candidate => candidate.Id)
                .Distinct()
                .Count() != submission.Candidates.Count)
        {
            throw new ArgumentException(
                "Recommendation Candidate IDs must be unique.",
                nameof(submission));
        }

        if (string.IsNullOrWhiteSpace(submission.Rationale))
        {
            throw new ArgumentException(
                "Recommendation rationale cannot be empty.",
                nameof(submission));
        }

        if (submission.Confidence is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(submission),
                "Recommendation confidence must be between 0 and 1.");
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

    private static void ValidateIds(Guid personId, Guid recommendationId)
    {
        ValidatePersonId(personId);
        if (recommendationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Recommendation ID cannot be empty.",
                nameof(recommendationId));
        }
    }

    private static void ValidateAuthority(IKnowledgeAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
        if (!string.Equals(
                authority.Context,
                RecommendationAuthority.Context,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Recommendation authority context must be '{RecommendationAuthority.Context}'.",
                nameof(authority));
        }
    }

    private static string GetCurrentRecommendationKey(
        IKnowledgeAuthority authority,
        Guid personId,
        Guid recommendationId)
        => $"{authority.Identity.Scheme}:{authority.Identity.SubjectId}:{personId:N}:{recommendationId:N}";

    private sealed record LoadedRecommendation(
        ParallelYou.Models.Recommendation.Recommendation Recommendation,
        IKnowledgeArtifact Artifact);

    private sealed record RecommendationArtifact(
        ParallelYou.Models.Recommendation.Recommendation Recommendation,
        DateTimeOffset UpdatedAtUtc);
}

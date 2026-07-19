using System.Text.Json;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Library.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Workbench.Services;
using AethericForge.Runtime.Models.Knowledge.Primitives;
using AethericForge.Runtime.Models.Knowledge.Representations;
using ParallelYou.Abstractions.Intention;
using ParallelYou.Models.Intention;

namespace ParallelYou.Services.Intention;

public sealed class IntentionService(
    ILibrarian librarian,
    IArtificer artificer) : ServiceBase, IIntentionService
{
    private const string CurrentIntentionStage = "IntentionCurrent";

    public async Task<IIntention> CreateIntentionAsync(
        Guid personId,
        IIntentionSubmission submission,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default)
    {
        ValidatePersonId(personId);
        ValidateSubmission(submission);
        ValidateAuthority(authority);

        var intentionId = Guid.NewGuid();
        var intention = CreateModel(personId, intentionId, submission);
        await PublishIntentionAsync(
            intention,
            authority,
            previous: null,
            cancellationToken);

        return intention;
    }

    public async Task<IIntention?> ReviseIntentionAsync(
        Guid personId,
        Guid intentionId,
        IIntentionSubmission submission,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default)
    {
        ValidateIds(personId, intentionId);
        ValidateSubmission(submission);
        ValidateAuthority(authority);

        var previous = await GetCurrentArtifactAsync(
            personId,
            intentionId,
            authority,
            cancellationToken);
        if (previous is null)
        {
            return null;
        }

        var current = await DeserializeIntentionAsync(
            previous,
            cancellationToken);
        if (current is null ||
            current.PersonId != personId ||
            current.Id != intentionId)
        {
            return null;
        }

        var revision = CreateModel(personId, intentionId, submission);
        await PublishIntentionAsync(
            revision,
            authority,
            previous,
            cancellationToken);

        return revision;
    }

    public async Task<IReadOnlyCollection<IIntention>> GetIntentionsAsync(
        Guid personId,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default)
    {
        ValidatePersonId(personId);
        ValidateAuthority(authority);
        var intentions = await GetIntentionArtifactsAsync(
            authority,
            cancellationToken);

        return intentions
            .Where(item => item.Intention.PersonId == personId)
            .GroupBy(item => item.Intention.Id)
            .Select(group => group
                .OrderByDescending(item => item.UpdatedAtUtc)
                .First())
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => (IIntention)item.Intention)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<IIntentionRepresentation>> GetHistoryAsync(
        Guid personId,
        Guid intentionId,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default)
    {
        ValidateIds(personId, intentionId);
        ValidateAuthority(authority);
        var intentions = await GetIntentionArtifactsAsync(
            authority,
            cancellationToken);

        return intentions
            .Where(item =>
                item.Intention.PersonId == personId &&
                item.Intention.Id == intentionId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item =>
                (IIntentionRepresentation)item.Intention.Representation)
            .ToArray();
    }

    private async Task<IKnowledgeArtifact?> GetCurrentArtifactAsync(
        Guid personId,
        Guid intentionId,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken)
    {
        var current = await CurrentArtifactResolver.ResolveAsync(
            librarian,
            artificer,
            CurrentIntentionStage,
            GetCurrentIntentionKey(
                authority,
                personId,
                intentionId),
            authority,
            DeserializeIntentionAsync,
            intention =>
                intention.PersonId == personId &&
                intention.Id == intentionId,
            cancellationToken);

        return current?.Artifact;
    }

    private async Task PublishIntentionAsync(
        ParallelYou.Models.Intention.Intention intention,
        IKnowledgeAuthority authority,
        IKnowledgeArtifact? previous,
        CancellationToken cancellationToken)
    {
        var content = JsonSerializer.SerializeToUtf8Bytes(intention);
        var artifact = await librarian.PublishArtifactAsync(
            new KnowledgeDescriptor($"Intention_{intention.Id}"),
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
            CurrentIntentionStage,
            GetCurrentIntentionKey(
                authority,
                intention.PersonId,
                intention.Id),
            artifact.Reference,
            cancellationToken);
    }

    private async Task<List<IntentionArtifact>> GetIntentionArtifactsAsync(
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken)
    {
        var artifacts = await librarian.FindArtifactsAsync(
            authority,
            cancellationToken);
        var intentions = new List<IntentionArtifact>();

        foreach (var artifact in artifacts)
        {
            var intention = await DeserializeIntentionAsync(
                artifact,
                cancellationToken);
            if (intention is not null)
            {
                intentions.Add(new IntentionArtifact(
                    intention,
                    artifact.UpdatedAtUtc));
            }
        }

        return intentions;
    }

    private static async Task<ParallelYou.Models.Intention.Intention?>
        DeserializeIntentionAsync(
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
            .DeserializeAsync<ParallelYou.Models.Intention.Intention>(
                stream,
                cancellationToken: cancellationToken);
    }

    private static ParallelYou.Models.Intention.Intention CreateModel(
        Guid personId,
        Guid intentionId,
        IIntentionSubmission submission)
        => new(
            intentionId,
            personId,
            new IntentionRepresentation(
                Guid.NewGuid(),
                intentionId,
                submission.Provenance,
                submission.Description.Trim(),
                submission.IsAdopted));

    private static void ValidateSubmission(IIntentionSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(submission.Provenance);
        if (string.IsNullOrWhiteSpace(submission.Description))
        {
            throw new ArgumentException(
                "Intention description cannot be empty.",
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

    private static void ValidateIds(Guid personId, Guid intentionId)
    {
        ValidatePersonId(personId);
        if (intentionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Intention ID cannot be empty.",
                nameof(intentionId));
        }
    }

    private static void ValidateAuthority(IKnowledgeAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
        if (!string.Equals(
                authority.Context,
                IntentionAuthority.Context,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Intention authority context must be '{IntentionAuthority.Context}'.",
                nameof(authority));
        }
    }

    private static string GetCurrentIntentionKey(
        IKnowledgeAuthority authority,
        Guid personId,
        Guid intentionId)
        => $"{authority.Identity.Scheme}:{authority.Identity.SubjectId}:{personId:N}:{intentionId:N}";

    private sealed record IntentionArtifact(
        ParallelYou.Models.Intention.Intention Intention,
        DateTimeOffset UpdatedAtUtc);
}

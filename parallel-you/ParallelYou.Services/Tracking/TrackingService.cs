using System.Text.Json;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Library.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Workbench.Services;
using AethericForge.Runtime.Models.Knowledge.Primitives;
using AethericForge.Runtime.Models.Knowledge.Representations;
using ParallelYou.Abstractions.Tracking;
using ParallelYou.Models.Tracking;

namespace ParallelYou.Services.Tracking;

public sealed class TrackingService(
    ILibrarian librarian,
    IArtificer artificer) : ServiceBase, ITrackingService
{
    private const string CurrentStateStage = "TrackingCurrent";

    public async Task<ITrackedState> TrackAsync(
        ITrackedState state,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateAuthority(authority);
        ValidateState(state);

        var trackedState = ToModel(state);
        var previous = await GetCurrentArtifactAsync(
            state.PersonId,
            state.Subject.Id,
            authority,
            cancellationToken);
        var content = JsonSerializer.SerializeToUtf8Bytes(trackedState);
        var artifact = await librarian.PublishArtifactAsync(
            new KnowledgeDescriptor($"Tracking_{state.Subject.Id}"),
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
            CurrentStateStage,
            GetCurrentStateKey(authority, state.PersonId, state.Subject.Id),
            artifact.Reference,
            cancellationToken);

        return trackedState;
    }

    public async Task<ITrackedState?> GetCurrentStateAsync(
        Guid personId,
        Guid subjectId,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default)
    {
        ValidateIds(personId, subjectId);
        ValidateAuthority(authority);

        var artifact = await GetCurrentArtifactAsync(
            personId,
            subjectId,
            authority,
            cancellationToken);
        if (artifact is null)
        {
            return null;
        }

        var state = await DeserializeStateAsync(artifact, cancellationToken);
        return state is not null &&
               state.PersonId == personId &&
               state.Subject.Id == subjectId
            ? state
            : null;
    }

    public async Task<IReadOnlyCollection<ITrackedState>> GetCurrentStatesAsync(
        Guid personId,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default)
    {
        if (personId == Guid.Empty)
        {
            throw new ArgumentException("Person ID cannot be empty.", nameof(personId));
        }

        ValidateAuthority(authority);
        var states = await GetStatesAsync(authority, cancellationToken);

        return states
            .Where(item => item.State.PersonId == personId)
            .GroupBy(item => item.State.Subject.Id)
            .Select(group => group
                .OrderByDescending(item => item.UpdatedAtUtc)
                .First())
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => (ITrackedState)item.State)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<ITrackedState>> GetHistoryAsync(
        Guid personId,
        Guid subjectId,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default)
    {
        ValidateIds(personId, subjectId);
        ValidateAuthority(authority);
        var states = await GetStatesAsync(authority, cancellationToken);

        return states
            .Where(item =>
                item.State.PersonId == personId &&
                item.State.Subject.Id == subjectId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => (ITrackedState)item.State)
            .ToArray();
    }

    private async Task<IKnowledgeArtifact?> GetCurrentArtifactAsync(
        Guid personId,
        Guid subjectId,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken)
    {
        var current = await CurrentArtifactResolver.ResolveAsync(
            librarian,
            artificer,
            CurrentStateStage,
            GetCurrentStateKey(authority, personId, subjectId),
            authority,
            DeserializeStateAsync,
            state =>
                state.PersonId == personId &&
                state.Subject.Id == subjectId,
            cancellationToken);

        return current?.Artifact;
    }

    private async Task<List<TrackedStateArtifact>> GetStatesAsync(
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken)
    {
        var artifacts = await librarian.FindArtifactsAsync(
            authority,
            cancellationToken);
        var states = new List<TrackedStateArtifact>();

        foreach (var artifact in artifacts)
        {
            var state = await DeserializeStateAsync(artifact, cancellationToken);
            if (state is not null)
            {
                states.Add(new TrackedStateArtifact(state, artifact.UpdatedAtUtc));
            }
        }

        return states;
    }

    private static async Task<TrackedState?> DeserializeStateAsync(
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
        return await JsonSerializer.DeserializeAsync<TrackedState>(
            stream,
            cancellationToken: cancellationToken);
    }

    private static TrackedState ToModel(ITrackedState state)
        => new(
            state.Id,
            state.PersonId,
            new TrackedSubject(
                state.Subject.Id,
                state.Subject.Type,
                state.Subject.Description),
            state.Value,
            state.Provenance,
            state.Confidence);

    private static void ValidateState(ITrackedState state)
    {
        ArgumentNullException.ThrowIfNull(state.Subject);
        if (state.Id == Guid.Empty)
        {
            throw new ArgumentException("Observation ID cannot be empty.", nameof(state));
        }

        ValidateIds(state.PersonId, state.Subject.Id);
        if (string.IsNullOrWhiteSpace(state.Subject.Type))
        {
            throw new ArgumentException("Subject type cannot be empty.", nameof(state));
        }

        if (string.IsNullOrWhiteSpace(state.Subject.Description))
        {
            throw new ArgumentException("Subject description cannot be empty.", nameof(state));
        }

        if (string.IsNullOrWhiteSpace(state.Value))
        {
            throw new ArgumentException("Tracked value cannot be empty.", nameof(state));
        }

        if (state.Confidence is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                "Confidence must be between 0 and 1.");
        }
    }

    private static void ValidateIds(Guid personId, Guid subjectId)
    {
        if (personId == Guid.Empty)
        {
            throw new ArgumentException("Person ID cannot be empty.", nameof(personId));
        }

        if (subjectId == Guid.Empty)
        {
            throw new ArgumentException("Subject ID cannot be empty.", nameof(subjectId));
        }
    }

    private static void ValidateAuthority(IKnowledgeAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
        if (!string.Equals(
                authority.Context,
                TrackingAuthority.Context,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Tracking authority context must be '{TrackingAuthority.Context}'.",
                nameof(authority));
        }
    }

    private static string GetCurrentStateKey(
        IKnowledgeAuthority authority,
        Guid personId,
        Guid subjectId)
        => $"{authority.Identity.Scheme}:{authority.Identity.SubjectId}:{personId:N}:{subjectId:N}";

    private sealed record TrackedStateArtifact(
        TrackedState State,
        DateTimeOffset UpdatedAtUtc);
}

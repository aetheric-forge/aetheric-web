using System.Text.Json;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Library.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Workbench.Services;
using AethericForge.Runtime.Models.Knowledge.Primitives;
using AethericForge.Runtime.Models.Staging;

namespace AethericForge.Web.Services;

internal static class CurrentArtifactResolver
{
    public static async Task<ResolvedArtifact<T>?> ResolveAsync<T>(
        ILibrarian librarian,
        IArtificer artificer,
        string stage,
        string key,
        IKnowledgeAuthority authority,
        Func<IKnowledgeArtifact, CancellationToken, Task<T?>> deserialize,
        Func<T, bool> matches,
        CancellationToken cancellationToken)
        where T : class
    {
        var cachedReference = await TryReadReferenceAsync(
            artificer,
            stage,
            key,
            cancellationToken);
        var candidates = new List<IKnowledgeArtifact>();

        if (cachedReference is not null)
        {
            var cachedArtifact = await librarian.GetArtifactAsync(
                cachedReference,
                cancellationToken);
            if (cachedArtifact is not null && HasAuthority(cachedArtifact, authority))
            {
                candidates.Add(cachedArtifact);
            }
        }

        var durableArtifacts = await librarian.FindArtifactsAsync(
            authority,
            cancellationToken);
        if (durableArtifacts is not null)
        {
            candidates.AddRange(durableArtifacts);
        }

        ResolvedArtifact<T>? current = null;
        foreach (var artifact in candidates
                     .Where(candidate => HasAuthority(candidate, authority))
                     .GroupBy(candidate => GetReferenceKey(candidate.Reference))
                     .Select(group => group.First())
                     .OrderByDescending(candidate => candidate.UpdatedAtUtc))
        {
            var model = await deserialize(artifact, cancellationToken);
            if (model is not null && matches(model))
            {
                current = new ResolvedArtifact<T>(artifact, model);
                break;
            }
        }

        if (current is not null &&
            (cachedReference is null ||
             !ReferenceEquals(cachedReference, current.Artifact.Reference)))
        {
            await TryStoreReferenceAsync(
                artificer,
                stage,
                key,
                current.Artifact.Reference,
                cancellationToken);
        }

        return current;
    }

    public static async Task TryStoreReferenceAsync(
        IArtificer artificer,
        string stage,
        string key,
        IKnowledgeReference reference,
        CancellationToken cancellationToken)
    {
        try
        {
            var content = JsonSerializer.SerializeToUtf8Bytes(reference);
            await artificer.PutAsync(
                stage,
                key,
                new MemoryStream(content),
                ct: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Cache population is best-effort; durable knowledge remains authoritative.
        }
    }

    private static async Task<IKnowledgeReference?> TryReadReferenceAsync(
        IArtificer artificer,
        string stage,
        string key,
        CancellationToken cancellationToken)
    {
        try
        {
            var mapping = new StagingReference(stage, key);
            if (!await artificer.ExistsAsync(mapping, cancellationToken))
            {
                return null;
            }

            await using var stream = await artificer.OpenReadAsync(
                mapping,
                cancellationToken);
            return await JsonSerializer.DeserializeAsync<KnowledgeReference>(
                stream,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static bool HasAuthority(
        IKnowledgeArtifact artifact,
        IKnowledgeAuthority authority)
    {
        var candidate = artifact.Authority;
        return candidate is not null &&
               candidate.Identity.Scheme == authority.Identity.Scheme &&
               string.Equals(
                   candidate.Identity.SubjectId,
                   authority.Identity.SubjectId,
                   StringComparison.Ordinal) &&
               string.Equals(
                   candidate.Context,
                   authority.Context,
                   StringComparison.Ordinal);
    }

    private static bool ReferenceEquals(
        IKnowledgeReference left,
        IKnowledgeReference right)
        => string.Equals(
            GetReferenceKey(left),
            GetReferenceKey(right),
            StringComparison.Ordinal);

    private static string GetReferenceKey(IKnowledgeReference reference)
        => $"{reference.Scheme}:{reference.Kind}/{reference.Name}@{reference.Version}.{reference.Revision}";
}

internal sealed record ResolvedArtifact<T>(
    IKnowledgeArtifact Artifact,
    T Model)
    where T : class;

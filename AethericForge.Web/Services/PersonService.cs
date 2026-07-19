using System.Collections.Concurrent;
using System.Text.Json;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Library.Services;
using AethericForge.Runtime.Models.Knowledge.Authorities;
using AethericForge.Runtime.Models.Knowledge.Primitives;
using AethericForge.Runtime.Models.Knowledge.Representations;
using AethericForge.Web.Abstractions.Person;
using ParallelYou.Abstractions;
using ParallelYou.Models.Person;

namespace AethericForge.Web.Services;

public sealed class PersonService(ILibrarian librarian) : ServiceBase, IPersonService
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _provisioningLocks =
        new(StringComparer.Ordinal);

    public async Task<IPerson> GetOrCreatePersonAsync(
        IIdentitySubject identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var authority = new KnowledgeAuthority(identity, PersonAuthority.Context);
        var key = $"{identity.Scheme}:{identity.SubjectId}";
        var gate = _provisioningLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken);
        try
        {
            var existing = await librarian.FindArtifactsAsync(
                authority,
                cancellationToken);
            var artifact = existing
                .OrderBy(candidate => candidate.CreatedAtUtc)
                .FirstOrDefault();

            if (artifact is not null)
            {
                var record = await ReadRecordAsync(artifact, cancellationToken);
                return new ParallelYou.Models.Person.Person(record.PersonId);
            }

            var personId = Guid.NewGuid();
            var createdAtUtc = DateTimeOffset.UtcNow;
            var representation = new PersonRepresentation(
                personId,
                new Provenance(
                    ProvenanceKind.Observed,
                    key,
                    createdAtUtc),
                identity.DisplayName);
            var recordToPublish = new PersonRecord(
                representation.PersonId,
                representation.DisplayName,
                representation.Provenance);
            var content = JsonSerializer.SerializeToUtf8Bytes(recordToPublish);

            await librarian.PublishArtifactAsync(
                new KnowledgeDescriptor("Person representation"),
                [new KnowledgeRepresentation(
                    "application/json",
                    content.LongLength,
                    _ => Task.FromResult<Stream>(new MemoryStream(content)),
                    encoding: "utf-8")],
                authority: authority,
                ct: cancellationToken);

            return new ParallelYou.Models.Person.Person(personId);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<PersonRecord> ReadRecordAsync(
        IKnowledgeArtifact artifact,
        CancellationToken cancellationToken)
    {
        var representation = artifact.Representations
            .FirstOrDefault(candidate =>
                string.Equals(
                    candidate.ContentType,
                    "application/json",
                    StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException(
                "The Person artifact has no JSON representation.");

        await using var stream = await representation.OpenStreamAsync(
            cancellationToken);
        return await JsonSerializer.DeserializeAsync<PersonRecord>(
                   stream,
                   cancellationToken: cancellationToken)
               ?? throw new InvalidDataException(
                   "The Person representation is empty or invalid.");
    }

    private sealed record PersonRecord(
        Guid PersonId,
        string? DisplayName,
        Provenance Provenance);
}

namespace ParallelYou.Abstractions.Reflection;

using ParallelYou.Abstractions;

public interface IReflectionEvidence
{
    Guid Id { get; }
    string Content { get; }
    Provenance Provenance { get; }
}

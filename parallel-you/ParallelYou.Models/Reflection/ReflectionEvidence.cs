using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Reflection;

namespace ParallelYou.Models.Reflection;

public class ReflectionEvidence : IReflectionEvidence
{
    public Guid Id { get; set; }
    public string Content { get; set; }
    public Provenance Provenance { get; set; }
}

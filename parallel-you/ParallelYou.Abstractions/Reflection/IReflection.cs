using System.Collections.Generic;

namespace ParallelYou.Abstractions.Reflection;

public interface IReflection
{
    Guid Id { get; }
    Guid PersonId { get; }
    IReflectionSubject Subject { get; }
    IEnumerable<string> Questions { get; }
    IEnumerable<IReflectionEvidence> Evidence { get; }
    IEnumerable<IReflectionInsight> Insights { get; }
}

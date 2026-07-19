using System;

namespace ParallelYou.Abstractions.Reflection;

public interface IReflectionInsightSubmission
{
    Guid Id { get; }
    string Content { get; }
}

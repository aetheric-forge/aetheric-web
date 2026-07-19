using System;
using ParallelYou.Abstractions.Reflection;

namespace ParallelYou.Models.Reflection;

public class ReflectionInsightSubmission : IReflectionInsightSubmission
{
    public Guid Id { get; set; }
    public string Content { get; set; }
}

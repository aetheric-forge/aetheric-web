using System;
using System.Collections.Generic;
using ParallelYou.Abstractions.Reflection;

namespace ParallelYou.Models.Reflection;

public class ReflectionService : IReflection
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public ReflectionSubject Subject { get; set; }
    public List<string> Questions { get; set; } = new();
    public List<ReflectionEvidence> Evidence { get; set; } = new();
    public List<ReflectionInsight> Insights { get; set; } = new();

    IEnumerable<string> IReflection.Questions => Questions;
    IEnumerable<IReflectionEvidence> IReflection.Evidence => Evidence;
    IEnumerable<IReflectionInsight> IReflection.Insights => Insights;
    IReflectionSubject IReflection.Subject => Subject;
}

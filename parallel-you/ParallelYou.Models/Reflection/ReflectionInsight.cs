using System;
using ParallelYou.Abstractions.Reflection;

namespace ParallelYou.Models.Reflection;

public class ReflectionInsight : IReflectionInsight
{
    public Guid Id { get; set; }
    public string Content { get; set; }
    public bool IsAdopted { get; set; }
}

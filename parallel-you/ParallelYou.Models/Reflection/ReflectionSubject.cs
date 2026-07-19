using System;
using ParallelYou.Abstractions.Reflection;

namespace ParallelYou.Models.Reflection;

public class ReflectionSubject : IReflectionSubject
{
    public Guid Id { get; set; }
    public string Type { get; set; }
    public string Description { get; set; }
}

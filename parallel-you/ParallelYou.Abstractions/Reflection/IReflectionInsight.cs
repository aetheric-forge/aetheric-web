namespace ParallelYou.Abstractions.Reflection;

public interface IReflectionInsight
{
    Guid Id { get; }
    string Content { get; }
    bool IsAdopted { get; }
}

namespace ParallelYou.Abstractions.Reflection;

public interface IReflectionSubject
{
    Guid Id { get; }
    string Type { get; }
    string Description { get; }
}

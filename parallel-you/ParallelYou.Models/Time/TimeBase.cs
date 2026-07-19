using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Time;

namespace ParallelYou.Models.Time;

public abstract class TimeBase : ITime
{
    public Guid Id { get; init; }

    protected TimeBase(Guid id)
    {
        Id = id;
    }
}

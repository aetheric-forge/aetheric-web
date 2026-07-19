using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Activity;

namespace ParallelYou.Models.Activity;

public abstract class ActivityBase : IActivity
{
    public Guid Id { get; init; }

    protected ActivityBase(Guid id)
    {
        Id = id;
    }
}

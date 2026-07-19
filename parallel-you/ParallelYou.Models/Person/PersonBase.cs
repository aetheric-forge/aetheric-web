using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Person;

namespace ParallelYou.Models.Person;

public abstract class PersonBase : IPerson
{
    public Guid Id { get; init; }

    protected PersonBase(Guid id)
    {
        Id = id;
    }
}

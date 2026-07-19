using AethericForge.Web.Abstractions.Person;
using ParallelYou.Abstractions;

namespace ParallelYou.Models.Person;

public abstract class PersonBase : IPerson
{
    public Guid Id { get; init; }

    protected PersonBase(Guid id)
    {
        Id = id;
    }
}

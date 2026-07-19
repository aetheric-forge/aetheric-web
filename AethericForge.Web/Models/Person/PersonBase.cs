using AethericForge.Web.Abstractions.Person;

namespace AethericForge.Web.Models.Person;

public abstract class PersonBase : IPerson
{
    public Guid Id { get; init; }

    protected PersonBase(Guid id)
    {
        Id = id;
    }
}

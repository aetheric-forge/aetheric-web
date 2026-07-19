using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Energy;

namespace ParallelYou.Models.Energy;

public abstract class EnergyBase : IEnergy
{
    public Guid Id { get; init; }

    protected EnergyBase(Guid id)
    {
        Id = id;
    }
}

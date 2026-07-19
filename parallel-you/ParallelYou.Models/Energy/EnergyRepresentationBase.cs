using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Energy;

namespace ParallelYou.Models.Energy;

public abstract class EnergyRepresentationBase : IEnergyRepresentation
{
    public Guid EnergyId { get; init; }
    public Provenance Provenance { get; init; }
    public string? DisplayName { get; init; }

    protected EnergyRepresentationBase(Guid energyId, Provenance provenance, string? displayName)
    {
        EnergyId = energyId;
        Provenance = provenance;
        DisplayName = displayName;
    }
}

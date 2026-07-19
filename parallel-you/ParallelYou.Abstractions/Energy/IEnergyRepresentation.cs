namespace ParallelYou.Abstractions.Energy;

public interface IEnergyRepresentation
{
    Guid EnergyId { get; }
    Provenance Provenance { get; }
    string? DisplayName { get; }
}

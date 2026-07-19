namespace ParallelYou.Abstractions.Tracking;

using ParallelYou.Abstractions;

public interface ITrackedState
{
    Guid Id { get; }
    Guid PersonId { get; }
    ITrackedSubject Subject { get; }
    string Value { get; }
    Provenance Provenance { get; }
    decimal Confidence { get; }
}

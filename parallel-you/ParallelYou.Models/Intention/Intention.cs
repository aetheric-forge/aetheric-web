namespace ParallelYou.Models.Intention;

public sealed class Intention : IntentionBase
{
    public Intention(
        Guid id,
        Guid personId,
        IntentionRepresentation representation)
        : base(id, personId, representation)
    {
    }
}

using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Intention;

namespace ParallelYou.Models.Intention;

public sealed record IntentionSubmission(
    string Description,
    Provenance Provenance,
    bool IsAdopted) : IIntentionSubmission;

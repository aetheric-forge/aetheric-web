using ParallelYou.Abstractions.Capture;

namespace ParallelYou.Models.Capture;

public sealed record CaptureEvidence(string Title, string Content) : ICaptureEvidence;

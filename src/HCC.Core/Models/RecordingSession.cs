namespace HCC.Core.Models;

/// <summary>
/// Summary of one recorded sampling session. The samples themselves live in a
/// separate JSONL file; this record only carries what the UI needs to list it.
/// </summary>
public sealed record RecordingSession(
    string Id,
    string Name,
    DateTimeOffset StartedAt,
    DateTimeOffset? StoppedAt,
    int SampleCount)
{
    public bool IsComplete => StoppedAt.HasValue;

    public string DisplayName => IsComplete
        ? $"{Name} · {StartedAt.LocalDateTime:dd.MM.yyyy HH:mm} · {SampleCount} Messungen"
        : $"{Name} · {StartedAt.LocalDateTime:dd.MM.yyyy HH:mm} · abgebrochen";
}

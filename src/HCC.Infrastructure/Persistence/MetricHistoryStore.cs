using System.Text.Json;
using System.Text;
using HCC.Core.Models;

namespace HCC.Infrastructure.Persistence;

public sealed class MetricHistoryStore
{
    public const int MaximumEntries = 7200;
    private const int CompactionBatchSize = 128;
    private static readonly Encoding Utf8 = new UTF8Encoding(false);
    private readonly object _sync = new();
    private readonly string _historyPath;
    private readonly List<MetricHistoryEntry> _entries = [];
    private readonly int _maximumEntries;
    private readonly int _compactionBatchSize;
    private bool _loaded;

    public MetricHistoryStore(string? storageDirectory = null, int maximumEntries = MaximumEntries, int compactionBatchSize = CompactionBatchSize)
    {
        var root = storageDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HardwareControlCenter");
        _historyPath = Path.Combine(root, "history.jsonl");
        _maximumEntries = Math.Max(1, maximumEntries);
        _compactionBatchSize = Math.Max(1, compactionBatchSize);
    }

    public IReadOnlyList<MetricHistoryEntry> LoadRecent(int count)
    {
        lock (_sync)
        {
            try
            {
                EnsureLoaded();
                return _entries.TakeLast(Math.Clamp(count, 1, _maximumEntries)).ToArray();
            }
            catch (IOException) { return []; }
            catch (JsonException) { return []; }
            catch (UnauthorizedAccessException) { return []; }
        }
    }

    public bool Append(MetricHistoryEntry entry)
    {
        lock (_sync)
        {
            try
            {
                EnsureLoaded();
                if (!IsValid(entry)) return false;
                Directory.CreateDirectory(Path.GetDirectoryName(_historyPath)!);
                File.AppendAllText(_historyPath, JsonSerializer.Serialize(entry) + Environment.NewLine, Utf8);
                _entries.Add(entry);
                if (_entries.Count > _maximumEntries + _compactionBatchSize)
                {
                    _entries.RemoveRange(0, _entries.Count - _maximumEntries);
                    Rewrite();
                }
                return true;
            }
            catch (IOException) { return false; }
            catch (JsonException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        if (!File.Exists(_historyPath)) return;

        foreach (var line in File.ReadLines(_historyPath, Utf8))
        {
            try
            {
                if (JsonSerializer.Deserialize<MetricHistoryEntry>(line) is { } entry && IsValid(entry)) _entries.Add(entry);
            }
            catch (JsonException) { }
        }
        if (_entries.Count > _maximumEntries)
        {
            _entries.RemoveRange(0, _entries.Count - _maximumEntries);
            Rewrite();
        }
    }

    private void Rewrite()
    {
        var temporaryPath = _historyPath + ".tmp";
        File.WriteAllLines(temporaryPath, _entries.Select(entry => JsonSerializer.Serialize(entry)), Utf8);
        File.Move(temporaryPath, _historyPath, overwrite: true);
    }

    private static bool IsValid(MetricHistoryEntry entry) =>
        entry.CpuUsagePercent is >= 0 and <= 100 &&
        entry.GpuUsagePercent is >= 0 and <= 100 &&
        entry.MemoryUsagePercent is >= 0 and <= 100;
}

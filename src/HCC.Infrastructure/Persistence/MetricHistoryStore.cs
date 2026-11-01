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
    public string? LastError { get; private set; }

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
                if (count <= 0) return [];
                return _entries.TakeLast(Math.Min(count, _maximumEntries)).ToArray();
            }
            catch (IOException ex) { LastError = ex.Message; return []; }
            catch (JsonException ex) { LastError = ex.Message; return []; }
            catch (UnauthorizedAccessException ex) { LastError = ex.Message; return []; }
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
                LastError = null;
                return true;
            }
            catch (IOException ex) { LastError = ex.Message; return false; }
            catch (JsonException ex) { LastError = ex.Message; return false; }
            catch (UnauthorizedAccessException ex) { LastError = ex.Message; return false; }
        }
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        if (!File.Exists(_historyPath))
        {
            _loaded = true;
            return;
        }

        var loadedEntries = new List<MetricHistoryEntry>();
        foreach (var line in File.ReadLines(_historyPath, Utf8))
        {
            try
            {
                if (JsonSerializer.Deserialize<MetricHistoryEntry>(line) is { } entry && IsValid(entry)) loadedEntries.Add(entry);
            }
            catch (JsonException) { }
        }
        _entries.Clear();
        _entries.AddRange(loadedEntries);
        if (_entries.Count > _maximumEntries)
        {
            _entries.RemoveRange(0, _entries.Count - _maximumEntries);
            Rewrite();
        }
        _loaded = true;
    }

    private void Rewrite()
    {
        var temporaryPath = _historyPath + ".tmp";
        File.WriteAllLines(temporaryPath, _entries.Select(entry => JsonSerializer.Serialize(entry)), Utf8);
        File.Move(temporaryPath, _historyPath, overwrite: true);
    }

    private static bool IsValid(MetricHistoryEntry entry) =>
        (entry.CpuUsagePercent.HasValue || entry.GpuUsagePercent.HasValue || entry.MemoryUsagePercent.HasValue) &&
        IsValid(entry.CpuUsagePercent) && IsValid(entry.GpuUsagePercent) && IsValid(entry.MemoryUsagePercent);

    private static bool IsValid(double? value) => value is null or (>= 0 and <= 100);
}

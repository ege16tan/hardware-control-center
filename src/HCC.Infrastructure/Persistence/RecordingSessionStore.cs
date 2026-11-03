using System.Globalization;
using System.Text;
using System.Text.Json;
using HCC.Core.Models;

namespace HCC.Infrastructure.Persistence;

/// <summary>
/// Records samples that the shared sampler already produced into named sessions.
/// It never opens hardware, never writes fan values and never changes a setting;
/// it only persists measurements. Every failure surfaces through <see cref="LastError"/>
/// instead of silently dropping data.
/// </summary>
public sealed class RecordingSessionStore
{
    public const int MaximumSessions = 20;
    public const int MaximumNameLength = 32;
    public const int MaximumSamplesPerSession = 86400;

    private static readonly Encoding Utf8 = new UTF8Encoding(false);
    private readonly object _sync = new();
    private readonly string _sessionDirectory;
    private readonly string _indexPath;
    private readonly int _maximumSessions;
    private readonly List<SavedSession> _sessions = [];
    private SavedSession? _active;
    private int _activeSamples;
    private bool _loaded;

    public RecordingSessionStore(string? storageDirectory = null, int maximumSessions = MaximumSessions)
    {
        var root = storageDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HardwareControlCenter");
        _sessionDirectory = Path.Combine(root, "sessions");
        _indexPath = Path.Combine(_sessionDirectory, "sessions.json");
        _maximumSessions = Math.Max(1, maximumSessions);
    }

    public string? LastError { get; private set; }
    public bool IsRecording => _active is not null;
    public string? ActiveSessionId => _active?.Id;
    public string? ActiveSessionName => _active?.Name;

    /// <summary>Newest session first. A running session appears with its current sample count.</summary>
    public IReadOnlyList<RecordingSession> Sessions
    {
        get
        {
            lock (_sync)
            {
                EnsureLoaded();
                return _sessions.Select(ToModel).ToArray();
            }
        }
    }

    public bool TryStart(string? name, out string? error)
    {
        lock (_sync)
        {
            if (IsRecording)
            {
                error = "Es läuft bereits eine Aufzeichnung. Bitte zuerst stoppen.";
                return false;
            }

            if (!TryNormalizeName(name, out var normalizedName, out error)) return false;

            try
            {
                EnsureLoaded();
                var id = Guid.NewGuid().ToString("N");
                var session = new SavedSession(id, normalizedName, DateTimeOffset.Now, null, 0);
                Directory.CreateDirectory(_sessionDirectory);
                File.WriteAllText(GetSamplePath(id), string.Empty, Utf8);

                _sessions.Insert(0, session);
                TrimSessions();
                if (!SaveIndex())
                {
                    _sessions.Remove(session);
                    TryDelete(GetSamplePath(id));
                    error = LastError ?? "Die Aufzeichnung konnte nicht gestartet werden.";
                    return false;
                }

                _active = session;
                _activeSamples = 0;
                LastError = null;
                error = null;
                return true;
            }
            catch (IOException ex) { error = SetError($"Die Aufzeichnung konnte nicht gestartet werden: {ex.Message}"); return false; }
            catch (UnauthorizedAccessException ex) { error = SetError($"Die Aufzeichnung darf nicht gestartet werden: {ex.Message}"); return false; }
        }
    }

    /// <summary>Returns false while no recording runs or when the sample could not be written.</summary>
    public bool Append(MetricHistoryEntry entry)
    {
        lock (_sync)
        {
            if (_active is null) return false;

            if (!entry.IsValid)
            {
                SetError("Der Messpunkt ist ungültig und wurde nicht aufgezeichnet.");
                return false;
            }

            if (_activeSamples >= MaximumSamplesPerSession)
            {
                SetError($"Die Aufzeichnung hat das Maximum von {MaximumSamplesPerSession} Messpunkten erreicht. Bitte eine neue Sitzung starten.");
                return false;
            }

            try
            {
                File.AppendAllText(GetSamplePath(_active.Id), JsonSerializer.Serialize(entry) + Environment.NewLine, Utf8);
                _activeSamples++;
                LastError = null;
                return true;
            }
            catch (IOException ex) { SetError($"Der Messpunkt konnte nicht gespeichert werden: {ex.Message}"); return false; }
            catch (UnauthorizedAccessException ex) { SetError($"Der Messpunkt darf nicht gespeichert werden: {ex.Message}"); return false; }
        }
    }

    public bool TryDelete(string? sessionId, out string? error)
    {
        lock (_sync)
        {
            EnsureLoaded();
            var index = _sessions.FindIndex(candidate => candidate.Id == sessionId);
            if (index < 0)
            {
                error = SetError("Die ausgewählte Aufzeichnung wurde nicht gefunden.");
                return false;
            }

            if (_active?.Id == sessionId)
            {
                error = SetError("Die laufende Aufzeichnung kann nicht gelöscht werden. Bitte zuerst stoppen.");
                return false;
            }

            var session = _sessions[index];
            _sessions.RemoveAt(index);
            if (!SaveIndex())
            {
                _sessions.Insert(index, session);
                error = LastError;
                return false;
            }

            // The index is authoritative; a leftover file without an entry is harmless.
            TryDelete(GetSamplePath(session.Id));
            LastError = null;
            error = null;
            return true;
        }
    }

    /// <summary>Writes one session as CSV (semicolon separated, German decimal comma) and returns its path.</summary>
    public bool TryExportCsv(string? sessionId, out string? path)
    {
        lock (_sync)
        {
            EnsureLoaded();
            var session = _sessions.FirstOrDefault(candidate => candidate.Id == sessionId);
            if (session is null)
            {
                path = null;
                SetError("Die ausgewählte Aufzeichnung wurde nicht gefunden.");
                return false;
            }

            var entries = LoadEntries(sessionId);
            if (entries.Count == 0)
            {
                path = null;
                SetError(LastError ?? "Die Aufzeichnung enthält keine Messwerte.");
                return false;
            }

            try
            {
                var directory = Path.Combine(Path.GetDirectoryName(_sessionDirectory)!, "exports");
                Directory.CreateDirectory(directory);
                var file = Path.Combine(directory,
                    $"{SanitizeFileName(session.Name)}-{session.StartedAt.LocalDateTime:yyyyMMdd-HHmm}.csv");

                var lines = new List<string>(entries.Count + 1)
                {
                    "Zeitstempel;CPU (%);GPU (%);RAM (%)"
                };
                lines.AddRange(entries.Select(entry => string.Join(';',
                    entry.Timestamp.ToString("O", CultureInfo.InvariantCulture),
                    FormatPercent(entry.CpuUsagePercent),
                    FormatPercent(entry.GpuUsagePercent),
                    FormatPercent(entry.MemoryUsagePercent))));

                File.WriteAllLines(file, lines, Utf8);
                LastError = null;
                path = file;
                return true;
            }
            catch (IOException ex) { path = null; SetError($"Die CSV-Datei konnte nicht geschrieben werden: {ex.Message}"); return false; }
            catch (UnauthorizedAccessException ex) { path = null; SetError($"Die CSV-Datei darf nicht geschrieben werden: {ex.Message}"); return false; }
        }
    }

    public bool TryStop(out RecordingSession? session)
    {
        lock (_sync)
        {
            if (_active is null)
            {
                session = null;
                SetError("Es läuft keine Aufzeichnung.");
                return false;
            }

            var finished = _active with { StoppedAt = DateTimeOffset.Now, SampleCount = _activeSamples };
            var index = _sessions.FindIndex(candidate => candidate.Id == finished.Id);
            if (index >= 0) _sessions[index] = finished;
            else _sessions.Insert(0, finished);

            _active = null;
            session = ToModel(finished);

            if (SaveIndex())
            {
                LastError = null;
                return true;
            }

            return false;
        }
    }

    public IReadOnlyList<MetricHistoryEntry> LoadEntries(string? sessionId)
    {
        lock (_sync)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(sessionId) || _sessions.All(candidate => candidate.Id != sessionId))
            {
                SetError("Die ausgewählte Aufzeichnung wurde nicht gefunden.");
                return [];
            }

            try
            {
                var path = GetSamplePath(sessionId);
                if (!File.Exists(path))
                {
                    SetError("Die Aufzeichnungsdatei fehlt.");
                    return [];
                }

                var entries = new List<MetricHistoryEntry>();
                foreach (var line in File.ReadLines(path, Utf8))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        if (JsonSerializer.Deserialize<MetricHistoryEntry>(line) is { IsValid: true } entry) entries.Add(entry);
                    }
                    catch (JsonException) { /* One broken line must not hide the rest of the session. */ }
                }

                LastError = null;
                return entries;
            }
            catch (IOException ex) { SetError($"Die Aufzeichnung konnte nicht gelesen werden: {ex.Message}"); return []; }
            catch (UnauthorizedAccessException ex) { SetError($"Die Aufzeichnung darf nicht gelesen werden: {ex.Message}"); return []; }
        }
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        try
        {
            if (!File.Exists(_indexPath)) return;

            var saved = JsonSerializer.Deserialize<SavedSessions>(File.ReadAllText(_indexPath));
            var discarded = 0;
            foreach (var session in saved?.Sessions ?? [])
            {
                if (!IsValid(session))
                {
                    discarded++;
                    continue;
                }

                _sessions.Add(session! with { SampleCount = CountSamples(session!) });
            }

            _sessions.Sort((left, right) => right.StartedAt.CompareTo(left.StartedAt));
            TrimSessions();
            if (discarded > 0)
                SetError($"{discarded} gespeicherte Aufzeichnung(en) wurden wegen ungültiger Daten verworfen.");
        }
        catch (IOException ex) { SetError($"Aufzeichnungen konnten nicht geladen werden: {ex.Message}"); }
        catch (JsonException ex) { SetError($"Die Aufzeichnungsliste ist beschädigt: {ex.Message}"); }
        catch (UnauthorizedAccessException ex) { SetError($"Aufzeichnungen dürfen nicht gelesen werden: {ex.Message}"); }
    }

    private bool SaveIndex()
    {
        try
        {
            Directory.CreateDirectory(_sessionDirectory);
            var temporaryPath = _indexPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new SavedSessions(_sessions.ToList())), Utf8);
            File.Move(temporaryPath, _indexPath, overwrite: true);
            return true;
        }
        catch (IOException ex) { SetError($"Die Aufzeichnungsliste konnte nicht gespeichert werden: {ex.Message}"); return false; }
        catch (UnauthorizedAccessException ex) { SetError($"Die Aufzeichnungsliste darf nicht gespeichert werden: {ex.Message}"); return false; }
    }

    /// <summary>Stored samples are authoritative; the index count can be stale after a crash.</summary>
    private int CountSamples(SavedSession session)
    {
        if (session.SampleCount > 0) return session.SampleCount;

        try
        {
            var path = GetSamplePath(session.Id);
            return File.Exists(path) ? File.ReadLines(path, Utf8).Count(line => !string.IsNullOrWhiteSpace(line)) : 0;
        }
        catch (IOException) { return 0; }
        catch (UnauthorizedAccessException) { return 0; }
    }

    private void TrimSessions()
    {
        while (_sessions.Count > _maximumSessions)
        {
            var oldest = _sessions[^1];
            _sessions.RemoveAt(_sessions.Count - 1);
            if (_active?.Id != oldest.Id) TryDelete(GetSamplePath(oldest.Id));
        }
    }

    private RecordingSession ToModel(SavedSession session) =>
        new(session.Id,
            session.Name,
            session.StartedAt,
            session.StoppedAt,
            _active?.Id == session.Id ? _activeSamples : session.SampleCount);

    private string GetSamplePath(string sessionId) => Path.Combine(_sessionDirectory, sessionId + ".jsonl");

    private string SetError(string message)
    {
        LastError = message;
        return message;
    }

    private static bool TryNormalizeName(string? name, out string normalizedName, out string? error)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            normalizedName = $"Sitzung {DateTimeOffset.Now:dd.MM.yyyy HH:mm}";
            error = null;
            return true;
        }

        if (trimmed.Length > MaximumNameLength)
        {
            normalizedName = "";
            error = $"Bitte einen Sitzungsnamen mit 1 bis {MaximumNameLength} Zeichen eingeben.";
            return false;
        }

        normalizedName = trimmed;
        error = null;
        return true;
    }

    private static bool IsValid(SavedSession? session) =>
        session?.Id is { Length: 32 } id
        && id.All(char.IsAsciiHexDigit)
        && !string.IsNullOrWhiteSpace(session.Name)
        && session.Name.Length <= MaximumNameLength
        && session.SampleCount >= 0
        && (session.StoppedAt is null || session.StoppedAt >= session.StartedAt);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim('_', ' ');
        return cleaned.Length == 0 ? "sitzung" : cleaned;
    }

    private static string FormatPercent(double? value) => value?.ToString("0.##", CultureInfo.CurrentCulture) ?? "";

    private sealed record SavedSessions(List<SavedSession>? Sessions = null);

    private sealed record SavedSession(
        string Id,
        string Name,
        DateTimeOffset StartedAt,
        DateTimeOffset? StoppedAt,
        int SampleCount);
}


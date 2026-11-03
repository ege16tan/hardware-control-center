using HCC.Core.Models;
using HCC.Infrastructure.Persistence;
using Xunit;

namespace HCC.Tests;

public sealed class RecordingSessionStoreTests
{
    [Fact]
    public void StartAppendStopPersistsSamplesAndSession()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new RecordingSessionStore(directory);
            Assert.True(store.TryStart("Spieleabend", out var error));
            Assert.Null(error);
            Assert.True(store.IsRecording);
            Assert.True(store.Append(Sample(1)));
            Assert.True(store.Append(Sample(2)));

            Assert.True(store.TryStop(out var session));

            Assert.False(store.IsRecording);
            Assert.NotNull(session);
            Assert.Equal("Spieleabend", session!.Name);
            Assert.Equal(2, session.SampleCount);
            Assert.True(session.IsComplete);

            var restored = new RecordingSessionStore(directory);
            var single = Assert.Single(restored.Sessions);
            Assert.Equal(2, single.SampleCount);
            Assert.Equal("Spieleabend", single.Name);

            var entries = restored.LoadEntries(single.Id);
            Assert.Equal(2, entries.Count);
            Assert.Equal(1d, entries[0].CpuUsagePercent);
            Assert.Equal(2d, entries[1].CpuUsagePercent);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AppendWithoutRecordingWritesNothing()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new RecordingSessionStore(directory);

            Assert.False(store.Append(Sample(1)));
            Assert.Empty(store.Sessions);
            var sessionDirectory = Path.Combine(directory, "sessions");
            if (Directory.Exists(sessionDirectory)) Assert.Empty(Directory.GetFiles(sessionDirectory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RejectsASecondRecordingAndInvalidNames()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new RecordingSessionStore(directory);
            Assert.True(store.TryStart("Erste", out _));

            Assert.False(store.TryStart("Zweite", out var error));
            Assert.NotNull(error);
            Assert.Equal("Erste", store.ActiveSessionName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RejectsNamesThatAreTooLong()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new RecordingSessionStore(directory);

            Assert.False(store.TryStart(new string('a', RecordingSessionStore.MaximumNameLength + 1), out var error));
            Assert.NotNull(error);
            Assert.False(store.IsRecording);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void UsesAutomaticNameWhenNameIsBlank()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new RecordingSessionStore(directory);

            Assert.True(store.TryStart("   ", out _));
            Assert.StartsWith("Sitzung ", store.ActiveSessionName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(150d, null, null)]
    [InlineData(10d, -1d, null)]
    [InlineData(10d, 20d, double.NaN)]
    public void RejectsInvalidSamples(double? cpu, double? gpu, double? memory)
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new RecordingSessionStore(directory);
            Assert.True(store.TryStart(null, out _));

            Assert.False(store.Append(new MetricHistoryEntry(DateTimeOffset.UtcNow, cpu, gpu, memory)));
            Assert.NotNull(store.LastError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RestoresUnfinishedSessionAfterCrashWithCountedSamples()
    {
        var directory = CreateTempDirectory();
        try
        {
            var crashed = new RecordingSessionStore(directory);
            Assert.True(crashed.TryStart("Absturz", out _));
            Assert.True(crashed.Append(Sample(1)));
            Assert.True(crashed.Append(Sample(2)));
            Assert.True(crashed.Append(Sample(3)));

            // A new instance simulates a restart; the index still reports zero samples.
            var restored = new RecordingSessionStore(directory);
            var session = Assert.Single(restored.Sessions);

            Assert.False(session.IsComplete);
            Assert.Equal(3, session.SampleCount);
            Assert.Equal(3, restored.LoadEntries(session.Id).Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void KeepsOnlyTheNewestSessionsAndDeletesTheirFiles()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new RecordingSessionStore(directory, maximumSessions: 2);
            for (var index = 1; index <= 3; index++)
            {
                Assert.True(store.TryStart($"Sitzung {index}", out _));
                Assert.True(store.Append(Sample(index)));
                Assert.True(store.TryStop(out _));
            }

            var restored = new RecordingSessionStore(directory, maximumSessions: 2);

            Assert.Equal(2, restored.Sessions.Count);
            Assert.DoesNotContain("Sitzung 1", restored.Sessions.Select(session => session.Name));
            Assert.Equal(2, Directory.GetFiles(Path.Combine(directory, "sessions"), "*.jsonl").Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void UsesDefaultsWhenTheIndexIsCorrupt()
    {
        var directory = CreateTempDirectory();
        try
        {
            var sessionDirectory = Directory.CreateDirectory(Path.Combine(directory, "sessions")).FullName;
            File.WriteAllText(Path.Combine(sessionDirectory, "sessions.json"), "{ das ist kein json");

            var store = new RecordingSessionStore(directory);

            Assert.Empty(store.Sessions);
            Assert.NotNull(store.LastError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DiscardsIndexEntriesWithUnsafeIds()
    {
        var directory = CreateTempDirectory();
        try
        {
            var sessionDirectory = Directory.CreateDirectory(Path.Combine(directory, "sessions")).FullName;
            File.WriteAllText(Path.Combine(sessionDirectory, "sessions.json"),
                "{\"Sessions\":[{\"Id\":\"../evil\",\"Name\":\"Boese\",\"StartedAt\":\"2026-11-02T10:00:00+01:00\",\"StoppedAt\":\"2026-11-02T10:05:00+01:00\",\"SampleCount\":4}]}");

            var store = new RecordingSessionStore(directory);

            Assert.Empty(store.Sessions);
            Assert.NotNull(store.LastError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ReportsStorageFailureInsteadOfClaimingARecording()
    {
        var directory = CreateTempDirectory();
        var blockingFile = Path.Combine(directory, "blocked");
        File.WriteAllText(blockingFile, "kein Verzeichnis");
        try
        {
            var store = new RecordingSessionStore(blockingFile);

            Assert.False(store.TryStart("Sitzung", out var error));
            Assert.NotNull(error);
            Assert.False(store.IsRecording);
            Assert.Empty(store.Sessions);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ReportsUnknownRecordings()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new RecordingSessionStore(directory);

            Assert.Empty(store.LoadEntries("unbekannt"));
            Assert.NotNull(store.LastError);
            Assert.Empty(store.LoadEntries(null));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DeleteRemovesTheSessionAndItsFile()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new RecordingSessionStore(directory);
            Assert.True(store.TryStart("Weg damit", out _));
            Assert.True(store.Append(Sample(1)));
            Assert.True(store.TryStop(out var session));
            var file = Assert.Single(Directory.GetFiles(Path.Combine(directory, "sessions"), "*.jsonl"));

            Assert.True(store.TryDelete(session!.Id, out var error));

            Assert.Null(error);
            Assert.Empty(store.Sessions);
            Assert.False(File.Exists(file));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DeleteRefusesTheRunningSessionAndUnknownIds()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new RecordingSessionStore(directory);
            Assert.True(store.TryStart("Läuft", out _));

            Assert.False(store.TryDelete(store.ActiveSessionId, out var runningError));
            Assert.NotNull(runningError);
            Assert.True(store.IsRecording);

            Assert.False(store.TryDelete("gibt-es-nicht", out var unknownError));
            Assert.NotNull(unknownError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ExportWritesSemicolonSeparatedCsvNextToTheSessions()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new RecordingSessionStore(directory);
            Assert.True(store.TryStart("Export: Test/1", out _));
            Assert.True(store.Append(Sample(1)));
            Assert.True(store.Append(Sample(2)));
            Assert.True(store.TryStop(out var session));

            Assert.True(store.TryExportCsv(session!.Id, out var path));

            Assert.NotNull(path);
            Assert.True(File.Exists(path));
            var lines = File.ReadAllLines(path!);
            Assert.Equal(3, lines.Length);
            Assert.StartsWith("Zeitstempel;", lines[0]);
            Assert.Equal(4, lines[1].Split(';').Length);
            Assert.Contains("Export", Path.GetFileName(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ExportReportsUnknownSessions()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new RecordingSessionStore(directory);

            Assert.False(store.TryExportCsv("gibt-es-nicht", out var path));
            Assert.Null(path);
            Assert.NotNull(store.LastError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static MetricHistoryEntry Sample(int index) =>
        new(DateTimeOffset.UnixEpoch.AddSeconds(index), index, index + 1, index + 2);

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hcc-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}

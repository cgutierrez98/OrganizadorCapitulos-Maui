using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OrganizadorCapitulos.Maui.Services
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Action { get; set; } = "";
        public string OldName { get; set; } = "";
        public string NewName { get; set; } = "";
        public string Status { get; set; } = "Success";
        public string? Error { get; set; }
    }

    public class OperationLogService
    {
        private readonly List<LogEntry> _entries = new();
        private IReadOnlyList<LogEntry>? _cachedSnapshot;
        private readonly object _entriesLock = new();
        private readonly object _debounceLock = new();
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private readonly string _logPath;
        private readonly ILogger<OperationLogService> _logger;
        private CancellationTokenSource? _debounceCts;
        private Task _pendingSaveTask = Task.CompletedTask;
        private const int MaxEntries = 500;
        private const int SaveDebounceMs = 350;
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public IReadOnlyList<LogEntry> Entries
        {
            get
            {
                lock (_entriesLock)
                {
                    if (_cachedSnapshot == null)
                    {
                        var newestFirst = new List<LogEntry>(_entries.Count);
                        for (int i = _entries.Count - 1; i >= 0; i--)
                        {
                            newestFirst.Add(_entries[i]);
                        }
                        _cachedSnapshot = newestFirst.AsReadOnly();
                    }
                    return _cachedSnapshot;
                }
            }
        }

        public OperationLogService() : this(null, null) { }

        public OperationLogService(ILogger<OperationLogService> logger) : this(null, logger) { }

        public OperationLogService(string? logDirectory, ILogger<OperationLogService>? logger = null)
        {
            _logger = logger ?? NullLogger<OperationLogService>.Instance;
            var appFolder = logDirectory;
            if (string.IsNullOrWhiteSpace(appFolder))
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                appFolder = Path.Combine(appDataPath, "OrganizadorCapitulos");
            }

            if (!Directory.Exists(appFolder))
                Directory.CreateDirectory(appFolder);

            _logPath = Path.Combine(appFolder, "operations.log.json");
        }

        public Task InitializeAsync() => LoadLogAsync();

        public void LogRename(string oldPath, string newPath, bool success = true, string? error = null)
        {
            AddEntry(CreateRenameEntry(oldPath, newPath, success, error));
        }

        public void LogUndo(string fromPath, string toPath)
        {
            AddEntry(new LogEntry
            {
                Action = "Undo",
                OldName = Path.GetFileName(fromPath),
                NewName = Path.GetFileName(toPath),
                Status = "Success"
            });
        }

        public void LogRedo(string fromPath, string toPath)
        {
            AddEntry(new LogEntry
            {
                Action = "Redo",
                OldName = Path.GetFileName(fromPath),
                NewName = Path.GetFileName(toPath),
                Status = "Success"
            });
        }

        public void LogMove(string fileName, string destination)
        {
            AddEntry(CreateMoveEntry(fileName, destination));
        }

        public void LogBatch(IEnumerable<LogEntry> entries)
        {
            bool added = false;
            lock (_entriesLock)
            {
                foreach (var entry in entries)
                {
                    _entries.Add(entry);
                    added = true;
                }

                if (_entries.Count > MaxEntries)
                {
                    _entries.RemoveRange(0, _entries.Count - MaxEntries);
                }

                if (added)
                {
                    _cachedSnapshot = null;
                }
            }

            if (added)
            {
                ScheduleSave();
            }
        }

        public static LogEntry CreateRenameEntry(string oldPath, string newPath, bool success = true, string? error = null) =>
            new()
            {
                Action = "Rename",
                OldName = Path.GetFileName(oldPath),
                NewName = Path.GetFileName(newPath),
                Status = success ? "Success" : "Error",
                Error = error
            };

        public static LogEntry CreateMoveEntry(string fileName, string destination) =>
            new()
            {
                Action = "Move",
                OldName = fileName,
                NewName = $"→ {destination}",
                Status = "Success"
            };

        private void AddEntry(LogEntry entry)
        {
            lock (_entriesLock)
            {
                _entries.Add(entry);
                if (_entries.Count > MaxEntries)
                {
                    _entries.RemoveRange(0, _entries.Count - MaxEntries);
                }
                _cachedSnapshot = null;
            }

            ScheduleSave();
        }

        public void Clear()
        {
            lock (_entriesLock)
            {
                _entries.Clear();
                _cachedSnapshot = Array.Empty<LogEntry>();
            }
            ScheduleSave();
        }

        public async Task FlushAsync()
        {
            CancellationTokenSource? ctsToCancel;
            lock (_debounceLock)
            {
                ctsToCancel = _debounceCts;
                _debounceCts = null;
            }

            if (ctsToCancel != null)
            {
                ctsToCancel.Cancel();
                ctsToCancel.Dispose();
            }

            await SaveLogCoreAsync().ConfigureAwait(false);
        }

        private void ScheduleSave()
        {
            lock (_debounceLock)
            {
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();
                _debounceCts = new CancellationTokenSource();
                var token = _debounceCts.Token;
                _pendingSaveTask = DebouncedSaveAsync(token);
            }
        }

        private async Task DebouncedSaveAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(SaveDebounceMs, token).ConfigureAwait(false);
                if (!token.IsCancellationRequested)
                {
                    await SaveLogCoreAsync().ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Debounced by a newer write
            }
        }

        private async Task LoadLogAsync()
        {
            try
            {
                if (File.Exists(_logPath))
                {
                    var json = await File.ReadAllTextAsync(_logPath).ConfigureAwait(false);
                    var entries = JsonSerializer.Deserialize<List<LogEntry>>(json, _jsonOptions);
                    if (entries != null && entries.Count > 0)
                    {
                        lock (_entriesLock)
                        {
                            // Stored newest-first on disk; keep oldest-first internally
                            for (int i = entries.Count - 1; i >= 0; i--)
                            {
                                _entries.Add(entries[i]);
                            }
                            if (_entries.Count > MaxEntries)
                            {
                                _entries.RemoveRange(0, _entries.Count - MaxEntries);
                            }
                            _cachedSnapshot = null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load operation log from {LogPath}", _logPath);
            }
        }

        private async Task SaveLogCoreAsync()
        {
            await _saveLock.WaitAsync().ConfigureAwait(false);
            try
            {
                IReadOnlyList<LogEntry> snapshot = Entries;
                var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
                var tempPath = _logPath + ".tmp";
                await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);
                File.Move(tempPath, _logPath, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save operation log to {LogPath}", _logPath);
            }
            finally
            {
                _saveLock.Release();
            }
        }
    }
}

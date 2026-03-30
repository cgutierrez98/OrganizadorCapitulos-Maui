using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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
        private readonly object _entriesLock = new();
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private readonly string _logPath;
        private const int MaxEntries = 500;
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public IReadOnlyList<LogEntry> Entries
        {
            get
            {
                lock (_entriesLock)
                    return _entries.ToList().AsReadOnly();
            }
        }

        public OperationLogService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "OrganizadorCapitulos");

            if (!Directory.Exists(appFolder))
                Directory.CreateDirectory(appFolder);

            _logPath = Path.Combine(appFolder, "operations.log.json");
            _ = Task.Run(async () => await LoadLogAsync().ConfigureAwait(false));
        }

        public void LogRename(string oldPath, string newPath, bool success = true, string? error = null)
        {
            var entry = new LogEntry
            {
                Action = "Rename",
                OldName = Path.GetFileName(oldPath),
                NewName = Path.GetFileName(newPath),
                Status = success ? "Success" : "Error",
                Error = error
            };

            AddEntry(entry);
        }

        public void LogUndo(string fromPath, string toPath)
        {
            var entry = new LogEntry
            {
                Action = "Undo",
                OldName = Path.GetFileName(fromPath),
                NewName = Path.GetFileName(toPath),
                Status = "Success"
            };

            AddEntry(entry);
        }

        public void LogRedo(string fromPath, string toPath)
        {
            var entry = new LogEntry
            {
                Action = "Redo",
                OldName = Path.GetFileName(fromPath),
                NewName = Path.GetFileName(toPath),
                Status = "Success"
            };

            AddEntry(entry);
        }

        public void LogMove(string fileName, string destination)
        {
            var entry = new LogEntry
            {
                Action = "Move",
                OldName = fileName,
                NewName = $"→ {destination}",
                Status = "Success"
            };

            AddEntry(entry);
        }

        private void AddEntry(LogEntry entry)
        {
            lock (_entriesLock)
            {
                _entries.Insert(0, entry);

                // Keep only the last MaxEntries
                if (_entries.Count > MaxEntries)
                    _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
            }

            _ = SaveLogAsync();
        }

        public void Clear()
        {
            lock (_entriesLock)
                _entries.Clear();
            _ = SaveLogAsync();
        }

        private async Task LoadLogAsync()
        {
            try
            {
                if (File.Exists(_logPath))
                {
                    var json = await File.ReadAllTextAsync(_logPath).ConfigureAwait(false);
                    var entries = JsonSerializer.Deserialize<List<LogEntry>>(json, _jsonOptions);
                    if (entries != null)
                    {
                        lock (_entriesLock)
                            _entries.AddRange(entries);
                    }
                }
            }
            catch
            {
                // Ignore load errors
            }
        }

        private async Task SaveLogAsync()
        {
            await _saveLock.WaitAsync().ConfigureAwait(false);
            try
            {
                List<LogEntry> snapshot;
                lock (_entriesLock)
                    snapshot = _entries.ToList();

                var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
                await File.WriteAllTextAsync(_logPath, json).ConfigureAwait(false);
            }
            catch
            {
                // Ignore save errors
            }
            finally
            {
                _saveLock.Release();
            }
        }
    }
}

using System.Text.Json;

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
        private readonly string _logPath;
        private const int MaxEntries = 500;

        public IReadOnlyList<LogEntry> Entries => _entries.AsReadOnly();

        public OperationLogService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "OrganizadorCapitulos");

            if (!Directory.Exists(appFolder))
                Directory.CreateDirectory(appFolder);

            _logPath = Path.Combine(appFolder, "operations.log.json");
            LoadLog();
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
            _entries.Insert(0, entry);

            // Keep only the last MaxEntries
            while (_entries.Count > MaxEntries)
            {
                _entries.RemoveAt(_entries.Count - 1);
            }

            SaveLog();
        }

        public void Clear()
        {
            _entries.Clear();
            SaveLog();
        }

        private void LoadLog()
        {
            try
            {
                if (File.Exists(_logPath))
                {
                    var json = File.ReadAllText(_logPath);
                    var entries = JsonSerializer.Deserialize<List<LogEntry>>(json);
                    if (entries != null)
                    {
                        _entries.AddRange(entries);
                    }
                }
            }
            catch
            {
                // Ignore load errors
            }
        }

        private void SaveLog()
        {
            try
            {
                var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_logPath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }
    }
}

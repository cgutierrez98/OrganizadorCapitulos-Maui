using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Interfaces.Services;

namespace organizadorCapitulos.Infrastructure.Services
{
    public class PythonAIService : IAIService
    {
        private string? FindScriptPath()
        {
            var baseDir = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
            var candidates = new[] {
                Path.Combine(baseDir, "ai_service.py"),
                Path.Combine(baseDir, "Python", "ai_service.py"),
                Path.Combine(Directory.GetCurrentDirectory(), "Python", "ai_service.py")
            };

            foreach (var c in candidates)
            {
                if (File.Exists(c)) return c;
            }

            return null;
        }

        public bool IsAvailable()
        {
            try
            {
                var script = FindScriptPath();
                if (script == null) return false;
                var psi = new ProcessStartInfo("python")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add("--version");

                using var p = Process.Start(psi);
                if (p == null) return false;
                if (!p.WaitForExit(2000))
                {
                    try { p.Kill(true); } catch { }
                    return false;
                }
                return p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private async Task<(int? exitCode, string stdout, string stderr, bool timedOut)> RunPythonProcessAsync(
            string script, string command, string inputArg, int timeoutMs = 5000)
        {
            var psi = new ProcessStartInfo("python")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };
            psi.ArgumentList.Add(script);
            psi.ArgumentList.Add(command);
            psi.ArgumentList.Add("--input");
            psi.ArgumentList.Add(inputArg);

            try
            {
                using var p = Process.Start(psi);
                if (p == null) return (null, string.Empty, string.Empty, false);

                var stdoutTask = p.StandardOutput.ReadToEndAsync();
                var stderrTask = p.StandardError.ReadToEndAsync();

                var waitTask = p.WaitForExitAsync();
                var completed = await Task.WhenAny(waitTask, Task.Delay(timeoutMs)).ConfigureAwait(false);
                if (completed != waitTask)
                {
                    try { p.Kill(true); } catch { }
                    return (null, await stdoutTask.ConfigureAwait(false), await stderrTask.ConfigureAwait(false), true);
                }

                var stdout = await stdoutTask.ConfigureAwait(false);
                var stderr = await stderrTask.ConfigureAwait(false);
                return (p.ExitCode, stdout, stderr, false);
            }
            catch (Exception ex)
            {
                return (null, string.Empty, ex.Message, false);
            }
        }

        public async Task<string?> SuggestTitleAsync(string filePath)
        {
            var script = FindScriptPath();
            if (script == null) return null;
            var (exitCode, stdout, _, timedOut) = await RunPythonProcessAsync(script, "normalize", filePath, 5000).ConfigureAwait(false);
            if (timedOut) return null;
            if (exitCode == null || exitCode != 0) return null;
            if (string.IsNullOrWhiteSpace(stdout)) return null;

            try
            {
                using var doc = JsonDocument.Parse(stdout);
                if (doc.RootElement.TryGetProperty("result", out var res))
                {
                    return res.GetString();
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        public async Task<organizadorCapitulos.Core.Entities.ChapterInfo?> AnalyzeFilenameAsync(string filename)
        {
            var script = FindScriptPath();
            if (script == null) return null;
            var (exitCode, stdout, _, timedOut) = await RunPythonProcessAsync(script, "analyze", filename, 8000).ConfigureAwait(false);
            if (timedOut) return null;
            if (exitCode == null || exitCode != 0) return null;
            if (string.IsNullOrWhiteSpace(stdout)) return null;

            try
            {
                using var doc = JsonDocument.Parse(stdout);
                var root = doc.RootElement;
                if (root.TryGetProperty("error", out var err)) return null;

                var info = new ChapterInfo();
                if (root.TryGetProperty("series", out var series) && series.ValueKind != JsonValueKind.Null) info.Title = series.GetString() ?? string.Empty;
                if (root.TryGetProperty("season", out var season) && season.ValueKind == JsonValueKind.Number) info.Season = season.GetInt32();
                else if (root.TryGetProperty("season", out season) && season.ValueKind == JsonValueKind.String && int.TryParse(season.GetString(), out var s)) info.Season = s;
                if (root.TryGetProperty("episode", out var episode) && episode.ValueKind == JsonValueKind.Number) info.Episode = episode.GetInt32();
                else if (root.TryGetProperty("episode", out episode) && episode.ValueKind == JsonValueKind.String && int.TryParse(episode.GetString(), out var e)) info.Episode = e;
                if (root.TryGetProperty("title", out var title) && title.ValueKind != JsonValueKind.Null) info.EpisodeTitle = title.GetString() ?? string.Empty;

                return info;
            }
            catch
            {
                return null;
            }
        }
    }
}

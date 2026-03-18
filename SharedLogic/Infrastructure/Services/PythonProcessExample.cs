using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

namespace organizadorCapitulos.Infrastructure.Services
{
    // Example helper demonstrating how to safely invoke the Python ai_service script with timeout and JSON parsing
    public static class PythonProcessExample
    {
        public static async Task<string?> RunAnalyzeAsync(string scriptPath, string input, int timeoutMs = 8000)
        {
            var args = $"\"{scriptPath}\" analyze --input \"{input}\"";
            var psi = new ProcessStartInfo("python", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            try
            {
                using var p = Process.Start(psi);
                if (p == null) return null;

                var stdoutTask = p.StandardOutput.ReadToEndAsync();
                var stderrTask = p.StandardError.ReadToEndAsync();
                var waitTask = p.WaitForExitAsync();

                var completed = await Task.WhenAny(waitTask, Task.Delay(timeoutMs)).ConfigureAwait(false);
                if (completed != waitTask)
                {
                    try { p.Kill(true); } catch { }
                    return null;
                }

                var stdout = await stdoutTask.ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(stdout)) return null;

                using var doc = JsonDocument.Parse(stdout);
                var root = doc.RootElement;
                if (root.TryGetProperty("series", out var series))
                {
                    return series.GetString();
                }
            }
            catch
            {
                // swallow errors in example helper
            }

            return null;
        }
    }
}

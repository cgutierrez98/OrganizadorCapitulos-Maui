using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using organizadorCapitulos.Application.Services;
using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Interfaces.Services;

namespace OrganizadorCapitulos.Maui.Services.Workflows
{
    public sealed record LocalAnalyzedFile(
        string OriginalName,
        string FullPath,
        bool IsAnalyzed,
        string SeriesTitle,
        int Season,
        int Episode,
        string EpisodeTitle,
        int Year);

    public sealed record LoadFoldersAnalysisResult(
        List<LocalAnalyzedFile> Files,
        int AutoAnalyzedCount);

    public sealed record AIFileAnalysisResult(
        int Index,
        bool Success,
        ChapterInfo? Metadata);

    public sealed record AIBatchAnalysisResult(
        List<AIFileAnalysisResult> Results,
        int AnalyzedCount,
        bool Cancelled);

    public sealed class FileAnalysisWorkflow
    {
        private readonly FileOrganizerService _fileOrganizerService;
        private readonly IAIService _aiService;

        public FileAnalysisWorkflow(FileOrganizerService fileOrganizerService, IAIService aiService)
        {
            _fileOrganizerService = fileOrganizerService;
            _aiService = aiService;
        }

        public bool IsAIAvailable() => _aiService.IsAvailable();

        public async Task<LoadFoldersAnalysisResult> LoadAndAnalyzeLocalAsync(
            IEnumerable<string> folders,
            CancellationToken ct = default)
        {
            var filePaths = await _fileOrganizerService.LoadVideoFilesAsync(folders, ct).ConfigureAwait(false);
            var results = new List<LocalAnalyzedFile>(filePaths.Count);
            int autoAnalyzed = 0;

            foreach (var path in filePaths)
            {
                ct.ThrowIfCancellationRequested();
                var originalName = Path.GetFileName(path);

                if (EpisodePatternParser.TryExtractSeasonEpisode(originalName, out int season, out int episode))
                {
                    autoAnalyzed++;
                    results.Add(new LocalAnalyzedFile(
                        originalName,
                        path,
                        IsAnalyzed: true,
                        SeriesTitle: EpisodePatternParser.ExtractSeriesTitle(originalName),
                        Season: season,
                        Episode: episode,
                        EpisodeTitle: EpisodePatternParser.ExtractEpisodeTitle(originalName),
                        Year: 0));
                }
                else if (EpisodePatternParser.TryExtractYear(originalName, out int year))
                {
                    autoAnalyzed++;
                    results.Add(new LocalAnalyzedFile(
                        originalName,
                        path,
                        IsAnalyzed: true,
                        SeriesTitle: EpisodePatternParser.ExtractMovieTitle(originalName),
                        Season: 0,
                        Episode: 0,
                        EpisodeTitle: string.Empty,
                        Year: year));
                }
                else
                {
                    results.Add(new LocalAnalyzedFile(
                        originalName,
                        path,
                        IsAnalyzed: false,
                        SeriesTitle: string.Empty,
                        Season: 1,
                        Episode: 1,
                        EpisodeTitle: string.Empty,
                        Year: 0));
                }
            }

            return new LoadFoldersAnalysisResult(results, autoAnalyzed);
        }

        public async Task<AIBatchAnalysisResult> AnalyzeWithAIAsync(
            IReadOnlyList<string> fileNames,
            Action<int, int, string>? onProgress = null,
            CancellationToken ct = default)
        {
            var results = new List<AIFileAnalysisResult>(fileNames.Count);
            int total = fileNames.Count;
            int analyzed = 0;
            long lastReportTicks = 0;

            try
            {
                for (int i = 0; i < total; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var fileName = fileNames[i];
                    int current = i + 1;

                    long now = Environment.TickCount64;
                    if (current == 1 || current == total || now - lastReportTicks >= 60)
                    {
                        lastReportTicks = now;
                        onProgress?.Invoke(current, total, fileName);
                    }

                    try
                    {
                        var metadata = await _aiService.AnalyzeFilenameAsync(fileName, ct).ConfigureAwait(false);
                        if (metadata != null)
                        {
                            analyzed++;
                            results.Add(new AIFileAnalysisResult(i, Success: true, metadata));
                        }
                        else
                        {
                            results.Add(new AIFileAnalysisResult(i, Success: false, null));
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        results.Add(new AIFileAnalysisResult(i, Success: false, null));
                    }
                }

                return new AIBatchAnalysisResult(results, analyzed, Cancelled: false);
            }
            catch (OperationCanceledException)
            {
                return new AIBatchAnalysisResult(results, analyzed, Cancelled: true);
            }
        }
    }
}

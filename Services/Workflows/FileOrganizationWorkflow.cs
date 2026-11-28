using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using organizadorCapitulos.Application.Services;
using organizadorCapitulos.Application.Strategies;
using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Enums;

namespace OrganizadorCapitulos.Maui.Services.Workflows
{
    public sealed record RenameBatchItem(
        string FullPath,
        string OriginalName,
        string SeriesTitle,
        int Season,
        int Episode,
        string EpisodeTitle,
        int Year);

    public sealed record RenameItemResult(
        int Index,
        bool Success,
        string OldPath,
        string NewPath,
        ChapterInfo AppliedInfo,
        string? ErrorMessage);

    public sealed record RenameBatchResult(
        List<RenameItemResult> ItemResults,
        List<RenameOperation> UndoOperations,
        List<LogEntry> LogEntries,
        bool Cancelled);

    public sealed record MoveBatchResult(
        Dictionary<string, string> MovedBySource,
        List<RenameOperation> UndoOperations,
        List<LogEntry> LogEntries);

    public sealed class FileOrganizationWorkflow
    {
        private readonly FileOrganizerService _fileOrganizerService;
        private readonly RenameStrategyFactory _strategyFactory;

        public FileOrganizationWorkflow(
            FileOrganizerService fileOrganizerService,
            RenameStrategyFactory strategyFactory)
        {
            _fileOrganizerService = fileOrganizerService;
            _strategyFactory = strategyFactory;
        }

        public List<(string original, string preview)> BuildDryRunPreview(
            IReadOnlyList<RenameBatchItem> items,
            RenameMode mode,
            string? fileNameTemplate)
        {
            var previewList = new List<(string original, string preview)>(items.Count);
            if (items.Count == 0) return previewList;

            var strategy = _strategyFactory.CreateStrategy(mode);
            string? maintainTitle = mode == RenameMode.Maintain ? items[0].SeriesTitle : null;
            int maintainSeason = mode == RenameMode.Maintain ? items[0].Season : 0;
            int maintainEp = mode == RenameMode.Maintain ? items[0].Episode : 0;

            foreach (var item in items)
            {
                var info = new ChapterInfo
                {
                    Title = maintainTitle ?? item.SeriesTitle,
                    Season = maintainTitle != null ? maintainSeason : item.Season,
                    Episode = maintainTitle != null ? maintainEp++ : item.Episode,
                    EpisodeTitle = item.EpisodeTitle,
                    Year = item.Year
                };
                var preview = strategy.GetNewFileName(item.OriginalName, info, fileNameTemplate);
                previewList.Add((item.OriginalName, string.IsNullOrEmpty(preview) ? "(sin vista previa)" : preview));
            }

            return previewList;
        }

        public async Task<RenameBatchResult> RenameBatchAsync(
            IReadOnlyList<RenameBatchItem> items,
            RenameMode mode,
            string? fileNameTemplate,
            bool renameCompanions,
            Action<int, int, string>? onProgress = null,
            CancellationToken ct = default)
        {
            var itemResults = new List<RenameItemResult>(items.Count);
            var undoOperations = new List<RenameOperation>();
            var logEntries = new List<LogEntry>();

            if (items.Count == 0)
            {
                return new RenameBatchResult(itemResults, undoOperations, logEntries, Cancelled: false);
            }

            var strategy = _strategyFactory.CreateStrategy(mode);
            var companionIndex = renameCompanions
                ? FileOrganizerService.BuildCompanionIndex(items.Select(i => i.FullPath))
                : null;

            string? maintainTitle = mode == RenameMode.Maintain ? items[0].SeriesTitle : null;
            int maintainSeason = mode == RenameMode.Maintain ? items[0].Season : 0;
            int maintainNextEp = mode == RenameMode.Maintain ? items[0].Episode : 0;

            int total = items.Count;
            long lastReportTicks = 0;

            try
            {
                for (int i = 0; i < total; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var item = items[i];
                    int current = i + 1;

                    long now = Environment.TickCount64;
                    if (current == 1 || current == total || now - lastReportTicks >= 60)
                    {
                        lastReportTicks = now;
                        onProgress?.Invoke(current, total, item.OriginalName);
                    }

                    var chapterInfo = new ChapterInfo
                    {
                        Title = maintainTitle ?? item.SeriesTitle,
                        Season = maintainTitle != null ? maintainSeason : item.Season,
                        Episode = maintainTitle != null ? maintainNextEp++ : item.Episode,
                        EpisodeTitle = item.EpisodeTitle,
                        Year = item.Year
                    };

                    try
                    {
                        var (newPath, companionMoves) = await _fileOrganizerService.RenameFileWithCompanionsAsync(
                            item.FullPath,
                            chapterInfo,
                            strategy,
                            ct,
                            fileNameTemplate,
                            renameCompanions,
                            companionIndex).ConfigureAwait(false);

                        if (!string.Equals(item.FullPath, newPath, StringComparison.OrdinalIgnoreCase))
                        {
                            undoOperations.Add(new RenameOperation { OldPath = item.FullPath, NewPath = newPath });
                            logEntries.Add(OperationLogService.CreateRenameEntry(item.FullPath, newPath, success: true));
                        }

                        foreach (var (compOld, compNew) in companionMoves)
                        {
                            undoOperations.Add(new RenameOperation { OldPath = compOld, NewPath = compNew });
                            logEntries.Add(OperationLogService.CreateRenameEntry(compOld, compNew, success: true));
                        }

                        itemResults.Add(new RenameItemResult(i, Success: true, item.FullPath, newPath, chapterInfo, null));
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        logEntries.Add(OperationLogService.CreateRenameEntry(item.FullPath, item.FullPath, success: false, error: ex.Message));
                        itemResults.Add(new RenameItemResult(i, Success: false, item.FullPath, item.FullPath, chapterInfo, ex.Message));
                    }
                }

                return new RenameBatchResult(itemResults, undoOperations, logEntries, Cancelled: false);
            }
            catch (OperationCanceledException)
            {
                return new RenameBatchResult(itemResults, undoOperations, logEntries, Cancelled: true);
            }
        }

        public async Task<MoveBatchResult> MoveBatchAsync(
            IReadOnlyList<(string sourcePath, ChapterInfo? info)> items,
            string destinationFolder,
            bool organizeIntoSubfolders,
            CancellationToken ct = default)
        {
            var movedFiles = await _fileOrganizerService.MoveFilesStructuredAsync(
                items.ToList(),
                destinationFolder,
                organizeIntoSubfolders,
                ct).ConfigureAwait(false);

            var movedBySource = new Dictionary<string, string>(movedFiles.Count, StringComparer.OrdinalIgnoreCase);
            var undoOperations = new List<RenameOperation>(movedFiles.Count);
            var logEntries = new List<LogEntry>(movedFiles.Count);

            foreach (var (oldPath, newPath) in movedFiles)
            {
                movedBySource[oldPath] = newPath;
                undoOperations.Add(new RenameOperation { OldPath = oldPath, NewPath = newPath });
                logEntries.Add(OperationLogService.CreateMoveEntry(
                    Path.GetFileName(oldPath),
                    Path.GetDirectoryName(newPath) ?? destinationFolder));
            }

            return new MoveBatchResult(movedBySource, undoOperations, logEntries);
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading;
using organizadorCapitulos.Application.Services;
using organizadorCapitulos.Application.Strategies;using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Enums;
using organizadorCapitulos.Core.Interfaces.Services;
using OrganizadorCapitulos.Maui.Models;
using OrganizadorCapitulos.Maui.Services;
using OrganizadorCapitulos.Maui.Services.Interfaces;

namespace OrganizadorCapitulos.Maui.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        private readonly FileOrganizerService _fileOrganizerService;
        private readonly IAIService _aiService;
        private readonly RenameStrategyFactory _strategyFactory;
        private readonly IMetadataService _metadataService;
        private readonly SettingsService _settingsService;
        private readonly UndoRedoService _undoRedoService;
        private readonly OperationLogService _logService;
        private readonly IThemeService _themeService;
        private readonly IDragDropService _dragDropService;

        // Dependencies needed for specific logic
        private string? _maintainSeriesTitle;
        private int _maintainSeason = 1;
        private int _maintainNextEpisode = 1;
        private CancellationTokenSource? _operationCts;
        private bool _servicesInitialized;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FileCounts))]
        [NotifyPropertyChangedFor(nameof(AnalyzedCount))]
        [NotifyPropertyChangedFor(nameof(PendingCount))]
        [NotifyPropertyChangedFor(nameof(ErrorCount))]
        private ObservableCollection<FileViewModel> _files = new();

        [ObservableProperty]
        private FileViewModel? _selectedFile;

        [ObservableProperty]
        private RenameMode _currentMode = RenameMode.Maintain;

        [ObservableProperty]
        private string _statusMessage = "Listo para trabajar...";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotProcessing))]
        private bool _isProcessing;

        [ObservableProperty]
        private string _progressMessage = "";

        [ObservableProperty]
        private int _progressCurrent;

        [ObservableProperty]
        private int _progressTotal;

        [ObservableProperty]
        private bool _showingFolderBrowser;

        [ObservableProperty]
        private bool _showingSettings;

        [ObservableProperty]
        private bool _showingTmdbSearch;

        [ObservableProperty]
        private bool _showingSaveAll;

        [ObservableProperty]
        private bool _showingOperationLog;

        [ObservableProperty]
        private bool _isDragging;

        // FEAT-06: dry-run preview gate
        [ObservableProperty]
        private bool _showingDryRun;
        private List<(string original, string preview)> _dryRunPreview = new();
        public IReadOnlyList<(string original, string preview)> DryRunPreview => _dryRunPreview;

        public bool IsNotProcessing => !IsProcessing;

        public string FileCounts => $"{Files.Count} total | {AnalyzedCount} listos";
        public int AnalyzedCount => Files.Count(f => f.Status == FileStatus.Analyzed);
        public int PendingCount => Files.Count(f => f.Status == FileStatus.Pending);
        public int ErrorCount => Files.Count(f => f.Status == FileStatus.Error);

        public int ProgressPercent => ProgressTotal > 0 ? (int)((ProgressCurrent * 100.0) / ProgressTotal) : 0;

        public UndoRedoService UndoRedo => _undoRedoService;
        public IThemeService ThemeService => _themeService;
        public SettingsService Settings => _settingsService;

        public HomeViewModel(
            FileOrganizerService fileOrganizerService,
            IAIService aiService,
            RenameStrategyFactory strategyFactory,
            IMetadataService metadataService,
            SettingsService settingsService,
            UndoRedoService undoRedoService,
            OperationLogService logService,
            IThemeService themeService,
            IDragDropService dragDropService)
        {
            _fileOrganizerService = fileOrganizerService;
            _aiService = aiService;
            _strategyFactory = strategyFactory;
            _metadataService = metadataService;
            _settingsService = settingsService;
            _undoRedoService = undoRedoService;
            _logService = logService;
            _themeService = themeService;
            _dragDropService = dragDropService;

            _dragDropService.FoldersDropped += OnFoldersDropped;
            _dragDropService.SessionFileDropped += OnSessionFileDropped;

            // Initialize saved settings
            if (!string.IsNullOrEmpty(_settingsService.TmdbApiKey))
            {
                _metadataService.Configure(_settingsService.TmdbApiKey);
            }
            _metadataService.ConfigureLanguage(_settingsService.TmdbLanguage);
        }

        [RelayCommand]
        public async Task InitializeAsync()
        {
            if (!_servicesInitialized)
            {
                _servicesInitialized = true;
                await _logService.InitializeAsync();
                await _undoRedoService.LoadHistoryAsync();
            }
            await _themeService.InitializeAsync();
        }

        [RelayCommand]
        public void CancelOperation()
        {
            _operationCts?.Cancel();
        }

        public void SetDragging(bool dragging)
        {
            IsDragging = dragging;
        }

        public Task HandleDropAsync()
        {
            IsDragging = false;
            return Task.CompletedTask;
        }

        private async void OnFoldersDropped(IReadOnlyList<string> folders)
        {
            try
            {
                IsDragging = false;
                await LoadFoldersAsync(new System.Collections.Generic.List<string>(folders));
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error al cargar carpetas: {ex.Message}";
            }
        }

        private async void OnSessionFileDropped(string jsonFilePath)
        {
            try
            {
                IsDragging = false;
                await ImportSessionFromFileAsync(jsonFilePath);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error al importar sesión: {ex.Message}";
            }
        }

        [ObservableProperty]
        private bool _showingLoadConfirm;

        private List<string> _pendingFoldersToLoad = new();

        [RelayCommand]
        private void ConfirmLoadFolders()
        {
            ShowingLoadConfirm = false;
            _ = LoadFoldersInternalAsync(_pendingFoldersToLoad);
        }

        [RelayCommand]
        private void CancelLoadFolders()
        {
            ShowingLoadConfirm = false;
            _pendingFoldersToLoad = new();
        }

        [RelayCommand]
        private void ShowFolderBrowser() => ShowingFolderBrowser = true;

        [RelayCommand]
        private void HideFolderBrowser() => ShowingFolderBrowser = false;

        [RelayCommand]
        private async Task LoadFoldersAsync(List<string> folders)
        {
            ShowingFolderBrowser = false;
            if (!folders.Any()) return;

            if (Files.Count > 0)
            {
                _pendingFoldersToLoad = folders;
                ShowingLoadConfirm = true;
                return;
            }

            await LoadFoldersInternalAsync(folders);
        }

        private async Task LoadFoldersInternalAsync(List<string> folders)
        {
            IsProcessing = true;
            UpdateProgress("Cargando archivos...", 0, 1);

            try
            {
                Files.Clear();
                var filePaths = await _fileOrganizerService.LoadVideoFilesAsync(folders);

                foreach (var path in filePaths)
                {
                    Files.Add(new FileViewModel
                    {
                        OriginalName = Path.GetFileName(path),
                        FullPath = path,
                        Status = FileStatus.Pending
                    });
                }

                // Auto-analyze with local parser (FEAT-01)
                AutoAnalyzeWithLocalParser();

                StatusMessage = $"Carga completada: {Files.Count} archivos de video encontrados";
                OnPropertyChanged(nameof(FileCounts));
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task AnalyzeWithAIAsync()
        {
            if (!Files.Any()) return;

            if (!_aiService.IsAvailable())
            {
                StatusMessage = "Error: El servicio de IA no está disponible";
                return;
            }

            _operationCts?.Cancel();
            _operationCts?.Dispose();
            _operationCts = new CancellationTokenSource();
            var ct = _operationCts.Token;

            IsProcessing = true;
            ProgressTotal = Files.Count;
            ProgressCurrent = 0;

            try
            {
                foreach (var file in Files)
                {
                    ct.ThrowIfCancellationRequested();

                    ProgressCurrent++;
                    UpdateProgress($"Analizando: {file.OriginalName}", ProgressCurrent, ProgressTotal);

                    try
                    {
                        var result = await _aiService.AnalyzeFilenameAsync(file.OriginalName, ct);
                        if (result != null)
                        {
                            file.SeriesTitle = result.Title ?? "";
                            file.Season = result.Season;
                            file.Episode = result.Episode;
                            file.EpisodeTitle = result.EpisodeTitle ?? "";
                            file.Status = FileStatus.Analyzed;
                            UpdatePreviewForFile(file);
                        }
                        else
                        {
                            file.Status = FileStatus.Error;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        file.Status = FileStatus.Error;
                    }
                }

                StatusMessage = $"Análisis completado: {Files.Count(f => f.Status == FileStatus.Analyzed)} archivos analizados";
                OnPropertyChanged(nameof(FileCounts));
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Análisis cancelado";
                OnPropertyChanged(nameof(FileCounts));
            }
            finally
            {
                _operationCts.Dispose();
                _operationCts = null;
                IsProcessing = false;
            }
        }

        [RelayCommand]
        public void SelectFile(FileViewModel file)
        {
            var previousFile = SelectedFile;

            foreach (var f in Files) f.IsSelected = false;
            file.IsSelected = true;
            SelectedFile = file;

            // Maintain mode: auto-fill only if the file has no metadata yet
            if (CurrentMode == RenameMode.Maintain
                && previousFile != null
                && !string.IsNullOrEmpty(previousFile.SeriesTitle)
                && string.IsNullOrEmpty(file.SeriesTitle))
            {
                file.SeriesTitle = previousFile.SeriesTitle;
                file.Season = previousFile.Season;
                file.Episode = previousFile.Episode + 1;
                file.Status = FileStatus.Analyzed;
            }

            UpdatePreviewForFile(file);
        }

        [RelayCommand]
        private async Task RenameAllAsync()
        {
            var filesToRename = Files.Where(f => f.Status == FileStatus.Analyzed && f.IsChecked).ToList();
            if (!filesToRename.Any()) return;

            // Validate: in Change mode, every file must have an episode number
            if (CurrentMode == RenameMode.Change)
            {
                var invalid = filesToRename.Where(f => f.Episode <= 0).ToList();
                if (invalid.Any())
                {
                    StatusMessage = $"Error: {invalid.Count} archivo(s) sin número de episodio válido. Corrígelos antes de renombrar.";
                    return;
                }
            }

            // Validate: in Movie mode, every file must have a title
            if (CurrentMode == RenameMode.Movie)
            {
                var invalid = filesToRename.Where(f => string.IsNullOrWhiteSpace(f.SeriesTitle)).ToList();
                if (invalid.Any())
                {
                    StatusMessage = $"Error: {invalid.Count} película(s) sin título de película asignado. Corrígelos antes de renombrar.";
                    return;
                }
            }

            // FEAT-06: Show dry-run confirmation with exact previews (including Maintain sequential numbering & template)
            var strategy = _strategyFactory.CreateStrategy(CurrentMode);
            string? maintainTitle = CurrentMode == RenameMode.Maintain ? filesToRename[0].SeriesTitle : null;
            int maintainSeason = CurrentMode == RenameMode.Maintain ? filesToRename[0].Season : 0;
            int maintainEp = CurrentMode == RenameMode.Maintain ? filesToRename[0].Episode : 0;

            _dryRunPreview = new List<(string original, string preview)>();
            foreach (var f in filesToRename)
            {
                var info = new ChapterInfo
                {
                    Title = maintainTitle ?? f.SeriesTitle,
                    Season = maintainTitle != null ? maintainSeason : f.Season,
                    Episode = maintainTitle != null ? maintainEp++ : f.Episode,
                    EpisodeTitle = f.EpisodeTitle,
                    Year = f.Year
                };
                var preview = strategy.GetNewFileName(f.OriginalName, info, _settingsService.FileNameTemplate);
                _dryRunPreview.Add((f.OriginalName, string.IsNullOrEmpty(preview) ? "(sin vista previa)" : preview));
            }
            ShowingDryRun = true;
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task ConfirmDryRunAsync()
        {
            ShowingDryRun = false;
            var filesToRename = Files.Where(f => f.Status == FileStatus.Analyzed && f.IsChecked).ToList();
            if (!filesToRename.Any()) return;

            _operationCts?.Cancel();
            _operationCts?.Dispose();
            _operationCts = new CancellationTokenSource();
            var ct = _operationCts.Token;

            IsProcessing = true;
            ProgressTotal = filesToRename.Count;
            ProgressCurrent = 0;

            if (CurrentMode == RenameMode.Maintain && filesToRename.Any())
            {
                var firstFile = filesToRename.First();
                _maintainSeriesTitle = firstFile.SeriesTitle;
                _maintainSeason = firstFile.Season;
                _maintainNextEpisode = firstFile.Episode;
            }

            var batchUndoOperations = new List<RenameOperation>();

            try
            {
                foreach (var file in filesToRename)
                {
                    ct.ThrowIfCancellationRequested();
                    ProgressCurrent++;
                    UpdateProgress($"Renombrando: {file.OriginalName}", ProgressCurrent, ProgressTotal);
                    var ops = await RenameFileInternalAsync(file, ct);
                    batchUndoOperations.AddRange(ops);
                }

                if (batchUndoOperations.Count > 0)
                {
                    _undoRedoService.RecordOperations(batchUndoOperations);
                }

                StatusMessage = $"Renombrado completado: {Files.Count(f => f.Status == FileStatus.Done)} archivos";
                OnPropertyChanged(nameof(FileCounts));
            }
            catch (OperationCanceledException)
            {
                if (batchUndoOperations.Count > 0)
                {
                    _undoRedoService.RecordOperations(batchUndoOperations);
                }
                StatusMessage = "Renombrado cancelado";
                OnPropertyChanged(nameof(FileCounts));
            }
            finally
            {
                _maintainSeriesTitle = null;
                _operationCts?.Dispose();
                _operationCts = null;
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private void CancelDryRun() => ShowingDryRun = false;

        private async Task<List<RenameOperation>> RenameFileInternalAsync(FileViewModel file, CancellationToken ct = default)
        {
            var recorded = new List<RenameOperation>();
            var oldPath = file.FullPath;
            try
            {
                var chapterInfo = new ChapterInfo
                {
                    Title = file.SeriesTitle,
                    Season = file.Season,
                    Episode = file.Episode,
                    EpisodeTitle = file.EpisodeTitle,
                    Year = file.Year
                };

                if (CurrentMode == RenameMode.Maintain && _maintainSeriesTitle != null)
                {
                    chapterInfo.Title = _maintainSeriesTitle;
                    chapterInfo.Season = _maintainSeason;
                    chapterInfo.Episode = _maintainNextEpisode;
                    _maintainNextEpisode++;
                }

                var strategy = _strategyFactory.CreateStrategy(CurrentMode);
                var (newPath, companionMoves) = await _fileOrganizerService.RenameFileWithCompanionsAsync(
                    oldPath,
                    chapterInfo,
                    strategy,
                    ct,
                    fileNameTemplate: _settingsService.FileNameTemplate,
                    renameCompanions: _settingsService.RenameCompanionFiles);

                if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
                {
                    recorded.Add(new RenameOperation { OldPath = oldPath, NewPath = newPath });
                    _logService.LogRename(oldPath, newPath, success: true);
                }

                foreach (var (compOld, compNew) in companionMoves)
                {
                    recorded.Add(new RenameOperation { OldPath = compOld, NewPath = compNew });
                    _logService.LogRename(compOld, compNew, success: true);
                }

                file.FullPath = newPath;
                file.OriginalName = Path.GetFileName(newPath);
                file.Status = FileStatus.Done;
                file.NewName = "";

                // Update displayed metadata
                file.SeriesTitle = chapterInfo.Title;
                file.Season = chapterInfo.Season;
                file.Episode = chapterInfo.Episode;
                file.Year = chapterInfo.Year;
            }
            catch (Exception ex)
            {
                file.Status = FileStatus.Error;
                _logService.LogRename(oldPath, oldPath, success: false, error: ex.Message);
                StatusMessage = $"Error: {ex.Message}";
            }
            return recorded;
        }

        [RelayCommand]
        private async Task UndoAsync()
        {
            IsProcessing = true;
            StatusMessage = "Deshaciendo...";

            try
            {
                var result = await _undoRedoService.UndoAsync();

                if (result.success && result.count > 0)
                {
                    ApplyFilePathChanges(result.appliedOperations, isCompletedOperation: false);

                    foreach (var operation in result.appliedOperations)
                    {
                        _logService.LogUndo(operation.OldPath, operation.NewPath);
                    }
                }

                StatusMessage = result.message;
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task UndoAllAsync()
        {
            if (!_undoRedoService.CanUndo) return;

            IsProcessing = true;
            StatusMessage = "Deshaciendo todas las operaciones...";

            int totalUndone = 0;

            try
            {
                while (_undoRedoService.CanUndo)
                {
                    try
                    {
                        var result = await _undoRedoService.UndoAsync();
                        if (!result.success) break;

                        ApplyFilePathChanges(result.appliedOperations, isCompletedOperation: false);

                        foreach (var operation in result.appliedOperations)
                        {
                            _logService.LogUndo(operation.OldPath, operation.NewPath);
                        }

                        totalUndone += result.count;
                    }
                    catch
                    {
                        break;
                    }
                }

                StatusMessage = totalUndone > 0 ? $"Deshecho: {totalUndone} archivo(s)" : "No hubo operaciones deshechas";

                // Refresh file list state
                OnPropertyChanged(nameof(FileCounts));
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task RedoAsync()
        {
            IsProcessing = true;
            StatusMessage = "Rehaciendo...";

            try
            {
                var result = await _undoRedoService.RedoAsync();

                if (result.success && result.count > 0)
                {
                    ApplyFilePathChanges(result.appliedOperations, isCompletedOperation: true);

                    foreach (var operation in result.appliedOperations)
                    {
                        _logService.LogRedo(operation.OldPath, operation.NewPath);
                    }
                }

                StatusMessage = result.message;
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        public void UpdatePreviewForFile(FileViewModel? file = null)
        {
            file ??= SelectedFile;
            if (file == null) return;

            try
            {
                if (string.IsNullOrWhiteSpace(file.SeriesTitle))
                {
                    file.NewName = "";
                    return;
                }

                var chapterInfo = new ChapterInfo
                {
                    Title = file.SeriesTitle,
                    Season = file.Season,
                    Episode = file.Episode,
                    EpisodeTitle = file.EpisodeTitle,
                    Year = file.Year
                };

                var strategy = _strategyFactory.CreateStrategy(CurrentMode);
                file.NewName = strategy.GetNewFileName(file.OriginalName, chapterInfo, _settingsService.FileNameTemplate);
            }
            catch
            {
                file.NewName = "Error";
            }
        }

        [RelayCommand]
        public void RefreshAllPreviews()
        {
            foreach (var file in Files.Where(f => f.Status == FileStatus.Analyzed || !string.IsNullOrWhiteSpace(f.SeriesTitle)))
            {
                UpdatePreviewForFile(file);
            }
        }

        [RelayCommand]
        public void ApplySeriesTitleToAll(string? seriesTitle)
        {
            if (string.IsNullOrWhiteSpace(seriesTitle)) return;
            foreach (var file in Files.Where(f => f.IsChecked))
            {
                file.SeriesTitle = seriesTitle.Trim();
                if (file.Status == FileStatus.Pending)
                {
                    file.Status = FileStatus.Analyzed;
                }
                UpdatePreviewForFile(file);
            }
            StatusMessage = $"Título \"{seriesTitle.Trim()}\" aplicado a {Files.Count(f => f.IsChecked)} archivo(s)";
            OnPropertyChanged(nameof(FileCounts));
        }

        [RelayCommand]
        public void ApplySeasonToAll(int season)
        {
            if (season < 0) return;
            foreach (var file in Files.Where(f => f.IsChecked))
            {
                file.Season = season;
                UpdatePreviewForFile(file);
            }
            StatusMessage = $"Temporada {season} aplicada a {Files.Count(f => f.IsChecked)} archivo(s)";
        }

        [RelayCommand]
        public void RenumberEpisodesSequentially(int startEpisode = 1)
        {
            int ep = Math.Max(1, startEpisode);
            foreach (var file in Files.Where(f => f.IsChecked))
            {
                file.Episode = ep++;
                if (file.Status == FileStatus.Pending && !string.IsNullOrWhiteSpace(file.SeriesTitle))
                {
                    file.Status = FileStatus.Analyzed;
                }
                UpdatePreviewForFile(file);
            }
            StatusMessage = $"Episodios renumerados secuencialmente desde {Math.Max(1, startEpisode)}";
            OnPropertyChanged(nameof(FileCounts));
        }

        [RelayCommand]
        public void ToggleAllChecked(bool isChecked)
        {
            foreach (var file in Files)
            {
                file.IsChecked = isChecked;
            }
            OnPropertyChanged(nameof(FileCounts));
        }

        [RelayCommand]
        public void ChangeMode(RenameMode mode)
        {
            CurrentMode = mode;
            RefreshAllPreviews();
        }

        [RelayCommand]
        public void ApplySeriesMetadata(string seriesName)
        {
            foreach (var file in Files.Where(f => f.IsChecked && string.IsNullOrEmpty(f.SeriesTitle)))
            {
                file.SeriesTitle = seriesName;
            }

            RefreshAllPreviews();

            StatusMessage = $"Metadatos TMDB aplicados. Serie: {seriesName}";
            ShowingTmdbSearch = false;
            OnPropertyChanged(nameof(FileCounts));
        }

        // Dialog Commands
        [RelayCommand] private void OpenSettings() => ShowingSettings = true;
        [RelayCommand] private void CloseSettings() => ShowingSettings = false;
        [RelayCommand] private void OpenTmdbSearch() => ShowingTmdbSearch = true;
        [RelayCommand] private void CloseTmdbSearch() => ShowingTmdbSearch = false;
        [RelayCommand] private void OpenSaveAll() => ShowingSaveAll = true;
        [RelayCommand] private void CloseSaveAll() => ShowingSaveAll = false;
        [RelayCommand] private void OpenOperationLog() => ShowingOperationLog = true;
        [RelayCommand] private void CloseOperationLog() => ShowingOperationLog = false;

        // FEAT-04: Export session
        [RelayCommand]
        private async Task ExportSessionAsync()
        {
            if (!Files.Any()) return;
            try
            {
                var exportData = Files.Select(f => new
                {
                    f.OriginalName,
                    f.FullPath,
                    f.SeriesTitle,
                    f.Season,
                    f.Episode,
                    f.EpisodeTitle,
                    f.Year,
                    f.IsChecked,
                    Status = f.Status.ToString()
                }).ToList();
                var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var exportPath = Path.Combine(desktopPath, $"session_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                await File.WriteAllTextAsync(exportPath, json);
                StatusMessage = $"Sesión exportada a: {exportPath}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error exportando sesión: {ex.Message}";
            }
        }

        // FEAT-04: Show import session
        [RelayCommand]
        private async Task ShowImportSessionAsync()
        {
            StatusMessage = "Arrastra un archivo .json de sesión exportada sobre la ventana para importarlo";
            await Task.CompletedTask;
        }

        public async Task ImportSessionFromFileAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                StatusMessage = "El archivo de sesión no existe";
                return;
            }

            var json = await File.ReadAllTextAsync(filePath);
            await ImportSessionFromJsonAsync(json);
        }

        public async Task ImportSessionFromJsonAsync(string json)
        {
            try
            {
                var items = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(json);
                if (items == null) return;
                Files.Clear();
                foreach (var item in items)
                {
                    var originalName = item.TryGetProperty("OriginalName", out var p) ? p.GetString() ?? "" : "";
                    var fullPath = item.TryGetProperty("FullPath", out var fp) ? fp.GetString() ?? originalName : originalName;
                    var file = new FileViewModel
                    {
                        OriginalName = originalName,
                        FullPath = fullPath,
                        SeriesTitle = item.TryGetProperty("SeriesTitle", out var s) ? s.GetString() ?? "" : "",
                        Season = item.TryGetProperty("Season", out var sea) ? sea.GetInt32() : 1,
                        Episode = item.TryGetProperty("Episode", out var ep) ? ep.GetInt32() : 1,
                        EpisodeTitle = item.TryGetProperty("EpisodeTitle", out var et) ? et.GetString() ?? "" : "",
                        Year = item.TryGetProperty("Year", out var yr) ? yr.GetInt32() : 0,
                        IsChecked = !item.TryGetProperty("IsChecked", out var chk) || chk.GetBoolean()
                    };
                    if (Enum.TryParse<FileStatus>(item.TryGetProperty("Status", out var st) ? st.GetString() : null, out var status))
                        file.Status = status;
                    UpdatePreviewForFile(file);
                    Files.Add(file);
                }
                StatusMessage = $"Sesión importada: {Files.Count} archivos";
                OnPropertyChanged(nameof(FileCounts));
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error leyendo sesión: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task MoveFilesAsync(List<string> folders)
        {
            ShowingSaveAll = false;
            if (!folders.Any()) return;

            string destinationFolder = folders[0];
            var filesToMove = Files.Where(f => f.IsChecked).ToList();
            if (!filesToMove.Any()) return;

            IsProcessing = true;
            ProgressTotal = filesToMove.Count;
            ProgressCurrent = 0;

            try
            {
                var items = filesToMove.Select(file => (file.FullPath, (ChapterInfo?)new ChapterInfo
                {
                    Title = file.SeriesTitle,
                    Season = file.Season,
                    Episode = file.Episode,
                    EpisodeTitle = file.EpisodeTitle,
                    Year = file.Year
                })).ToList();

                var movedFiles = await _fileOrganizerService.MoveFilesStructuredAsync(
                    items,
                    destinationFolder,
                    _settingsService.OrganizeIntoSubfolders);
                var movedBySource = movedFiles.ToDictionary(move => move.oldPath, move => move.newPath);

                foreach (var file in filesToMove)
                {
                    ProgressCurrent++;
                    UpdateProgress($"Moviendo: {file.OriginalName}", ProgressCurrent, ProgressTotal);

                    if (!movedBySource.TryGetValue(file.FullPath, out var newPath))
                    {
                        continue;
                    }

                    file.FullPath = newPath;
                    file.OriginalName = Path.GetFileName(newPath);
                    file.Status = FileStatus.Done;
                }

                _undoRedoService.RecordOperations(movedFiles
                    .Select(move => new RenameOperation { OldPath = move.oldPath, NewPath = move.newPath })
                    .ToList());

                foreach (var movedFile in movedFiles)
                {
                    _logService.LogMove(Path.GetFileName(movedFile.oldPath), Path.GetDirectoryName(movedFile.newPath) ?? destinationFolder);
                }

                StatusMessage = $"Todos los archivos movidos a: {destinationFolder}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error al mover archivos: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task RenameSelectedAsync()
        {
            if (SelectedFile != null)
            {
                var ops = await RenameFileInternalAsync(SelectedFile);
                if (ops.Count > 0)
                {
                    _undoRedoService.RecordOperations(ops);
                }
                OnPropertyChanged(nameof(FileCounts));
            }
        }

        private void UpdateProgress(string message, int current, int total)
        {
            ProgressMessage = message;
            ProgressCurrent = current;
            ProgressTotal = total;
        }

        /// <summary>
        /// FEAT-01: Runs EpisodePatternParser on every Pending file immediately after loading.
        /// Files that match a pattern are marked as Analyzed so the user can rename without AI.
        /// </summary>
        private void AutoAnalyzeWithLocalParser()
        {
            int autoAnalyzed = 0;
            foreach (var file in Files.Where(f => f.Status == FileStatus.Pending))
            {
                if (EpisodePatternParser.TryExtractSeasonEpisode(file.OriginalName, out int season, out int episode))
                {
                    file.Season = season;
                    file.Episode = episode;
                    file.SeriesTitle = EpisodePatternParser.ExtractSeriesTitle(file.OriginalName);
                    file.EpisodeTitle = EpisodePatternParser.ExtractEpisodeTitle(file.OriginalName);
                    file.Status = FileStatus.Analyzed;
                    UpdatePreviewForFile(file);
                    autoAnalyzed++;
                }
                else if (EpisodePatternParser.TryExtractYear(file.OriginalName, out int year))
                {
                    file.Year = year;
                    file.SeriesTitle = EpisodePatternParser.ExtractMovieTitle(file.OriginalName);
                    file.Season = 0;
                    file.Episode = 0;
                    file.EpisodeTitle = string.Empty;
                    file.Status = FileStatus.Analyzed;
                    UpdatePreviewForFile(file);
                    autoAnalyzed++;
                }
            }
            if (autoAnalyzed > 0)
            {
                StatusMessage = $"Carga completada: {Files.Count} archivos ({autoAnalyzed} auto-analizados localmente)";
                OnPropertyChanged(nameof(FileCounts));
                OnPropertyChanged(nameof(AnalyzedCount));
                OnPropertyChanged(nameof(PendingCount));
            }
        }

        private void ApplyFilePathChanges(IEnumerable<RenameOperation> operations, bool isCompletedOperation)
        {
            foreach (var operation in operations)
            {
                var file = Files.FirstOrDefault(item => string.Equals(item.FullPath, operation.OldPath, StringComparison.OrdinalIgnoreCase));
                if (file == null)
                {
                    continue;
                }

                file.FullPath = operation.NewPath;
                file.OriginalName = Path.GetFileName(operation.NewPath);

                if (isCompletedOperation)
                {
                    file.Status = FileStatus.Done;
                    file.NewName = string.Empty;
                    continue;
                }

                file.Status = string.IsNullOrWhiteSpace(file.SeriesTitle)
                    ? FileStatus.Pending
                    : FileStatus.Analyzed;

                UpdatePreviewForFile(file);
            }

            OnPropertyChanged(nameof(FileCounts));
        }
    }
}

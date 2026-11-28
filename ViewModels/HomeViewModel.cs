using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using organizadorCapitulos.Application.Services;
using organizadorCapitulos.Application.Strategies;
using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Enums;
using organizadorCapitulos.Core.Interfaces.Services;
using OrganizadorCapitulos.Maui.Models;
using OrganizadorCapitulos.Maui.Services;
using OrganizadorCapitulos.Maui.Services.Interfaces;
using OrganizadorCapitulos.Maui.Services.Workflows;

namespace OrganizadorCapitulos.Maui.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        private static readonly JsonSerializerOptions SessionJsonOptions = new() { WriteIndented = true };

        private readonly FileOrganizerService _fileOrganizerService;
        private readonly IAIService _aiService;
        private readonly RenameStrategyFactory _strategyFactory;
        private readonly IMetadataService _metadataService;
        private readonly SettingsService _settingsService;
        private readonly UndoRedoService _undoRedoService;
        private readonly OperationLogService _logService;
        private readonly IThemeService _themeService;
        private readonly IDragDropService _dragDropService;
        private readonly FileAnalysisWorkflow _analysisWorkflow;
        private readonly FileOrganizationWorkflow _organizationWorkflow;
        private readonly ILogger<HomeViewModel> _logger;

        private CancellationTokenSource? _operationCts;
        private bool _servicesInitialized;
        private long _lastProgressTicks;

        private bool _metricsDirty = true;
        private int _analyzedCount;
        private int _pendingCount;
        private int _errorCount;
        private int _readyCheckedCount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FileCounts))]
        [NotifyPropertyChangedFor(nameof(AnalyzedCount))]
        [NotifyPropertyChangedFor(nameof(PendingCount))]
        [NotifyPropertyChangedFor(nameof(ErrorCount))]
        [NotifyPropertyChangedFor(nameof(ReadyCheckedCount))]
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
        [NotifyPropertyChangedFor(nameof(ProgressPercent))]
        private int _progressCurrent;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ProgressPercent))]
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

        public int AnalyzedCount
        {
            get { EnsureMetricsUpToDate(); return _analyzedCount; }
        }

        public int PendingCount
        {
            get { EnsureMetricsUpToDate(); return _pendingCount; }
        }

        public int ErrorCount
        {
            get { EnsureMetricsUpToDate(); return _errorCount; }
        }

        public int ReadyCheckedCount
        {
            get { EnsureMetricsUpToDate(); return _readyCheckedCount; }
        }

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
            IDragDropService dragDropService,
            FileAnalysisWorkflow? analysisWorkflow = null,
            FileOrganizationWorkflow? organizationWorkflow = null,
            ILogger<HomeViewModel>? logger = null)
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
            _analysisWorkflow = analysisWorkflow ?? new FileAnalysisWorkflow(fileOrganizerService, aiService);
            _organizationWorkflow = organizationWorkflow ?? new FileOrganizationWorkflow(fileOrganizerService, strategyFactory);
            _logger = logger ?? NullLogger<HomeViewModel>.Instance;

            _dragDropService.FoldersDropped += OnFoldersDropped;
            _dragDropService.SessionFileDropped += OnSessionFileDropped;
            AttachFilesCollection(_files);

            // Initialize saved settings
            if (!string.IsNullOrEmpty(_settingsService.TmdbApiKey))
            {
                _metadataService.Configure(_settingsService.TmdbApiKey);
            }
            _metadataService.ConfigureLanguage(_settingsService.TmdbLanguage);
        }

        partial void OnFilesChanging(ObservableCollection<FileViewModel>? oldValue, ObservableCollection<FileViewModel> newValue)
        {
            if (oldValue != null)
            {
                DetachFilesCollection(oldValue);
            }
        }

        partial void OnFilesChanged(ObservableCollection<FileViewModel> value)
        {
            AttachFilesCollection(value);
            _metricsDirty = true;
        }

        private void AttachFilesCollection(ObservableCollection<FileViewModel> collection)
        {
            collection.CollectionChanged += OnFilesCollectionChanged;
            foreach (var item in collection)
            {
                item.PropertyChanged += OnFileItemPropertyChanged;
            }
        }

        private void DetachFilesCollection(ObservableCollection<FileViewModel> collection)
        {
            collection.CollectionChanged -= OnFilesCollectionChanged;
            foreach (var item in collection)
            {
                item.PropertyChanged -= OnFileItemPropertyChanged;
            }
        }

        private void OnFilesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (FileViewModel item in e.OldItems)
                {
                    item.PropertyChanged -= OnFileItemPropertyChanged;
                }
            }
            if (e.NewItems != null)
            {
                foreach (FileViewModel item in e.NewItems)
                {
                    item.PropertyChanged += OnFileItemPropertyChanged;
                }
            }
            _metricsDirty = true;
        }

        private void OnFileItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(FileViewModel.Status) or nameof(FileViewModel.IsChecked))
            {
                _metricsDirty = true;
            }
        }

        private void EnsureMetricsUpToDate()
        {
            if (!_metricsDirty) return;

            int analyzed = 0, pending = 0, error = 0, readyChecked = 0;
            foreach (var f in Files)
            {
                switch (f.Status)
                {
                    case FileStatus.Analyzed:
                        analyzed++;
                        if (f.IsChecked) readyChecked++;
                        break;
                    case FileStatus.Pending:
                        pending++;
                        break;
                    case FileStatus.Error:
                        error++;
                        break;
                }
            }

            _analyzedCount = analyzed;
            _pendingCount = pending;
            _errorCount = error;
            _readyCheckedCount = readyChecked;
            _metricsDirty = false;
        }

        private void NotifyFileMetricsChanged()
        {
            _metricsDirty = true;
            EnsureMetricsUpToDate();
            OnPropertyChanged(nameof(FileCounts));
            OnPropertyChanged(nameof(AnalyzedCount));
            OnPropertyChanged(nameof(PendingCount));
            OnPropertyChanged(nameof(ErrorCount));
            OnPropertyChanged(nameof(ReadyCheckedCount));
        }

        [RelayCommand]
        public async Task InitializeAsync()
        {
            if (!_servicesInitialized)
            {
                _servicesInitialized = true;
                await Task.WhenAll(
                    _logService.InitializeAsync(),
                    _undoRedoService.LoadHistoryAsync(),
                    _themeService.InitializeAsync());
            }
            else
            {
                await _themeService.InitializeAsync();
            }
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
                await LoadFoldersAsync(new List<string>(folders));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading dropped folders");
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
                _logger.LogWarning(ex, "Error importing dropped session file {Path}", jsonFilePath);
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
                var result = await _analysisWorkflow.LoadAndAnalyzeLocalAsync(folders);
                var strategy = _strategyFactory.CreateStrategy(CurrentMode);
                var template = _settingsService.FileNameTemplate;
                var loaded = new List<FileViewModel>(result.Files.Count);

                foreach (var item in result.Files)
                {
                    var vm = new FileViewModel
                    {
                        OriginalName = item.OriginalName,
                        FullPath = item.FullPath,
                        Status = item.IsAnalyzed ? FileStatus.Analyzed : FileStatus.Pending,
                        SeriesTitle = item.SeriesTitle,
                        Season = item.Season,
                        Episode = item.Episode,
                        EpisodeTitle = item.EpisodeTitle,
                        Year = item.Year
                    };

                    if (item.IsAnalyzed && !string.IsNullOrWhiteSpace(vm.SeriesTitle))
                    {
                        vm.NewName = strategy.GetNewFileName(
                            vm.OriginalName,
                            new ChapterInfo
                            {
                                Title = vm.SeriesTitle,
                                Season = vm.Season,
                                Episode = vm.Episode,
                                EpisodeTitle = vm.EpisodeTitle,
                                Year = vm.Year
                            },
                            template);
                    }

                    loaded.Add(vm);
                }

                SelectedFile = null;
                Files = new ObservableCollection<FileViewModel>(loaded);
                NotifyFileMetricsChanged();

                StatusMessage = result.AutoAnalyzedCount > 0
                    ? $"Carga completada: {Files.Count} archivos ({result.AutoAnalyzedCount} auto-analizados localmente)"
                    : $"Carga completada: {Files.Count} archivos de video encontrados";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load folders");
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

            if (!_analysisWorkflow.IsAIAvailable())
            {
                StatusMessage = "Error: El servicio de IA no está disponible";
                return;
            }

            _operationCts?.Cancel();
            _operationCts?.Dispose();
            _operationCts = new CancellationTokenSource();
            var ct = _operationCts.Token;

            var filesSnapshot = Files.ToList();
            var fileNames = filesSnapshot.Select(f => f.OriginalName).ToList();

            IsProcessing = true;
            _lastProgressTicks = 0;
            UpdateProgress("Analizando archivos...", 0, filesSnapshot.Count);

            try
            {
                var batch = await _analysisWorkflow.AnalyzeWithAIAsync(
                    fileNames,
                    (current, total, fileName) => UpdateProgressThrottled($"Analizando: {fileName}", current, total),
                    ct);

                foreach (var itemResult in batch.Results)
                {
                    var file = filesSnapshot[itemResult.Index];
                    if (itemResult.Success && itemResult.Metadata != null)
                    {
                        file.SeriesTitle = itemResult.Metadata.Title ?? "";
                        file.Season = itemResult.Metadata.Season;
                        file.Episode = itemResult.Metadata.Episode;
                        file.EpisodeTitle = itemResult.Metadata.EpisodeTitle ?? "";
                        file.Status = FileStatus.Analyzed;
                        UpdatePreviewForFile(file);
                    }
                    else
                    {
                        file.Status = FileStatus.Error;
                    }
                }

                NotifyFileMetricsChanged();
                StatusMessage = batch.Cancelled
                    ? "Análisis cancelado"
                    : $"Análisis completado: {AnalyzedCount} archivos analizados";
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

            if (previousFile != null && !ReferenceEquals(previousFile, file))
            {
                previousFile.IsSelected = false;
            }
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
                NotifyFileMetricsChanged();
            }

            UpdatePreviewForFile(file);
        }

        [RelayCommand]
        public void ClearErrors()
        {
            var remaining = Files.Where(f => f.Status != FileStatus.Error).ToList();
            if (remaining.Count == Files.Count) return;

            if (SelectedFile?.Status == FileStatus.Error)
            {
                SelectedFile = null;
            }
            Files = new ObservableCollection<FileViewModel>(remaining);
            NotifyFileMetricsChanged();
            StatusMessage = "Incidencias limpiadas de la lista";
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

            // FEAT-06: Show dry-run confirmation with exact previews
            var batchItems = filesToRename.Select(ToRenameBatchItem).ToList();
            _dryRunPreview = _organizationWorkflow.BuildDryRunPreview(batchItems, CurrentMode, _settingsService.FileNameTemplate);
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
            _lastProgressTicks = 0;
            UpdateProgress("Renombrando archivos...", 0, filesToRename.Count);

            try
            {
                var batchItems = filesToRename.Select(ToRenameBatchItem).ToList();
                var batchResult = await _organizationWorkflow.RenameBatchAsync(
                    batchItems,
                    CurrentMode,
                    _settingsService.FileNameTemplate,
                    _settingsService.RenameCompanionFiles,
                    (current, total, name) => UpdateProgressThrottled($"Renombrando: {name}", current, total),
                    ct);

                string? lastError = null;
                int doneCount = 0;

                foreach (var itemResult in batchResult.ItemResults)
                {
                    var file = filesToRename[itemResult.Index];
                    if (itemResult.Success)
                    {
                        file.FullPath = itemResult.NewPath;
                        file.OriginalName = Path.GetFileName(itemResult.NewPath);
                        file.Status = FileStatus.Done;
                        file.NewName = "";
                        file.SeriesTitle = itemResult.AppliedInfo.Title;
                        file.Season = itemResult.AppliedInfo.Season;
                        file.Episode = itemResult.AppliedInfo.Episode;
                        file.Year = itemResult.AppliedInfo.Year;
                        doneCount++;
                    }
                    else
                    {
                        file.Status = FileStatus.Error;
                        lastError = itemResult.ErrorMessage;
                    }
                }

                if (batchResult.UndoOperations.Count > 0)
                {
                    _undoRedoService.RecordOperations(batchResult.UndoOperations);
                }

                if (batchResult.LogEntries.Count > 0)
                {
                    _logService.LogBatch(batchResult.LogEntries);
                }

                NotifyFileMetricsChanged();

                if (batchResult.Cancelled)
                {
                    StatusMessage = "Renombrado cancelado";
                }
                else if (lastError != null && doneCount == 0)
                {
                    StatusMessage = $"Error: {lastError}";
                }
                else
                {
                    StatusMessage = $"Renombrado completado: {Files.Count(f => f.Status == FileStatus.Done)} archivos";
                }
            }
            finally
            {
                _operationCts?.Dispose();
                _operationCts = null;
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private void CancelDryRun() => ShowingDryRun = false;

        private static RenameBatchItem ToRenameBatchItem(FileViewModel f) =>
            new(f.FullPath, f.OriginalName, f.SeriesTitle, f.Season, f.Episode, f.EpisodeTitle, f.Year);

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
                    _logService.LogBatch(result.appliedOperations.Select(operation => new LogEntry
                    {
                        Action = "Undo",
                        OldName = Path.GetFileName(operation.OldPath),
                        NewName = Path.GetFileName(operation.NewPath),
                        Status = "Success"
                    }));
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

            try
            {
                var result = await _undoRedoService.UndoAllAsync();
                if (result.success && result.count > 0)
                {
                    ApplyFilePathChanges(result.appliedOperations, isCompletedOperation: false);
                    _logService.LogBatch(result.appliedOperations.Select(operation => new LogEntry
                    {
                        Action = "Undo",
                        OldName = Path.GetFileName(operation.OldPath),
                        NewName = Path.GetFileName(operation.NewPath),
                        Status = "Success"
                    }));
                }

                StatusMessage = result.count > 0 ? $"Deshecho: {result.count} archivo(s)" : "No hubo operaciones deshechas";
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
                    _logService.LogBatch(result.appliedOperations.Select(operation => new LogEntry
                    {
                        Action = "Redo",
                        OldName = Path.GetFileName(operation.OldPath),
                        NewName = Path.GetFileName(operation.NewPath),
                        Status = "Success"
                    }));
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
            var strategy = _strategyFactory.CreateStrategy(CurrentMode);
            var template = _settingsService.FileNameTemplate;

            foreach (var file in Files)
            {
                if (file.Status != FileStatus.Analyzed && string.IsNullOrWhiteSpace(file.SeriesTitle))
                    continue;

                if (string.IsNullOrWhiteSpace(file.SeriesTitle))
                {
                    file.NewName = "";
                    continue;
                }

                try
                {
                    file.NewName = strategy.GetNewFileName(
                        file.OriginalName,
                        new ChapterInfo
                        {
                            Title = file.SeriesTitle,
                            Season = file.Season,
                            Episode = file.Episode,
                            EpisodeTitle = file.EpisodeTitle,
                            Year = file.Year
                        },
                        template);
                }
                catch
                {
                    file.NewName = "Error";
                }
            }
        }

        [RelayCommand]
        public void ApplySeriesTitleToAll(string? seriesTitle)
        {
            if (string.IsNullOrWhiteSpace(seriesTitle)) return;
            var trimmed = seriesTitle.Trim();
            int count = 0;
            foreach (var file in Files)
            {
                if (!file.IsChecked) continue;
                count++;
                file.SeriesTitle = trimmed;
                if (file.Status == FileStatus.Pending)
                {
                    file.Status = FileStatus.Analyzed;
                }
                UpdatePreviewForFile(file);
            }
            StatusMessage = $"Título \"{trimmed}\" aplicado a {count} archivo(s)";
            NotifyFileMetricsChanged();
        }

        [RelayCommand]
        public void ApplySeasonToAll(int season)
        {
            if (season < 0) return;
            int count = 0;
            foreach (var file in Files)
            {
                if (!file.IsChecked) continue;
                count++;
                file.Season = season;
                UpdatePreviewForFile(file);
            }
            StatusMessage = $"Temporada {season} aplicada a {count} archivo(s)";
        }

        [RelayCommand]
        public void RenumberEpisodesSequentially(int startEpisode = 1)
        {
            int start = Math.Max(1, startEpisode);
            int ep = start;
            foreach (var file in Files)
            {
                if (!file.IsChecked) continue;
                file.Episode = ep++;
                if (file.Status == FileStatus.Pending && !string.IsNullOrWhiteSpace(file.SeriesTitle))
                {
                    file.Status = FileStatus.Analyzed;
                }
                UpdatePreviewForFile(file);
            }
            StatusMessage = $"Episodios renumerados secuencialmente desde {start}";
            NotifyFileMetricsChanged();
        }

        [RelayCommand]
        public void ToggleAllChecked(bool isChecked)
        {
            foreach (var file in Files)
            {
                file.IsChecked = isChecked;
            }
            NotifyFileMetricsChanged();
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
            foreach (var file in Files)
            {
                if (file.IsChecked && string.IsNullOrEmpty(file.SeriesTitle))
                {
                    file.SeriesTitle = seriesName;
                }
            }

            RefreshAllPreviews();

            StatusMessage = $"Metadatos TMDB aplicados. Serie: {seriesName}";
            ShowingTmdbSearch = false;
            NotifyFileMetricsChanged();
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
                var json = JsonSerializer.Serialize(exportData, SessionJsonOptions);
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var exportPath = Path.Combine(desktopPath, $"session_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                await File.WriteAllTextAsync(exportPath, json);
                StatusMessage = $"Sesión exportada a: {exportPath}";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to export session");
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
                var items = JsonSerializer.Deserialize<List<JsonElement>>(json);
                if (items == null) return;

                var imported = new List<FileViewModel>(items.Count);
                var strategy = _strategyFactory.CreateStrategy(CurrentMode);
                var template = _settingsService.FileNameTemplate;

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

                    if (!string.IsNullOrWhiteSpace(file.SeriesTitle))
                    {
                        file.NewName = strategy.GetNewFileName(
                            file.OriginalName,
                            new ChapterInfo
                            {
                                Title = file.SeriesTitle,
                                Season = file.Season,
                                Episode = file.Episode,
                                EpisodeTitle = file.EpisodeTitle,
                                Year = file.Year
                            },
                            template);
                    }

                    imported.Add(file);
                }

                SelectedFile = null;
                Files = new ObservableCollection<FileViewModel>(imported);
                StatusMessage = $"Sesión importada: {Files.Count} archivos";
                NotifyFileMetricsChanged();
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to import session JSON");
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
            _lastProgressTicks = 0;
            UpdateProgress("Moviendo archivos...", 0, filesToMove.Count);

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

                var moveResult = await _organizationWorkflow.MoveBatchAsync(
                    items,
                    destinationFolder,
                    _settingsService.OrganizeIntoSubfolders);

                for (int i = 0; i < filesToMove.Count; i++)
                {
                    var file = filesToMove[i];
                    UpdateProgressThrottled($"Moviendo: {file.OriginalName}", i + 1, filesToMove.Count);

                    if (!moveResult.MovedBySource.TryGetValue(file.FullPath, out var newPath))
                    {
                        continue;
                    }

                    file.FullPath = newPath;
                    file.OriginalName = Path.GetFileName(newPath);
                    file.Status = FileStatus.Done;
                }

                if (moveResult.UndoOperations.Count > 0)
                {
                    _undoRedoService.RecordOperations(moveResult.UndoOperations);
                }

                if (moveResult.LogEntries.Count > 0)
                {
                    _logService.LogBatch(moveResult.LogEntries);
                }

                NotifyFileMetricsChanged();
                StatusMessage = $"Todos los archivos movidos a: {destinationFolder}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to move files to {DestinationFolder}", destinationFolder);
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
            if (SelectedFile == null) return;

            var file = SelectedFile;
            var batchResult = await _organizationWorkflow.RenameBatchAsync(
                new[] { ToRenameBatchItem(file) },
                CurrentMode,
                _settingsService.FileNameTemplate,
                _settingsService.RenameCompanionFiles);

            if (batchResult.ItemResults.Count > 0)
            {
                var r = batchResult.ItemResults[0];
                if (r.Success)
                {
                    file.FullPath = r.NewPath;
                    file.OriginalName = Path.GetFileName(r.NewPath);
                    file.Status = FileStatus.Done;
                    file.NewName = "";
                    file.SeriesTitle = r.AppliedInfo.Title;
                    file.Season = r.AppliedInfo.Season;
                    file.Episode = r.AppliedInfo.Episode;
                    file.Year = r.AppliedInfo.Year;
                }
                else
                {
                    file.Status = FileStatus.Error;
                    StatusMessage = $"Error: {r.ErrorMessage}";
                }
            }

            if (batchResult.UndoOperations.Count > 0)
            {
                _undoRedoService.RecordOperations(batchResult.UndoOperations);
            }

            if (batchResult.LogEntries.Count > 0)
            {
                _logService.LogBatch(batchResult.LogEntries);
            }

            NotifyFileMetricsChanged();
        }

        private void UpdateProgress(string message, int current, int total)
        {
            ProgressMessage = message;
            ProgressCurrent = current;
            ProgressTotal = total;
        }

        private void UpdateProgressThrottled(string message, int current, int total)
        {
            long now = Environment.TickCount64;
            if (current <= 0 || current >= total || now - _lastProgressTicks >= 60)
            {
                _lastProgressTicks = now;
                UpdateProgress(message, current, total);
            }
        }

        private void ApplyFilePathChanges(IEnumerable<RenameOperation> operations, bool isCompletedOperation)
        {
            var filesByPath = new Dictionary<string, FileViewModel>(Files.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var item in Files)
            {
                if (!string.IsNullOrEmpty(item.FullPath))
                {
                    filesByPath[item.FullPath] = item;
                }
            }

            foreach (var operation in operations)
            {
                if (!filesByPath.TryGetValue(operation.OldPath, out var file))
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

            NotifyFileMetricsChanged();
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using organizadorCapitulos.Application.Services;
using organizadorCapitulos.Application.Strategies;
using organizadorCapitulos.Core.Entities;
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

        // Dependencies needed for specific logic
        private string? _maintainSeriesTitle;
        private int _maintainSeason = 1;
        private int _maintainNextEpisode = 1;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredFiles))]
        [NotifyPropertyChangedFor(nameof(FileCounts))]
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

        public bool IsNotProcessing => !IsProcessing;

        // Filter properties
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredFiles))]
        private FileStatus? _currentFilter = null; // null = All

        public IEnumerable<FileViewModel> FilteredFiles => CurrentFilter.HasValue
            ? Files.Where(f => f.Status == CurrentFilter.Value)
            : Files;

        public string FileCounts => $"{Files.Count} total | {Files.Count(f => f.Status == FileStatus.Analyzed)} listos";

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
            IThemeService themeService)
        {
            _fileOrganizerService = fileOrganizerService;
            _aiService = aiService;
            _strategyFactory = strategyFactory;
            _metadataService = metadataService;
            _settingsService = settingsService;
            _undoRedoService = undoRedoService;
            _logService = logService;
            _themeService = themeService;

            // Initialize saved settings
            if (!string.IsNullOrEmpty(_settingsService.TmdbApiKey))
            {
                _metadataService.Configure(_settingsService.TmdbApiKey);
            }
        }

        [RelayCommand]
        public async Task InitializeAsync()
        {
            await _themeService.InitializeAsync();
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

                StatusMessage = $"Carga completada: {Files.Count} archivos de video encontrados";
                OnPropertyChanged(nameof(FileCounts));
                OnPropertyChanged(nameof(FilteredFiles));
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

            IsProcessing = true;
            ProgressTotal = Files.Count;
            ProgressCurrent = 0;

            foreach (var file in Files)
            {
                ProgressCurrent++;
                UpdateProgress($"Analizando: {file.OriginalName}", ProgressCurrent, ProgressTotal);

                try
                {
                    var result = await _aiService.AnalyzeFilenameAsync(file.OriginalName);
                    if (result != null)
                    {
                        file.SeriesTitle = result.Title ?? "";
                        file.Season = result.Season;
                        file.Episode = result.Chapter;
                        file.EpisodeTitle = result.EpisodeTitle ?? "";
                        file.Status = FileStatus.Analyzed;
                        UpdatePreviewForFile(file);
                    }
                    else
                    {
                        file.Status = FileStatus.Error;
                    }
                }
                catch
                {
                    file.Status = FileStatus.Error;
                }
            }

            IsProcessing = false;
            StatusMessage = $"Análisis completado: {Files.Count(f => f.Status == FileStatus.Analyzed)} archivos analizados";
            OnPropertyChanged(nameof(FileCounts));
            OnPropertyChanged(nameof(FilteredFiles));
        }

        [RelayCommand]
        public void SelectFile(FileViewModel file)
        {
            var previousFile = SelectedFile;

            foreach (var f in Files) f.IsSelected = false;
            file.IsSelected = true;
            SelectedFile = file;

            // Maintain mode logic
            if (CurrentMode == RenameMode.Maintain && previousFile != null && !string.IsNullOrEmpty(previousFile.SeriesTitle))
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
            var filesToRename = Files.Where(f => f.Status == FileStatus.Analyzed).ToList();
            if (!filesToRename.Any()) return;

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

            foreach (var file in filesToRename)
            {
                ProgressCurrent++;
                UpdateProgress($"Renombrando: {file.OriginalName}", ProgressCurrent, ProgressTotal);
                await RenameFileAsync(file);
            }

            _maintainSeriesTitle = null;
            IsProcessing = false;
            StatusMessage = $"Renombrado completado: {Files.Count(f => f.Status == FileStatus.Done)} archivos";
            OnPropertyChanged(nameof(FileCounts));
            OnPropertyChanged(nameof(FilteredFiles));
        }

        public async Task RenameFileAsync(FileViewModel file)
        {
            try
            {
                var oldPath = file.FullPath;

                var chapterInfo = new ChapterInfo
                {
                    Title = file.SeriesTitle,
                    Season = file.Season,
                    Chapter = file.Episode,
                    EpisodeTitle = file.EpisodeTitle
                };

                if (CurrentMode == RenameMode.Maintain && _maintainSeriesTitle != null)
                {
                    chapterInfo.Title = _maintainSeriesTitle;
                    chapterInfo.Season = _maintainSeason;
                    chapterInfo.Chapter = _maintainNextEpisode;
                    _maintainNextEpisode++;
                }

                var strategy = _strategyFactory.CreateStrategy(CurrentMode);
                var newPath = await _fileOrganizerService.RenameFileAsync(oldPath, chapterInfo, strategy);

                _undoRedoService.RecordOperation(oldPath, newPath);

                file.FullPath = newPath;
                file.OriginalName = Path.GetFileName(newPath);
                file.Status = FileStatus.Done;
                file.NewName = "";

                // Update displayed metadata
                file.SeriesTitle = chapterInfo.Title;
                file.Season = chapterInfo.Season;
                file.Episode = chapterInfo.Chapter;
            }
            catch (Exception ex)
            {
                file.Status = FileStatus.Error;
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task UndoAsync()
        {
            IsProcessing = true;
            StatusMessage = "Deshaciendo...";

            var result = await _undoRedoService.UndoAsync();
            StatusMessage = result.message;

            if (result.success && result.count > 0)
            {
                // Simple refresh for now - could be optimized
                // Ideally we'd map changes back to FileViewModels
            }

            IsProcessing = false;
        }

        [RelayCommand]
        private async Task RedoAsync()
        {
            IsProcessing = true;
            StatusMessage = "Rehaciendo...";

            var result = await _undoRedoService.RedoAsync();
            StatusMessage = result.message;
            IsProcessing = false;
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
                    Chapter = file.Episode,
                    EpisodeTitle = file.EpisodeTitle
                };

                var extension = Path.GetExtension(file.OriginalName);
                file.NewName = chapterInfo.GenerateFileName(extension);
            }
            catch
            {
                file.NewName = "Error";
            }
        }

        [RelayCommand]
        public void ChangeMode(RenameMode mode)
        {
            CurrentMode = mode;
            foreach (var file in Files.Where(f => f.Status == FileStatus.Analyzed))
            {
                UpdatePreviewForFile(file);
            }
        }

        [RelayCommand]
        public void ApplySeriesMetadata(string seriesName)
        {
            foreach (var file in Files.Where(f => string.IsNullOrEmpty(f.SeriesTitle)))
            {
                file.SeriesTitle = seriesName;
            }

            foreach (var file in Files.Where(f => f.Status == FileStatus.Analyzed))
            {
                UpdatePreviewForFile(file);
            }

            StatusMessage = $"Metadatos TMDB aplicados. Serie: {seriesName}";
            ShowingTmdbSearch = false;
        }

        // Dialog Commands
        [RelayCommand] private void OpenSettings() => ShowingSettings = true;
        [RelayCommand] private void CloseSettings() => ShowingSettings = false;
        [RelayCommand] private void OpenTmdbSearch() => ShowingTmdbSearch = true;
        [RelayCommand] private void CloseTmdbSearch() => ShowingTmdbSearch = false;
        [RelayCommand] private void OpenSaveAll() => ShowingSaveAll = true;
        [RelayCommand] private void CloseSaveAll() => ShowingSaveAll = false;

        [RelayCommand]
        private async Task MoveFilesAsync(List<string> folders)
        {
            ShowingSaveAll = false;
            if (!folders.Any()) return;

            string destinationFolder = folders[0];
            IsProcessing = true;
            ProgressTotal = Files.Count;
            ProgressCurrent = 0;

            try
            {
                foreach (var file in Files)
                {
                    ProgressCurrent++;
                    UpdateProgress($"Moviendo: {file.OriginalName}", ProgressCurrent, ProgressTotal);

                    var newPath = Path.Combine(destinationFolder, file.OriginalName);

                    // Run on background to not block UI
                    await Task.Run(() => File.Move(file.FullPath, newPath, true));

                    file.FullPath = newPath;
                    file.Status = FileStatus.Done;
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
                await RenameFileAsync(SelectedFile);
            }
        }

        private void UpdateProgress(string message, int current, int total)
        {
            ProgressMessage = message;
            ProgressCurrent = current;
            ProgressTotal = total;
        }
    }
}

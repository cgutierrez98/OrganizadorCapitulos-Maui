using CommunityToolkit.Mvvm.ComponentModel;
using organizadorCapitulos.Core.Enums;

namespace OrganizadorCapitulos.Maui.Models
{
    public partial class FileViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _originalName = "";

        [ObservableProperty]
        private string _fullPath = "";

        [ObservableProperty]
        private string _newName = "";

        [ObservableProperty]
        private FileStatus _status = FileStatus.Pending;

        // Metadata
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMetadata))]
        private string _seriesTitle = "";

        [ObservableProperty]
        private int _season = 1;

        [ObservableProperty]
        private int _episode = 1;

        [ObservableProperty]
        private string _episodeTitle = "";

        [ObservableProperty]
        private bool _isSelected;

        public bool HasMetadata => !string.IsNullOrEmpty(SeriesTitle);
    }
}

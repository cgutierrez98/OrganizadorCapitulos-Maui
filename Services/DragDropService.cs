using System;
using System.Collections.Generic;
using OrganizadorCapitulos.Maui.Services.Interfaces;

namespace OrganizadorCapitulos.Maui.Services
{
    public class DragDropService : IDragDropService
    {
        public event Action<IReadOnlyList<string>>? FoldersDropped;
        public event Action<string>? SessionFileDropped;

        public void NotifyFoldersDropped(IReadOnlyList<string> folderPaths)
        {
            FoldersDropped?.Invoke(folderPaths);
        }

        public void NotifySessionFileDropped(string jsonFilePath)
        {
            SessionFileDropped?.Invoke(jsonFilePath);
        }
    }
}

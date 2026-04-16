using System;
using System.Collections.Generic;
using OrganizadorCapitulos.Maui.Services.Interfaces;

namespace OrganizadorCapitulos.Maui.Services
{
    public class DragDropService : IDragDropService
    {
        public event Action<IReadOnlyList<string>>? FoldersDropped;

        public void NotifyFoldersDropped(IReadOnlyList<string> folderPaths)
        {
            FoldersDropped?.Invoke(folderPaths);
        }
    }
}

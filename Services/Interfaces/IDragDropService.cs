using System;
using System.Collections.Generic;

namespace OrganizadorCapitulos.Maui.Services.Interfaces
{
    public interface IDragDropService
    {
        /// <summary>Fired on the UI thread when the user drops folders onto the window.</summary>
        event Action<IReadOnlyList<string>>? FoldersDropped;

        void NotifyFoldersDropped(IReadOnlyList<string> folderPaths);
    }
}

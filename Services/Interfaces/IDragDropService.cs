using System;
using System.Collections.Generic;

namespace OrganizadorCapitulos.Maui.Services.Interfaces
{
    public interface IDragDropService
    {
        /// <summary>Fired on the UI thread when the user drops folders onto the window.</summary>
        event Action<IReadOnlyList<string>>? FoldersDropped;

        /// <summary>Fired when the user drops an exported .json session file onto the window.</summary>
        event Action<string>? SessionFileDropped { add { } remove { } }

        void NotifyFoldersDropped(IReadOnlyList<string> folderPaths);
        void NotifySessionFileDropped(string jsonFilePath) { }
    }
}

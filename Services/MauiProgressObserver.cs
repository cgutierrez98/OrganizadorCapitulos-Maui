using System;
using organizadorCapitulos.Core.Interfaces.Observers;

namespace OrganizadorCapitulos.Maui.Services
{
    public class MauiProgressObserver : IProgressObserver
    {
        public event Action<int, int, string>? OnProgressChanged;
        public event Action<string>? OnStatusChanged;

        public void UpdateProgress(int current, int total, string filename)
        {
            OnProgressChanged?.Invoke(current, total, filename);
        }

        public void UpdateStatus(string status)
        {
            OnStatusChanged?.Invoke(status);
        }
    }
}

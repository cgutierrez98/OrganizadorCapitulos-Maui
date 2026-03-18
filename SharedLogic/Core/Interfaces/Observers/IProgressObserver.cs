namespace organizadorCapitulos.Core.Interfaces.Observers
{
    public interface IProgressObserver
    {
        void UpdateProgress(int current, int total, string filename);
        void UpdateStatus(string status);
    }
}

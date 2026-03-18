using System.Collections.Generic;
using organizadorCapitulos.Core.Interfaces.Observers;

namespace organizadorCapitulos.Application.Services
{
    public class ProgressNotifier
    {
        private readonly List<IProgressObserver> _observers = new List<IProgressObserver>();

        public void Subscribe(IProgressObserver observer) => _observers.Add(observer);
        public void Unsubscribe(IProgressObserver observer) => _observers.Remove(observer);

        public void NotifyProgress(int current, int total, string filename)
        {
            foreach (var observer in _observers)
            {
                observer.UpdateProgress(current, total, filename);
            }
        }

        public void NotifyStatus(string status)
        {
            foreach (var observer in _observers)
            {
                observer.UpdateStatus(status);
            }
        }
    }
}

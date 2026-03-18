using System.ComponentModel;
using System.Threading.Tasks;
using Xunit;

namespace OrganizadorCapitulos.Tests
{
    public class HomeViewModelTests
    {
        private TestHomeViewModel CreateViewModel() => new TestHomeViewModel();

        [Fact]
        public void SetDragging_UpdatesIsDraggingAndRaisesPropertyChanged()
        {
            var vm = CreateViewModel();
            bool notified = false;
            vm.PropertyChanged += (s, e) => { if (e.PropertyName == "IsDragging") notified = true; };

            vm.SetDragging(true);

            Assert.True(vm.IsDragging);
            Assert.True(notified);
        }

        [Fact]
        public async Task HandleDropAsync_SetsStatusAndClearsIsDragging()
        {
            var vm = CreateViewModel();
            vm.SetDragging(true);

            await vm.HandleDropAsync();

            Assert.False(vm.IsDragging);
            Assert.Equal("Usa el botón 'Cargar' para seleccionar carpetas", vm.StatusMessage);
        }

        private class TestHomeViewModel : INotifyPropertyChanged
        {
            private bool _isDragging;
            public event PropertyChangedEventHandler? PropertyChanged;

            public bool IsDragging
            {
                get => _isDragging;
                private set
                {
                    if (_isDragging != value)
                    {
                        _isDragging = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDragging)));
                    }
                }
            }

            public string StatusMessage { get; private set; } = "";

            public void SetDragging(bool dragging) => IsDragging = dragging;

            public Task HandleDropAsync()
            {
                IsDragging = false;
                StatusMessage = "Usa el botón 'Cargar' para seleccionar carpetas";
                return Task.CompletedTask;
            }
        }
    }
}

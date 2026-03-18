using Microsoft.JSInterop;
using OrganizadorCapitulos.Maui.Services.Interfaces;

namespace OrganizadorCapitulos.Maui.Services
{
    public class ThemeService : IThemeService
    {
        private readonly IJSRuntime _jsRuntime;
        private bool _isDarkTheme;

        public ThemeService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public bool IsDarkTheme => _isDarkTheme;

        public async Task InitializeAsync()
        {
            try
            {
                var savedTheme = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "theme");
                if (savedTheme == "dark")
                {
                    _isDarkTheme = true;
                    await ApplyThemeAsync();
                }
            }
            catch
            {
                // Ignore errors during initialization (e.g. prerendering)
            }
        }

        public async Task ToggleThemeAsync()
        {
            _isDarkTheme = !_isDarkTheme;
            await ApplyThemeAsync();
        }

        private async Task ApplyThemeAsync()
        {
            try
            {
                var theme = _isDarkTheme ? "dark" : "light";
                await _jsRuntime.InvokeVoidAsync("eval", $"document.documentElement.setAttribute('data-theme', '{theme}')");
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "theme", theme);
            }
            catch
            {
                // Handle potential JS interop errors
            }
        }
    }
}

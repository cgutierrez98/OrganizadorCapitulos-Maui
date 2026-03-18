using Microsoft.JSInterop;
using OrganizadorCapitulos.Maui.Services.Interfaces;

namespace OrganizadorCapitulos.Maui.Services
{
    public class ThemeService : IThemeService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly SettingsService _settingsService;
        private bool _isDarkTheme;

        public ThemeService(IJSRuntime jsRuntime, SettingsService settingsService)
        {
            _jsRuntime = jsRuntime;
            _settingsService = settingsService;
        }

        public bool IsDarkTheme => _isDarkTheme;

        public async Task InitializeAsync()
        {
            try
            {
                // Prefer persisted app settings (cross-platform)
                var saved = _settingsService.Theme;
                if (!string.IsNullOrEmpty(saved))
                {
                    if (saved == "dark")
                    {
                        _isDarkTheme = true;
                        await ApplyThemeAsync();
                    }
                    return;
                }

                // Fallback to browser localStorage when running as Blazor
                var savedTheme = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "theme");
                if (savedTheme == "dark")
                {
                    _isDarkTheme = true;
                    await ApplyThemeAsync();
                }
            }
            catch
            {
                // Ignore errors during initialization (e.g. prerendering or missing JS runtime)
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
                // Apply theme in-browser when JS runtime is available
                try
                {
                    await _jsRuntime.InvokeVoidAsync("eval", $"document.documentElement.setAttribute('data-theme', '{theme}')");
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "theme", theme);
                }
                catch
                {
                    // Ignore JS interop errors (non-web platforms)
                }

                // Persist theme cross-platform via SettingsService
                _settingsService.Theme = theme;
            }
            catch
            {
                // Swallow any unexpected errors
            }
        }
    }
}

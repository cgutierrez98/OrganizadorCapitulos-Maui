using System;
using Microsoft.JSInterop;
using OrganizadorCapitulos.Maui.Services.Interfaces;

namespace OrganizadorCapitulos.Maui.Services
{
    public class ThemeService : IThemeService
    {
        private IJSRuntime? _jsRuntime;
        private readonly SettingsService _settingsService;
        private bool _isDarkTheme;

        public event Action<bool>? ThemeChanged;

        public ThemeService(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public void SetJsRuntime(object jsRuntime)
        {
            _jsRuntime = jsRuntime as IJSRuntime;
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
                    _isDarkTheme = saved == "dark";
                    await ApplyThemeAsync();
                    return;
                }

                // Fallback to browser localStorage when running as Blazor
                try
                {
                    if (_jsRuntime != null)
                    {
                        var savedTheme = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "theme");
                        if (savedTheme == "dark")
                        {
                            _isDarkTheme = true;
                            await ApplyThemeAsync();
                        }
                    }
                }
                catch
                {
                    // Ignore JS interop errors (non-web platforms)
                }
            }
            catch
            {
                // Ignore errors during initialization
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
                    // Prefer a small JS helper instead of eval for safety and reliability
                    if (_jsRuntime != null)
                    {
                        // Emit a debug message first to help diagnose whether JS interop is reachable
                        try { await _jsRuntime.InvokeVoidAsync("console.debug", "ThemeService.setDataTheme", theme); } catch { }
                        await _jsRuntime.InvokeVoidAsync("setDataTheme", theme);
                    }
                }
                catch
                {
                    // Ignore JS interop errors (non-web platforms)
                }

                // Persist theme cross-platform via SettingsService
                _settingsService.Theme = theme;

                // Notify subscribers about the change
                ThemeChanged?.Invoke(_isDarkTheme);
            }
            catch
            {
                // Swallow any unexpected errors
            }
        }
    }
}

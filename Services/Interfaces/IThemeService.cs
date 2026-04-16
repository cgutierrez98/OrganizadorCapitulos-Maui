using System;

namespace OrganizadorCapitulos.Maui.Services.Interfaces
{
    public interface IThemeService
    {
        Task InitializeAsync();
        Task ToggleThemeAsync();
        bool IsDarkTheme { get; }

        // Raised when the theme changes. Argument is true when dark theme is active.
        event Action<bool>? ThemeChanged;

        // Provide the JS runtime when available (components should call this)
        // Use object to avoid forcing test projects to reference Microsoft.JSInterop.
        void SetJsRuntime(object jsRuntime);
    }
}

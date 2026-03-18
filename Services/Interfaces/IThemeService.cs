namespace OrganizadorCapitulos.Maui.Services.Interfaces
{
    public interface IThemeService
    {
        Task InitializeAsync();
        Task ToggleThemeAsync();
        bool IsDarkTheme { get; }
    }
}

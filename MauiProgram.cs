using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using organizadorCapitulos.Application.Services;
using organizadorCapitulos.Application.Strategies;
using organizadorCapitulos.Core.Interfaces.Observers;
using organizadorCapitulos.Core.Interfaces.Repositories;
using organizadorCapitulos.Core.Interfaces.Services;
using organizadorCapitulos.Infrastructure.Repositories;
using organizadorCapitulos.Infrastructure.Services;
using OrganizadorCapitulos.Maui.Services;
using OrganizadorCapitulos.Maui.Services.Interfaces;
using OrganizadorCapitulos.Maui.ViewModels;

namespace OrganizadorCapitulos.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

        // Register Services
        builder.Services.AddSingleton<SettingsService>();
        builder.Services.AddSingleton<UndoRedoService>();
        builder.Services.AddSingleton<OperationLogService>();
        builder.Services.AddSingleton<IFileRepository, FileRepository>();
        builder.Services.AddSingleton<MauiProgressObserver>();
        builder.Services.AddSingleton<IProgressObserver>(sp => sp.GetRequiredService<MauiProgressObserver>());

        builder.Services.AddTransient<FileOrganizerService>();
        builder.Services.AddTransient<RenameStrategyFactory>();
        builder.Services.AddTransient<IMetadataService, TmdbMetadataService>();
        builder.Services.AddTransient<IAIService, PythonAIService>();
        builder.Services.AddScoped<IThemeService, ThemeService>();
        builder.Services.AddTransient<HomeViewModel>();

        return builder.Build();
    }
}

using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using Microsoft.Maui.LifecycleEvents;
using organizadorCapitulos.Application.Services;
using organizadorCapitulos.Application.Strategies;
using organizadorCapitulos.Core.Interfaces.Observers;
using organizadorCapitulos.Core.Interfaces.Repositories;
using organizadorCapitulos.Core.Interfaces.Services;
using organizadorCapitulos.Infrastructure.Repositories;
using organizadorCapitulos.Infrastructure.Services;
using OrganizadorCapitulos.Maui.Services;
using OrganizadorCapitulos.Maui.Services.Interfaces;
using OrganizadorCapitulos.Maui.Services.Workflows;
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
        builder.Services.AddSingleton<IFileRepository, FileRepository>();
        builder.Services.AddSingleton<UndoRedoService>();
        builder.Services.AddSingleton<OperationLogService>();
        builder.Services.AddSingleton<MauiProgressObserver>();
        builder.Services.AddSingleton<IProgressObserver>(sp => sp.GetRequiredService<MauiProgressObserver>());
        builder.Services.AddSingleton<IDragDropService, DragDropService>();

        builder.Services.AddTransient<FileOrganizerService>();
        builder.Services.AddTransient<RenameStrategyFactory>();
        builder.Services.AddTransient<FileAnalysisWorkflow>();
        builder.Services.AddTransient<FileOrganizationWorkflow>();
        builder.Services.AddTransient<TmdbMatchingWorkflow>();
        builder.Services.AddSingleton<IMetadataService, TmdbMetadataService>(_ =>
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Brotli
            };
            var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
            return new TmdbMetadataService(httpClient);
        });
        builder.Services.AddSingleton<IAIService, NativeHeuristicAIService>();
        // ThemeService must be a singleton so ViewModels (singletons) observe the same instance
        builder.Services.AddSingleton<IThemeService, ThemeService>();

        // Singleton HomeViewModel: one instance per app session
        builder.Services.AddSingleton<HomeViewModel>();

        return builder.Build();
    }
}


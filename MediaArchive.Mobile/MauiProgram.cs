using System.Net.Http;
using System.Net.Http.Headers;
using MediaArchive.Data;
using MediaArchive.Services.Import;
using MediaArchive.Services.Infrastructure;
using MediaArchive.Services.Logging;
using MediaArchive.Services.Native;
using MediaArchive.Services.Providers;
using MediaArchive.Services.Queries;
using MediaArchive.Services.UserItems;
using Foundation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Maui.LifecycleEvents;

namespace MediaArchive.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.ConfigureLifecycleEvents(events =>
            events.AddiOS(ios => ios.OpenUrl((_, url, _) =>
            {
                if (url.Scheme != "mediaarchive" || TryMapDeepLink(url) is not { } route)
                    return false;

                IPlatformApplication.Current?.Services
                    .GetService<DeepLinkService>()?.Dispatch(route);
                return true;
            })));

        // MAUI doesn't auto-load appsettings.json, so it is read out of the app package.
        using (var configStream = FileSystem.OpenAppPackageFileAsync("appsettings.json").GetAwaiter().GetResult())
            builder.Configuration.AddJsonStream(configStream);

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "mediaarchive.db");
        builder.Services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        builder.Services.AddHttpClient<OpenLibraryProvider>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(OpenLibraryProvider.UserAgent);
        });
        builder.Services.AddTransient<IMediaProvider>(sp => sp.GetRequiredService<OpenLibraryProvider>());

        builder.Services.Configure<TmdbOptions>(
            builder.Configuration.GetSection(TmdbOptions.SectionName));
        builder.Services.AddHttpClient<TmdbProvider>((sp, client) =>
        {
            var tmdb = sp.GetRequiredService<IOptions<TmdbOptions>>().Value;

            client.BaseAddress = new Uri("https://api.themoviedb.org/3/");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", tmdb.ReadAccessToken);
        });
        builder.Services.AddTransient<IMediaProvider>(sp => sp.GetRequiredService<TmdbProvider>());

        builder.Services.Configure<IgdbOptions>(
            builder.Configuration.GetSection(IgdbOptions.SectionName));
        // Singleton: it caches the Twitch access token shared by every IGDB request.
        builder.Services.AddSingleton<IgdbAuthenticator>();
        builder.Services.AddHttpClient<IgdbProvider>((sp, client) =>
        {
            var igdb = sp.GetRequiredService<IOptions<IgdbOptions>>().Value;

            client.BaseAddress = new Uri("https://api.igdb.com/v4/");
            client.DefaultRequestHeaders.Add("Client-ID", igdb.ClientId);
        });
        builder.Services.AddTransient<IMediaProvider>(sp => sp.GetRequiredService<IgdbProvider>());

        // 30s because OpenLibrary covers redirect through slow archive.org mirrors.
        builder.Services.AddHttpClient("covers", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(OpenLibraryProvider.UserAgent);
        });
        builder.Services.AddSingleton<ICoverCache>(sp => new CoverCacheService(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("covers"),
            Path.Combine(FileSystem.AppDataDirectory, "covers"),
            sp.GetRequiredService<ILogger<CoverCacheService>>()));

        builder.Services.AddScoped<MediaSearchService>();
        builder.Services.AddScoped<MediaImportService>();
        builder.Services.AddScoped<LoggingService>();
        builder.Services.AddScoped<UserItemService>();
        builder.Services.AddScoped<CommonQueries>();
        builder.Services.AddScoped<HomeQueries>();
        builder.Services.AddScoped<LibraryQueries>();
        builder.Services.AddScoped<ProfileQueries>();
        builder.Services.AddScoped<ActivityQueries>();

        builder.Services.AddSingleton<DeepLinkService>();

        builder.Services.AddSingleton(sp => new NativeApi(
            sp.GetRequiredService<IServiceScopeFactory>(),
            Path.Combine(FileSystem.AppDataDirectory, "covers")));
        builder.Services.AddSingleton<NativeBackend>();
        builder.Services.AddTransient<MainPage>();
        // Singletons, unlike the queries above: App holds the publisher outside any scope.
        builder.Services.AddSingleton<WidgetQueries>();
        builder.Services.AddSingleton<WidgetSnapshotPublisher>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        using (var db = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext())
            db.Database.Migrate();

        _ = app.Services.GetRequiredService<MediaImportService>().BackfillUncachedCoversAsync();

        return app;
    }

    // The open pass is resolved on the page: an entry id in a stale widget snapshot may already be closed.
    private static string? TryMapDeepLink(NSUrl url) =>
        url.Host == "log" && int.TryParse(url.Path?.TrimStart('/'), out var id)
            ? $"/item/{id}?log=true"
            : null;
}

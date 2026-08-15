using System.Net.Http;
using System.Net.Http.Headers;
using MediaArchive.Data;
using MediaArchive.Services;
using MediaArchive.Services.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediaArchive.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        // appsettings.json ships as a MauiAsset (not auto-loaded like on the web),
        // so read it out of the app package and feed it to configuration.
        using (var configStream = FileSystem.OpenAppPackageFileAsync("appsettings.json").GetAwaiter().GetResult())
            builder.Configuration.AddJsonStream(configStream);

        builder.Services.AddMauiBlazorWebView();

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

        // 30s: cover caching runs in the background and OpenLibrary redirects
        // through slow archive.org mirrors.
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
        builder.Services.AddScoped<ConsumptionService>();
        builder.Services.AddScoped<CollectionService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        using (var db = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext())
            db.Database.Migrate();

        _ = app.Services.GetRequiredService<MediaImportService>().BackfillUncachedCoversAsync();

        return app;
    }
}

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

        // On device the DB lives in the app's private data dir, not the CWD.
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "mediaarchive.db");
        builder.Services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Media providers: bind config, give each provider its own typed HttpClient,
        // then expose it via IMediaProvider so consumers can inject them as a set.
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

        // Phase 1: render provider image URLs directly (see NullCoverCache).
        builder.Services.AddSingleton<ICoverCache, NullCoverCache>();

        builder.Services.AddScoped<MediaSearchService>();
        builder.Services.AddScoped<MediaImportService>();
        builder.Services.AddScoped<ConsumptionService>();
        builder.Services.AddScoped<CollectionService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        // Create + migrate the on-device database on launch (seed data rides along
        // via the migrations' HasData).
        using (var db = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext())
            db.Database.Migrate();

        return app;
    }
}

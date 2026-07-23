using System.Net.Http.Headers;
using MediaArchive.Components;
using MediaArchive.Data;
using MediaArchive.Services;
using MediaArchive.Services.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? "Data Source=mediaarchive.db";
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// Media providers: bind config, give each provider its own typed HttpClient,
// then expose it via IMediaProvider so consumers can inject them as a set.
builder.Services.AddHttpClient<OpenLibraryProvider>(client =>
{
    // Open Library takes no API key but rate-limits anonymous clients hard.
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

builder.Services.AddScoped<MediaSearchService>();
builder.Services.AddScoped<MediaLogService>();
builder.Services.AddScoped<MediaLibraryService>();


var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
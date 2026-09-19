using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MediaArchive.Services.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace MediaArchive.Services.Native;

// The one door the Swift UI knocks on: a route name and an args payload in,
// JSON out. Each call gets its own DI scope, as a request on a server would.
public sealed class NativeApi(IServiceScopeFactory scopes, string coversRoot)
{
    private readonly JsonSerializerOptions _json = CreateOptions(coversRoot);

    public async Task<(string? Json, string? Error)> CallAsync(string route, string argsJson)
    {
        try
        {
            var r = NativeRoutes.Find(route)
                    ?? throw new InvalidOperationException($"No route named '{route}'.");

            var args = r.Args is null ? null : JsonSerializer.Deserialize(argsJson, r.Args, _json);

            await using var scope = scopes.CreateAsyncScope();
            var result = await r.Handle(scope.ServiceProvider, args);

            return (JsonSerializer.Serialize(result, r.Result ?? typeof(object), _json), null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    private static JsonSerializerOptions CreateOptions(string coversRoot)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { info => ResolveCoverUrls(info, coversRoot) }
            }
        };
        return options;
    }

    // LocalImagePath holds the old WebView pseudo-URL (covers://c/<file>);
    // the native side wants a file URL it can load directly.
    private static void ResolveCoverUrls(JsonTypeInfo info, string coversRoot)
    {
        foreach (var property in info.Properties)
        {
            if (property.Name != "imageUrl" || property.PropertyType != typeof(string) || property.Get is null)
                continue;

            var read = property.Get;
            property.Get = target => read(target) is string url && url.StartsWith(CoverCacheService.UrlBase + "/")
                ? new Uri(Path.Combine(coversRoot, url[(CoverCacheService.UrlBase.Length + 1)..])).AbsoluteUri
                : read(target);
        }
    }
}

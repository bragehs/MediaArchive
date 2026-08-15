using Foundation;
using WebKit;

namespace MediaArchive.Mobile;

// Serves runtime-cached covers (outside the bundled wwwroot) to the WebView.
public sealed class CoversSchemeHandler : NSObject, IWKUrlSchemeHandler
{
    public const string Scheme = "covers";

    private readonly string _root = Path.Combine(FileSystem.AppDataDirectory, "covers");

    public void StartUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
    {
        var url = urlSchemeTask.Request.Url;

        try
        {
            var fileName = Path.GetFileName(url?.Path ?? string.Empty);
            var path = Path.Combine(_root, fileName);

            if (string.IsNullOrEmpty(fileName) || !File.Exists(path))
            {
                urlSchemeTask.DidReceiveResponse(new NSHttpUrlResponse(url!, 404, "HTTP/1.1", null));
                urlSchemeTask.DidFinish();
                return;
            }

            var data = NSData.FromFile(path);
            var headers = new NSDictionary(
                "Content-Type", MimeFor(Path.GetExtension(fileName)),
                "Access-Control-Allow-Origin", "*");

            urlSchemeTask.DidReceiveResponse(new NSHttpUrlResponse(url!, 200, "HTTP/1.1", headers));
            urlSchemeTask.DidReceiveData(data);
            urlSchemeTask.DidFinish();
        }
        catch (Exception)
        {
            urlSchemeTask.DidFailWithError(new NSError(new NSString(Scheme), 1));
        }
    }

    public void StopUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask) { }

    private static string MimeFor(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "image/jpeg"
    };
}

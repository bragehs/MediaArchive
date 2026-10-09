using System.Runtime.InteropServices;
using System.Text.Json;
using CoreGraphics;
using Foundation;
using MediaArchive.Services.Queries;
using Microsoft.Extensions.Logging;
using ObjCRuntime;
using UIKit;

namespace MediaArchive.Mobile;

public sealed class WidgetSnapshotPublisher(
    WidgetQueries queries,
    ILogger<WidgetSnapshotPublisher> logger)
{
    public const string AppGroupId = "group.no.norapps.mediaarchive";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync()
    {
        try
        {
            var container = NSFileManager.DefaultManager.GetContainerUrl(AppGroupId)?.Path;
            if (container is null)
            {
                logger.LogWarning("App Group container unavailable — widget snapshot skipped");
                return;
            }

            var dir = Path.Combine(container, "widget");
            var coversDir = Path.Combine(dir, "covers");
            Directory.CreateDirectory(coversDir);

            var items = await queries.GetInProgressAsync();

            foreach (var item in items.Take(4))
                if (item.Cover is { } cover)
                    CopyCoverDownscaled(cover, coversDir);

            var json = JsonSerializer.Serialize(new { items }, JsonOptions);
            await File.WriteAllTextAsync(Path.Combine(dir, "snapshot.json"), json);

            ReloadWidgetTimelines();
        }
        catch (Exception ex)
        {
            // The widget is a convenience surface — it must never take the app down.
            logger.LogError(ex, "Widget snapshot failed");
        }
    }

    // WidgetKit has no .NET binding, so the reload goes through WidgetLink.framework via the ObjC runtime.
    [DllImport(Constants.ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void ObjcMsgSendVoid(IntPtr receiver, IntPtr selector);

    private static void ReloadWidgetTimelines()
    {
        IntPtr cls = Class.GetHandle("MAWidgetLink");
        if (cls != IntPtr.Zero)
            ObjcMsgSendVoid(cls, Selector.GetHandle("reloadAll"));
    }

    // Downscaled for the widget's tight memory budget, and rewritten every publish so a resize leaves no stale copy.
    private static void CopyCoverDownscaled(string fileName, string coversDir)
    {
        var source = Path.Combine(FileSystem.AppDataDirectory, "covers", fileName);
        var dest = Path.Combine(coversDir, fileName);
        if (!File.Exists(source))
            return;

        using var image = UIImage.FromFile(source);
        if (image is null)
            return;

        const double targetHeight = 252;
        var scale = targetHeight / image.Size.Height;
        if (scale >= 1)
        {
            File.Copy(source, dest, overwrite: true);
            return;
        }

        var size = new CGSize(image.Size.Width * scale, targetHeight);
        var format = UIGraphicsImageRendererFormat.DefaultFormat;
        format.Scale = 1;
        using var renderer = new UIGraphicsImageRenderer(size, format);
        using var scaled = renderer.CreateImage(_ => image.Draw(new CGRect(CGPoint.Empty, size)));
        using var jpeg = scaled.AsJPEG(0.8f);
        if (jpeg is not null)
            File.WriteAllBytes(dest, jpeg.ToArray());
    }
}

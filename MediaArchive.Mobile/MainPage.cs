using MediaArchive.Services.Infrastructure;
using UIKit;

namespace MediaArchive.Mobile;

// Hosted as a child view controller so sheets and safe areas run through UIKit's normal parent chain.
public sealed class MainPage : ContentPage
{
    private readonly NativeBackend _backend;
    private readonly DeepLinkService _deepLinks;
    private bool _embedded;

    public MainPage(NativeBackend backend, DeepLinkService deepLinks)
    {
        _backend = backend;
        _deepLinks = deepLinks;
        BackgroundColor = Color.FromArgb("#141414");
        Loaded += (_, _) => Embed();
    }

    private void Embed()
    {
        if (_embedded || (Handler as IPlatformViewHandler)?.ViewController is not { View: not null } host)
            return;

        _embedded = true;

        var root = NativeHost.MakeRoot(_backend);
        host.AddChildViewController(root);
        root.View!.Frame = host.View.Bounds;
        root.View.AutoresizingMask = UIViewAutoresizing.FlexibleDimensions;
        host.View.AddSubview(root.View);
        root.DidMoveToParentViewController(host);

        _deepLinks.Subscribe(NativeHost.Open);
    }
}

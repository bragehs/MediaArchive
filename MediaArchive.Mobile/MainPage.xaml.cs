namespace MediaArchive.Mobile;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();

#if IOS
        // Must be attached before the view initializes.
        blazorWebView.BlazorWebViewInitializing += (_, e) =>
            e.Configuration?.SetUrlSchemeHandler(new CoversSchemeHandler(), CoversSchemeHandler.Scheme);

        // Edge-swipe back. Blazor routes through pushState, so the web view's
        // history is the navigation stack — WebKit's own gesture drives it and
        // stays edge-constrained, which keeps it clear of the Library canvas pan
        // and the horizontal cover rails.
        blazorWebView.BlazorWebViewInitialized += (_, e) =>
            e.WebView.AllowsBackForwardNavigationGestures = true;
#endif
    }
}

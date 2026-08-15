namespace MediaArchive.Mobile;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();

#if IOS
        // Teach the WKWebView to resolve covers://c/{file} to the cached cover
        // files. Must be attached to the configuration before the view initializes.
        blazorWebView.BlazorWebViewInitializing += (_, e) =>
            e.Configuration?.SetUrlSchemeHandler(new CoversSchemeHandler(), CoversSchemeHandler.Scheme);
#endif
    }
}

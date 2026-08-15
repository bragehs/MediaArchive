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
#endif
    }
}

namespace MediaArchive.Mobile;

public sealed class App(MainPage mainPage, WidgetSnapshotPublisher widgetPublisher) : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(mainPage) { Title = "MediaArchive" };

        // Refresh the widget when the app opens (changes from outside a session)
        // and when it backgrounds (whatever was just logged). PublishAsync guards
        // itself, so fire-and-forget is safe here.
        window.Created += (_, _) => _ = widgetPublisher.PublishAsync();
        window.Stopped += (_, _) => _ = widgetPublisher.PublishAsync();

        return window;
    }
}

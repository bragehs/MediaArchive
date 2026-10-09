namespace MediaArchive.Mobile;

public sealed class App(MainPage mainPage, WidgetSnapshotPublisher widgetPublisher) : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(mainPage) { Title = "MediaArchive" };

        // Fire-and-forget is safe: PublishAsync catches its own failures.
        window.Created += (_, _) => _ = widgetPublisher.PublishAsync();
        window.Stopped += (_, _) => _ = widgetPublisher.PublishAsync();

        return window;
    }
}

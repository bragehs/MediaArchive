namespace MediaArchive.Mobile;

public partial class App : Application
{
	private readonly WidgetSnapshotPublisher _widgetPublisher;

	public App(WidgetSnapshotPublisher widgetPublisher)
	{
		InitializeComponent();
		_widgetPublisher = widgetPublisher;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new MainPage()) { Title = "MediaArchive.Mobile" };

		// Refresh the widget when the app opens (changes from outside a session)
		// and when it backgrounds (whatever was just logged). PublishAsync guards
		// itself, so fire-and-forget is safe here.
		window.Created += (_, _) => _ = _widgetPublisher.PublishAsync();
		window.Stopped += (_, _) => _ = _widgetPublisher.PublishAsync();

		return window;
	}
}

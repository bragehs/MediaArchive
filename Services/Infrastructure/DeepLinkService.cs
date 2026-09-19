namespace MediaArchive.Services.Infrastructure;

// Holds native deep links (widget taps) for the UI. iOS can hand one over
// before the Swift root exists, so a route that arrives with no subscriber
// is held until the host claims it.
public sealed class DeepLinkService
{
    private readonly object _gate = new();
    private string? _pending;
    private Action<string>? _handler;

    public void Dispatch(string route)
    {
        Action<string>? handler;
        lock (_gate)
        {
            handler = _handler;
            if (handler is null)
            {
                _pending = route;
                return;
            }
        }
        handler(route);
    }

    // Latest subscriber wins; re-subscription after a UI reload is harmless.
    public void Subscribe(Action<string> handler)
    {
        string? pending;
        lock (_gate)
        {
            _handler = handler;
            pending = _pending;
            _pending = null;
        }
        if (pending is not null)
            handler(pending);
    }
}

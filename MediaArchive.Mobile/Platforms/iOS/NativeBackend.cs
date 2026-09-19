using Foundation;
using MediaArchive.Services.Native;

namespace MediaArchive.Mobile;

// The C# object Swift talks to. One exported selector, three strings in;
// the reply goes back through MANativeApp.complete with the request id.
[Register("MANativeBackend")]
public sealed class NativeBackend(NativeApi api) : NSObject
{
    [Export("call:args:requestId:")]
    public void Call(NSString route, NSString args, NSString requestId)
    {
        var (routeName, argsJson, id) = (route.ToString(), args.ToString(), requestId.ToString());

        _ = Task.Run(async () =>
        {
            var (json, error) = await api.CallAsync(routeName, argsJson);
            NativeHost.Complete(id, json, error);
        });
    }
}

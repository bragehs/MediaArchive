using Foundation;
using MediaArchive.Services.Native;

namespace MediaArchive.Mobile;

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

using System.Runtime.InteropServices;
using Foundation;
using ObjCRuntime;
using UIKit;

namespace MediaArchive.Mobile;

// The three things C# asks of the Swift framework (MANativeApp), reached
// through the ObjC runtime the way WidgetSnapshotPublisher reaches MAWidgetLink.
public static class NativeHost
{
    [DllImport(Constants.ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr ObjcMsgSend(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport(Constants.ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void ObjcMsgSend(IntPtr receiver, IntPtr selector, IntPtr a, IntPtr b, IntPtr c);

    private static IntPtr AppClass => Class.GetHandle("MANativeApp") is var cls && cls != IntPtr.Zero
        ? cls
        : throw new InvalidOperationException(
            "MediaArchiveUI.framework is not embedded — build with ./ma, not dotnet build alone.");

    public static UIViewController MakeRoot(NativeBackend backend)
    {
        var handle = ObjcMsgSend(AppClass, Selector.GetHandle("makeRootWithBackend:"), backend.Handle);
        return Runtime.GetNSObject<UIViewController>(handle)
               ?? throw new InvalidOperationException("MANativeApp returned no root controller.");
    }

    public static void Open(string route)
    {
        using var ns = new NSString(route);
        ObjcMsgSend(AppClass, Selector.GetHandle("openRoute:"), ns.Handle);
    }

    public static void Complete(string requestId, string? json, string? error)
    {
        using var id = new NSString(requestId);
        using var payload = json is null ? null : new NSString(json);
        using var failure = error is null ? null : new NSString(error);
        ObjcMsgSend(AppClass, Selector.GetHandle("complete:json:error:"),
            id.Handle, payload?.Handle ?? IntPtr.Zero, failure?.Handle ?? IntPtr.Zero);
    }
}

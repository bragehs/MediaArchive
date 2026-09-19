import SwiftUI
import UIKit

// What C# reaches through the ObjC runtime: build the root, deliver a reply,
// hand over a deep link. Nothing else in the framework is visible to it.
@objc(MANativeApp)
public final class MANativeApp: NSObject {
    @objc(makeRootWithBackend:)
    public static func makeRoot(backend: NSObject) -> UIViewController {
        Backend.shared.attach(backend)
        let controller = UIHostingController(rootView: RootView())
        controller.view.backgroundColor = UIColor(Palette.bg)
        return controller
    }

    @objc(complete:json:error:)
    public static func complete(_ requestId: NSString, json: NSString?, error: NSString?) {
        Backend.shared.complete(id: String(requestId), json: json.map(String.init), error: error.map(String.init))
    }

    @objc(openRoute:)
    public static func open(_ route: NSString) {
        let route = String(route)
        Task { @MainActor in Router.shared.open(route) }
    }
}

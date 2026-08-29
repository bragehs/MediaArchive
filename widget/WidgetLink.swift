// WidgetKit is Swift-only, so the .NET app can't call it directly. This tiny
// framework exposes the one call the app needs through the ObjC runtime;
// WidgetSnapshotPublisher invokes it via objc_msgSend after each snapshot.

import Foundation
import WidgetKit

@objc(MAWidgetLink)
public final class MAWidgetLink: NSObject {
    @objc public static func reloadAll() {
        WidgetCenter.shared.reloadAllTimelines()
    }
}

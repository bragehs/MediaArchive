// WidgetKit is Swift-only, so the .NET app reaches it through this class via the ObjC runtime.

import Foundation
import WidgetKit

@objc(MAWidgetLink)
public final class MAWidgetLink: NSObject {
    @objc public static func reloadAll() {
        WidgetCenter.shared.reloadAllTimelines()
    }
}

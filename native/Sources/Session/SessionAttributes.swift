import ActivityKit
import Foundation

// Compiled into both the app framework and the widget extension, so the two ends agree on it.
struct SessionAttributes: ActivityAttributes {
    struct ContentState: Codable, Hashable {
        // Resuming shifts the anchor forward by the break, so elapsed stays continuous.
        var anchor: Date
        var pausedAt: Date?
    }

    var sessionId: Int
    var userMediaItemId: Int
    var title: String
    var kind: String
    var cover: String?
    var targetMinutes: Int?
}

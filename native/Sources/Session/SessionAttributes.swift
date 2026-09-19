import ActivityKit
import Foundation

// Compiled into the app framework and the widget extension alike — the one type
// both ends of the Live Activity agree on. Attributes are fixed for the life of
// the activity; the state is what pause and resume rewrite.
struct SessionAttributes: ActivityAttributes {
    struct ContentState: Codable, Hashable {
        // The timer runs from here. Pausing sets the moment the clock froze; resuming
        // clears it and shifts the anchor forward by the break, so elapsed stays continuous.
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

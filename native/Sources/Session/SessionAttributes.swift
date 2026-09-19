import ActivityKit
import Foundation

// Compiled into the app framework and the widget extension alike — the one type
// both ends of the Live Activity agree on. Attributes are fixed for the life of
// the activity; the state is what pause and resume rewrite.
struct SessionAttributes: ActivityAttributes {
    struct ContentState: Codable, Hashable {
        // The timer runs from here. Pausing freezes the minutes; resuming shifts
        // the anchor forward by the break, so elapsed stays continuous.
        var anchor: Date
        var frozenMinutes: Int?
    }

    var sessionId: Int
    var userMediaItemId: Int
    var title: String
    var kind: String
    var cover: String?
    var targetMinutes: Int?
}

import ActivityKit
import Foundation

// The app's side of the Live Activity: requested after C# has written the
// Session row, ended when the sitting resolves. Never updated to tick — the
// system renders the timer from the anchor date.
enum SessionActivity {
    static func start(_ session: LiveSession) {
        guard ActivityAuthorizationInfo().areActivitiesEnabled else { return }

        let attributes = SessionAttributes(
            sessionId: session.sessionId,
            userMediaItemId: session.userMediaItemId,
            title: session.title,
            kind: session.mediaType.rawValue,
            cover: session.cover,
            targetMinutes: session.targetMinutes)
        let state = SessionAttributes.ContentState(anchor: session.startedAt, frozenMinutes: nil)

        // A refused activity is a lost Lock Screen, not a lost session: the row is the record.
        _ = try? Activity.request(attributes: attributes, content: .init(state: state, staleDate: nil))
    }

    static func isAlive(sessionId: Int) -> Bool {
        Activity<SessionAttributes>.activities.contains { $0.attributes.sessionId == sessionId }
    }

    static func end(sessionId: Int) async {
        for activity in Activity<SessionAttributes>.activities
        where activity.attributes.sessionId == sessionId {
            await activity.end(nil, dismissalPolicy: .immediate)
        }
    }
}

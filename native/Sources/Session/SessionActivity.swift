import ActivityKit
import Foundation

// The app's side of the Live Activity: requested after C# has written the
// Session row, ended when the sitting resolves. Never updated to tick — the
// system renders the timer from the anchor date; only the pause intent updates it.
enum SessionActivity {
    // An hour before the activity's ~8h ceiling the view turns into a prompt to log.
    private static let warnAfter: TimeInterval = 7 * 60 * 60

    static func start(_ session: LiveSession) {
        guard ActivityAuthorizationInfo().areActivitiesEnabled else { return }

        let attributes = SessionAttributes(
            sessionId: session.sessionId,
            userMediaItemId: session.userMediaItemId,
            title: session.title,
            kind: session.mediaType.rawValue,
            cover: session.cover,
            targetMinutes: session.targetMinutes)
        // A re-requested activity picks up the breaks already taken, paused or not.
        let pausedAt = PauseLog.pausedAt(sessionId: session.sessionId)
        let shift = PauseLog.pausedSeconds(sessionId: session.sessionId, until: pausedAt ?? Date())
        let state = SessionAttributes.ContentState(anchor: session.startedAt.addingTimeInterval(shift), pausedAt: pausedAt)

        // A refused activity is a lost Lock Screen, not a lost session: the row is the record.
        _ = try? Activity.request(attributes: attributes,
                                  content: .init(state: state, staleDate: session.startedAt.addingTimeInterval(warnAfter)))
    }

    static func isAlive(sessionId: Int) -> Bool {
        Activity<SessionAttributes>.activities.contains { $0.attributes.sessionId == sessionId }
    }

    static func end(sessionId: Int) async {
        for activity in Activity<SessionAttributes>.activities
        where activity.attributes.sessionId == sessionId {
            await activity.end(nil, dismissalPolicy: .immediate)
        }
        PauseLog.clear(sessionId: sessionId)
    }
}

import ActivityKit
import AppIntents

// The one button on the Live Activity. LiveActivityIntent makes the system run it
// in the app process — the extension cannot see the activity — which is why the
// head copies the framework's Metadata.appintents into the app bundle.
struct PauseIntent: LiveActivityIntent {
    static var title: LocalizedStringResource = "Pause or resume the session"

    @Parameter(title: "Session") var sessionId: Int

    init() {}
    init(sessionId: Int) { self.sessionId = sessionId }

    func perform() async throws -> some IntentResult {
        let now = Date()
        for activity in Activity<SessionAttributes>.activities
        where activity.attributes.sessionId == sessionId {
            let state = activity.content.state
            let next: SessionAttributes.ContentState
            if let pausedAt = state.pausedAt {
                PauseLog.resume(sessionId: sessionId, at: now)
                next = .init(anchor: state.anchor.addingTimeInterval(now.timeIntervalSince(pausedAt)), pausedAt: nil)
            } else {
                PauseLog.pause(sessionId: sessionId, at: now)
                next = .init(anchor: state.anchor, pausedAt: now)
            }
            // The stale date is the ceiling warning, measured from the request; updates keep it.
            await activity.update(ActivityContent(state: next, staleDate: activity.content.staleDate))
        }
        return .result()
    }
}

import Foundation

// The breaks of a live session, kept in the app's defaults by the pause intent and
// summed when the session ends. Never a row: the database gets one PausedMinutes
// at the close, and the log is cleared with the activity.
enum PauseLog {
    private struct Break: Codable {
        var pausedAt: Date
        var resumedAt: Date?
    }

    static func pause(sessionId: Int, at date: Date) {
        var breaks = read(sessionId)
        guard breaks.last?.resumedAt != nil || breaks.isEmpty else { return }
        breaks.append(Break(pausedAt: date, resumedAt: nil))
        write(sessionId, breaks)
    }

    static func resume(sessionId: Int, at date: Date) {
        var breaks = read(sessionId)
        guard let last = breaks.indices.last, breaks[last].resumedAt == nil else { return }
        breaks[last].resumedAt = date
        write(sessionId, breaks)
    }

    // The open break, when the session is paused right now.
    static func pausedAt(sessionId: Int) -> Date? {
        guard let last = read(sessionId).last, last.resumedAt == nil else { return nil }
        return last.pausedAt
    }

    // An open break counts up to `until`, so ending while paused loses nothing.
    static func pausedSeconds(sessionId: Int, until: Date) -> TimeInterval {
        read(sessionId).reduce(0) { $0 + max(0, ($1.resumedAt ?? until).timeIntervalSince($1.pausedAt)) }
    }

    static func pausedMinutes(sessionId: Int) -> Int {
        Int((pausedSeconds(sessionId: sessionId, until: Date()) / 60).rounded())
    }

    static func clear(sessionId: Int) {
        UserDefaults.standard.removeObject(forKey: key(sessionId))
    }

    private static func key(_ sessionId: Int) -> String { "session.\(sessionId).breaks" }

    private static func read(_ sessionId: Int) -> [Break] {
        guard let data = UserDefaults.standard.data(forKey: key(sessionId)) else { return [] }
        return (try? JSONDecoder().decode([Break].self, from: data)) ?? []
    }

    private static func write(_ sessionId: Int, _ breaks: [Break]) {
        UserDefaults.standard.set(try? JSONEncoder().encode(breaks), forKey: key(sessionId))
    }
}

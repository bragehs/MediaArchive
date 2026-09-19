import Foundation
import Observation

@MainActor
@Observable
final class HomeStore {
    var page: Loadable<HomePage> = .loading
    var logTarget: OpenNowItem?
    var saving = false
    var error: String?

    var live: LiveSession? { page.value?.live }

    // A sitting older than the activity's ceiling is asked about, not kept quietly.
    var stale: LiveSession?
    private let staleAfter: TimeInterval = 8 * 60 * 60

    var staleItem: OpenNowItem? {
        guard let stale else { return nil }
        return page.value?.openNow.first { $0.openEntryId == stale.entryId }
    }

    // Nil when another item's session is running: one at a time, and the row says nothing.
    func sessionLabel(_ item: OpenNowItem) -> String? {
        guard let live else { return "Start session" }
        return live.entryId == item.openEntryId ? "End session" : nil
    }

    func toggleSession(_ item: OpenNowItem) async {
        saving = true
        error = nil
        defer { saving = false }
        do {
            if let live, live.entryId == item.openEntryId {
                try await api.endSession(SessionEnd(sessionId: live.sessionId, endedAt: Date(), pausedMinutes: 0))
                await SessionActivity.end(sessionId: live.sessionId)
            } else if live == nil {
                let session = try await api.startSession(StartSessionArgs(entryId: item.openEntryId, startedAt: Date()))
                SessionActivity.start(session)
            }
            await load()
        } catch {
            self.error = error.localizedDescription
        }
    }

    // Silent when something is already showing, so coming back from an item
    // refreshes without a loading flash.
    func load() async {
        if page.value == nil { page = .loading }
        do {
            page = .loaded(try await api.home())
            reconcile()
        } catch {
            page = .failed(error.localizedDescription)
        }
    }

    // The row outlives the activity: a relaunch mid-sitting gets its timer back,
    // and a sitting past the ceiling surfaces as a question on this screen.
    private func reconcile() {
        guard let live else { stale = nil; return }
        if live.startedAt.timeIntervalSinceNow < -staleAfter {
            stale = live
            return
        }
        stale = nil
        if !SessionActivity.isAlive(sessionId: live.sessionId) {
            SessionActivity.start(live)
        }
    }

    // Let a stale sitting go without logging it; its minutes stay on record.
    func discardStale() async {
        guard let stale else { return }
        saving = true
        error = nil
        defer { saving = false }
        do {
            try await api.endSession(SessionEnd(sessionId: stale.sessionId, endedAt: Date(), pausedMinutes: 0))
            await SessionActivity.end(sessionId: stale.sessionId)
            await load()
        } catch {
            self.error = error.localizedDescription
        }
    }
}

extension OpenNowItem: Identifiable {
    var id: Int { userMediaItemId }
}

extension CoverCard: Identifiable {
    var id: Int { userMediaItemId }
}

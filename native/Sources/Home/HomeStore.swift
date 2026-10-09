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

    var stale: LiveSession?
    private let staleAfter: TimeInterval = 8 * 60 * 60

    var staleItem: OpenNowItem? {
        guard let stale else { return nil }
        return page.value?.openNow.first { $0.openEntryId == stale.entryId }
    }

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
                logTarget = item
                return
            } else if live == nil {
                let session = try await api.startSession(StartSessionArgs(entryId: item.openEntryId, startedAt: Date()))
                SessionActivity.start(session)
            }
            await load()
        } catch {
            self.error = error.localizedDescription
        }
    }

    // Silent when something is already showing, so coming back refreshes without a loading flash.
    func load() async {
        if page.value == nil { page = .loading }
        do {
            page = .loaded(try await api.home())
            reconcile()
        } catch {
            page = .failed(error.localizedDescription)
        }
    }

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

    func discardStale() async {
        guard let stale else { return }
        saving = true
        error = nil
        defer { saving = false }
        do {
            try await api.endSession(SessionEnd(sessionId: stale.sessionId, endedAt: Date(),
                                                pausedMinutes: PauseLog.pausedMinutes(sessionId: stale.sessionId)))
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

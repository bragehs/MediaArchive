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
        } catch {
            page = .failed(error.localizedDescription)
        }
    }
}

extension OpenNowItem: Identifiable {
    var id: Int { userMediaItemId }
}

extension CoverCard: Identifiable {
    var id: Int { userMediaItemId }
}

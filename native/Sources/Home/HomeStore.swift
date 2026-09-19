import Foundation
import Observation

@MainActor
@Observable
final class HomeStore {
    var page: Loadable<HomePage> = .loading
    var logTarget: OpenNowItem?

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

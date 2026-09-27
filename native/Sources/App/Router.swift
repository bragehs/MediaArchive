import Foundation
import Observation

// The five slots of the system tab bar; Search carries the search role, so the
// system draws it apart from the others as its own round button.
enum AppTab: String, CaseIterable, Hashable {
    case now, explore, library, profile, search

    var label: String { rawValue.capitalized }

    var symbol: String {
        switch self {
        case .now: "clock.fill"
        case .explore: "sparkles"
        case .library: "books.vertical.fill"
        case .profile: "person.fill"
        case .search: "magnifyingglass"
        }
    }
}

enum Route: Hashable {
    case item(Int, log: Bool = false)
    case activity
    case onDeck
    case constellation
    case universes
    case creators
}

// Which tab is up and what each tab's stack holds. Deep links land here, and
// a route that arrives before the shell exists waits for it.
@MainActor
@Observable
final class Router {
    static let shared = Router()

    var selected: AppTab = .now
    var paths: [AppTab: [Route]] = [:]
    var ready = false {
        didSet { if ready, let pending { self.pending = nil; open(pending) } }
    }

    private var pending: String?

    func path(for tab: AppTab) -> [Route] { paths[tab] ?? [] }

    func setPath(_ path: [Route], for tab: AppTab) { paths[tab] = path }

    func push(_ route: Route) {
        paths[selected, default: []].append(route)
    }

    // "/item/{id}?log=true" — the grammar MauiProgram.TryMapDeepLink produces.
    func open(_ route: String) {
        guard ready else { pending = route; return }
        guard let components = URLComponents(string: route) else { return }

        let parts = components.path.split(separator: "/").map(String.init)
        guard parts.count == 2, parts[0] == "item", let id = Int(parts[1]) else { return }

        let log = components.queryItems?.contains { $0.name == "log" && $0.value == "true" } ?? false
        selected = .now
        paths[.now] = [.item(id, log: log)]
    }
}

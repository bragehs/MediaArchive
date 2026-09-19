import Foundation
import Observation

enum Tab: String, CaseIterable, Hashable {
    case home, explore, library, diary, profile

    var label: String { rawValue.capitalized }

    var glyph: String {
        switch self {
        case .home: "◈"
        case .explore: "⌕"
        case .library: "▦"
        case .diary: "❯"
        case .profile: "◉"
        }
    }
}

enum Route: Hashable {
    case item(Int, log: Bool = false)
    case diaryMonth(year: Int, month: Int)
    case universes
    case creators
}

// Which tab is up and what each tab's stack holds. Deep links land here, and
// a route that arrives before the shell exists waits for it.
@MainActor
@Observable
final class Router {
    static let shared = Router()

    var selected: Tab = .home
    var paths: [Tab: [Route]] = [:]
    var ready = false {
        didSet { if ready, let pending { self.pending = nil; open(pending) } }
    }

    private var pending: String?

    func path(for tab: Tab) -> [Route] { paths[tab] ?? [] }

    func setPath(_ path: [Route], for tab: Tab) { paths[tab] = path }

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
        selected = .home
        paths[.home] = [.item(id, log: log)]
    }
}

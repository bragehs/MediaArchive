import Foundation
import Observation

// One payload, two screens. Shared rather than owned by the wall because
// Shell builds the map's destination, and a pop must keep the filter.
@MainActor
@Observable
final class LibraryStore {
    static let shared = LibraryStore()

    enum Sort: String, CaseIterable, Hashable {
        case recent, rating, title, year

        var label: String {
            switch self {
            case .recent: "Recent"
            case .rating: "Rating"
            case .title: "Title"
            case .year: "Year"
            }
        }
    }

    enum TypeFilter: Hashable {
        case all
        case only(MediaType)
    }

    let graph = ConstellationGraph()

    var items: [LibraryItem] = []
    var loaded = false
    var error: String?
    var pendingFit = false

    var query = ""
    // nil while nothing is typed; the search reaches every status, the wall does not.
    var hits: [LibraryItem]?
    var genreMatches: [String] = []

    var type: TypeFilter = .all
    var sort: Sort = .recent
    var hideDropped = false
    var genre: String?

    // Detects unchanged data so a revisit keeps the laid-out map and its camera.
    private var signature: [String] = []

    func load() async {
        do {
            let loadedItems = try await api.library()
            items = loadedItems
            let next = loadedItems.map { "\($0.userMediaItemId):\($0.genres.joined(separator: ","))" }
            if next != signature || !loaded {
                signature = next
                graph.camera = .init()
                graph.build(loadedItems.filter { $0.status == .completed })
                graph.relax()
                pendingFit = true
            }
            loaded = true
            error = nil
        } catch {
            self.error = error.localizedDescription
        }
    }

    var rows: [LibraryItem] {
        (hits ?? items).filter { passes($0, type: type) }.sorted(by: ordered)
    }

    // Year rules only make sense while the order is chronological.
    var groups: [(year: Int, items: [LibraryItem])]? {
        guard sort == .recent else { return nil }
        var out: [(year: Int, items: [LibraryItem])] = []
        for item in rows {
            let year = touched(item).year
            if out.last?.year == year { out[out.count - 1].items.append(item) } else { out.append((year, [item])) }
        }
        return out
    }

    func count(_ filter: TypeFilter) -> Int {
        (hits ?? items).count { passes($0, type: filter) }
    }

    func touched(_ item: LibraryItem) -> DateOnly { item.lastActivity ?? item.addedDate }

    private func passes(_ item: LibraryItem, type: TypeFilter) -> Bool {
        if case .only(let wanted) = type, item.mediaType != wanted { return false }
        if hideDropped, item.status == .dropped { return false }
        if let genre, !item.genres.contains(genre) { return false }
        return true
    }

    private func ordered(_ a: LibraryItem, _ b: LibraryItem) -> Bool {
        switch sort {
        case .recent: touched(a) > touched(b)
        case .rating: (a.rating ?? -1, touched(a)) > (b.rating ?? -1, touched(b))
        case .title: a.title.lowercased() < b.title.lowercased()
        case .year: (a.year ?? 0, touched(a)) > (b.year ?? 0, touched(b))
        }
    }

    // Genres match against what is already loaded; titles and credits go to the archive search.
    func search() async {
        let text = query.trimmingCharacters(in: .whitespaces)
        guard !text.isEmpty else { clearSearch(); return }

        let lowered = text.lowercased()
        genreMatches = Set(items.flatMap(\.genres)).filter { $0.contains(lowered) }.sorted()
        hits = (try? await api.searchLibrary(QueryArgs(query: text))) ?? []
    }

    func clearSearch() {
        query = ""
        hits = nil
        genreMatches = []
    }

    func filter(byGenre genre: String) {
        self.genre = genre
        clearSearch()
    }
}

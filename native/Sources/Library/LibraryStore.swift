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

    // The map is completed-only: it is the archive of things actually consumed.
    var map = ConstellationMap()
    var camera = MapCamera()
    var fitted = false

    var items: [LibraryItem] = []
    var loaded = false
    var error: String?

    var query = ""
    // nil while nothing is typed; the search reaches every status, the wall does not.
    var hits: [LibraryItem]?

    var type: MediaType?
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
                map = ConstellationMap(loadedItems.filter { $0.status == .completed })
                fitted = false
            }
            loaded = true
            error = nil
        } catch {
            self.error = error.localizedDescription
        }
    }

    var rows: [LibraryItem] {
        (hits ?? items).filter { passes($0, type: type, genre: genre) }.sorted(by: ordered)
    }

    // Year rules only make sense while the order is chronological.
    func groups(of rows: [LibraryItem]) -> [(year: Int, items: [LibraryItem])]? {
        guard sort == .recent else { return nil }
        var out: [(year: Int, items: [LibraryItem])] = []
        for item in rows {
            let year = touched(item).year
            if out.last?.year == year { out[out.count - 1].items.append(item) } else { out.append((year, [item])) }
        }
        return out
    }

    // Each dropdown counts against the other filters, never against itself.
    func count(_ type: MediaType) -> Int {
        (hits ?? items).count { passes($0, type: type, genre: genre) }
    }

    func count(_ genre: String) -> Int {
        (hits ?? items).count { passes($0, type: type, genre: genre) }
    }

    var types: [MediaType] {
        MediaType.allCases.filter { type in (hits ?? items).contains { $0.mediaType == type } }
    }

    var genres: [String] {
        var shown = Set((hits ?? items).filter { passes($0, type: type, genre: nil) }.flatMap(\.genres))
        if let genre { shown.insert(genre) }
        return shown.sorted()
    }

    func touched(_ item: LibraryItem) -> DateOnly { item.lastActivity ?? item.addedDate }

    private func passes(_ item: LibraryItem, type: MediaType?, genre: String?) -> Bool {
        if let type, item.mediaType != type { return false }
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

    // Titles, credits and genre names all go to the archive search.
    func search() async {
        let text = query.trimmingCharacters(in: .whitespaces)
        guard !text.isEmpty else { clearSearch(); return }
        hits = (try? await api.searchLibrary(QueryArgs(query: text))) ?? []
    }

    func clearSearch() {
        query = ""
        hits = nil
    }

    func filter(byGenre genre: String) {
        self.genre = genre
        clearSearch()
    }
}

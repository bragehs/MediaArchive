import Foundation
import Observation

@MainActor
@Observable
final class LibraryStore {
    let graph = ConstellationGraph()

    var loaded = false
    var error: String?
    var pendingFit = false

    // Mirrors graph.selected for the panel; the graph itself is not observed.
    var selection: ConstellationGraph.Node?

    var query = ""
    var resultsOpen = false
    var genreHits: [ConstellationGraph.Node] = []
    var itemHits: [LibraryItem] = []
    var searched = false

    // Detects unchanged data so a revisit keeps the laid-out graph and camera.
    private var signature: [String] = []

    var countLabel: String {
        "\(graph.items.count) works · \(graph.genreNodes.count) genres"
    }

    func load() async {
        do {
            let items = try await api.library()
            let next = items.map { "\($0.userMediaItemId):\($0.genres.joined(separator: ","))" }
            if next != signature || !loaded {
                signature = next
                graph.camera = .init()
                graph.select(nil)
                selection = nil
                graph.build(items)
                graph.relax()
                pendingFit = true
            }
            loaded = true
            error = nil
        } catch {
            self.error = error.localizedDescription
        }
    }

    func select(_ node: ConstellationGraph.Node?) {
        graph.select(node)
        selection = node
    }

    func focus(_ node: ConstellationGraph.Node) {
        select(node)
        graph.center(on: node)
    }

    func clearSearch() {
        query = ""
        resultsOpen = false
        genreHits = []
        itemHits = []
        searched = false
        graph.searchHits = nil
    }

    // Genres match client-side; titles, credits and genres go to the archive search.
    func search() async {
        let text = query.trimmingCharacters(in: .whitespaces)
        guard !text.isEmpty else { clearSearch(); return }

        let lowered = text.lowercased()
        genreHits = graph.nodes.filter { $0.kind == .genre && $0.name.lowercased().contains(lowered) }
        itemHits = (try? await api.searchLibrary(QueryArgs(query: text))) ?? []

        var hits = Set(genreHits.map(\.id))
        let itemIds = Set(itemHits.map(\.userMediaItemId))
        for node in graph.nodes where node.kind == .item && itemIds.contains(graph.items[node.itemIndex].userMediaItemId) {
            hits.insert(node.id)
        }
        graph.searchHits = hits
        searched = true
        resultsOpen = true
    }

    // Co-occurring genres for the panel, most shared first.
    func related(to node: ConstellationGraph.Node) -> [(genre: String, count: Int)] {
        var counts: [String: Int] = [:]
        for index in graph.itemIndices(of: node) {
            for genre in Set(graph.items[index].genres) where genre != node.name {
                counts[genre, default: 0] += 1
            }
        }
        return counts.sorted { ($0.value, $1.key) > ($1.value, $0.key) }.map { ($0.key, $0.value) }
    }

    // Favourites first, then by rating.
    func members(of node: ConstellationGraph.Node) -> [LibraryItem] {
        graph.itemIndices(of: node)
            .map { graph.items[$0] }
            .sorted { a, b in
                if a.isFavorite != b.isFavorite { return a.isFavorite }
                return (a.rating ?? 0) > (b.rating ?? 0)
            }
    }
}

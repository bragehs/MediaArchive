import Foundation
import Observation

// The universal add: search a provider, pick the work, describe it, save it
// unstarted — or, on the backfill path, as one finished pass.
@MainActor
@Observable
final class AddStore {
    static let vocabPreview = 6

    var query = ""
    var mediaType: MediaType = .book
    var results: [MediaSearchResultDto] = []
    var searching = false
    var searched = false

    var selected: MediaSearchResultDto?
    var detail: MediaItemDto?
    var loadingDetail = false
    var seasons: [SeasonDto] = []
    var seasonChosen = false
    private var showTitle: String?

    var vocab = Vocabulary(genres: [], tags: [], universes: [], series: [])

    var allGenres = false
    var allTags = false
    var capturing = false
    var backfill = false
    var genres: [String] = []
    var tags: [String] = []
    private var newTags: [String: TagInput] = [:]
    var universe: [String] = []
    var series: [String] = []
    var seriesPosition: Int?
    var discovery: DiscoverySource?
    var releaseDate: DateOnly?
    var startDate: DateOnly? = .today
    var endDate: DateOnly? = .today
    var effort: Int?
    var audioHours: Double?       // audiobook total length, entered once
    var hoursListened: Double?    // audiobook effort, entered in hours
    var context: ConsumptionContext?
    var rating = 0
    var note = ""
    var startNote = ""

    var saving = false
    var error: String?
    var savedMessage: String?
    var savedItemId: Int?

    // Hours only convert back to pages when both lengths are known, so the
    // pages input stands in when they aren't — same rule as the log sheet.
    var isAudiobook: Bool {
        guard let detail, detail.mediaType == .book, (detail.length ?? 0) > 0 else { return false }
        return context == .audiobook && (audioHours ?? 0) > 0
    }

    var newTagNames: [String] {
        tags.filter { tag in !vocab.tags.contains { $0.caseInsensitiveCompare(tag) == .orderedSame } }
    }

    var canSubmit: Bool { newTagNames.allSatisfy { facet(of: $0) != nil } }

    var footHint: String? {
        newTagNames.contains { facet(of: $0) == nil } ? "Pick a facet for each new tag." : nil
    }

    func facet(of tag: String) -> TagFacet? { newTags[tag.lowercased()]?.facet }

    func appliesTo(of tag: String) -> MediaType? { newTags[tag.lowercased()]?.appliesTo }

    func setFacet(_ facet: TagFacet?, of tag: String) {
        var input = existing(tag)
        input.facet = facet
        newTags[tag.lowercased()] = input
    }

    func setAppliesTo(_ type: MediaType?, of tag: String) {
        var input = existing(tag)
        input.appliesTo = type
        newTags[tag.lowercased()] = input
    }

    private func existing(_ tag: String) -> TagInput {
        newTags[tag.lowercased()] ?? TagInput(name: tag, facet: nil, appliesTo: nil)
    }

    static func alreadyPrompt(_ type: MediaType) -> String {
        switch type {
        case .book: "Already read it?"
        case .game: "Already played it?"
        default: "Already watched it?"
        }
    }

    func refreshVocabulary() async {
        if let loaded = try? await api.vocabulary() { vocab = loaded }
    }

    func setType(_ type: MediaType) async {
        mediaType = type
        if searched { await runSearch() }
    }

    func runSearch() async {
        let trimmed = query.trimmingCharacters(in: .whitespaces)
        guard !trimmed.isEmpty else { return }

        searching = true
        searched = true
        error = nil
        savedMessage = nil
        savedItemId = nil
        defer { searching = false }

        do {
            results = try await api.search(SearchArgs(query: trimmed, mediaType: mediaType))
        } catch {
            results = []
            self.error = error.localizedDescription
        }
    }

    func select(_ result: MediaSearchResultDto) async {
        selected = result
        detail = nil
        seasons = []
        seasonChosen = false
        showTitle = nil
        loadingDetail = true
        error = nil
        savedMessage = nil
        savedItemId = nil
        defer { loadingDetail = false }

        do {
            let fetched = try await api.searchDetail(ExternalArgs(externalId: result.externalId, mediaType: result.mediaType))
            detail = fetched

            if result.mediaType == .show {
                showTitle = fetched.title
                seasons = try await api.seasons(QueryArgs(query: result.externalId))
                if seasons.count <= 1 { pickSeason(seasons.first) }
            } else {
                seasonChosen = true
                effort = fetched.length
                releaseDate = fetched.releaseDate
            }
        } catch {
            self.error = error.localizedDescription
        }
    }

    func pickSeason(_ season: SeasonDto?) {
        if let season, var picked = detail, let selected {
            picked.externalId = "\(selected.externalId)/season/\(season.seasonNumber)"
            picked.title = seasons.count > 1 ? "\(showTitle ?? picked.title) — \(season.name)" : picked.title
            picked.imageUrl = season.imageUrl ?? picked.imageUrl
            picked.length = season.episodeCount ?? picked.length
            picked.releaseDate = season.airDate ?? picked.releaseDate
            detail = picked
            if let showTitle { series = [showTitle] }
            seriesPosition = season.seasonNumber
        }

        seasonChosen = true
        effort = detail?.length
        releaseDate = detail?.releaseDate
    }

    static func seasonMeta(_ season: SeasonDto) -> String {
        var parts: [String] = []
        if let count = season.episodeCount { parts.append("\(count) \(plural(count, "ep"))") }
        if let aired = season.airDate { parts.append(String(aired.year)) }
        return parts.joined(separator: " · ")
    }

    // Back pops one step: capture → the work, the work → the results.
    func back() {
        if capturing { capturing = false } else { reset() }
    }

    private func effortToStore() -> Int? {
        isAudiobook ? pagesFromHours(hoursListened, audioHours, detail?.length) : effort
    }

    private static func trimmedOrNil(_ text: String) -> String? {
        let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }

    func submit() async {
        guard var item = detail else { return }
        saving = true
        error = nil
        defer { saving = false }

        item.releaseDate = releaseDate
        let details = WorkDetails(
            genres: genres,
            tags: tags.map(existing),
            universe: universe.first,
            series: series.first,
            seriesPosition: seriesPosition,
            discovery: discovery,
            audioHours: audioHours)
        let title = item.title

        do {
            if backfill {
                let start = PassStart(startDate: startDate, context: context, note: Self.trimmedOrNil(startNote))
                let finish = PassFinish(endDate: endDate ?? .today, rating: rating == 0 ? nil : rating,
                                        effort: effortToStore(), note: Self.trimmedOrNil(note), dropped: false)
                savedItemId = try await api.logCompleted(LogCompletedArgs(item: item, details: details, start: start, finish: finish)).id
                savedMessage = "“\(title)” — logged as finished."
            } else {
                savedItemId = try await api.addItem(AddItemArgs(item: item, details: details)).id
                savedMessage = "“\(title)” — added to the library, unstarted."
            }
            await refreshVocabulary()
            reset()
        } catch {
            self.error = error.localizedDescription
        }
    }

    func reset() {
        selected = nil
        detail = nil
        seasons = []
        seasonChosen = false
        showTitle = nil
        allGenres = false
        allTags = false
        capturing = false
        backfill = false
        genres = []
        tags = []
        newTags = [:]
        universe = []
        series = []
        seriesPosition = nil
        discovery = nil
        releaseDate = nil
        startDate = .today
        endDate = .today
        effort = nil
        audioHours = nil
        hoursListened = nil
        context = nil
        rating = 0
        note = ""
        startNote = ""
    }
}

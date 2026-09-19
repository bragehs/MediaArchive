import Foundation
import Observation

@MainActor
@Observable
final class ItemStore {
    enum PassForm { case none, start, finished }

    let userMediaItemId: Int
    var page: Loadable<ItemPage> = .loading

    // Classification, edited in place and compared against the loaded detail.
    var genres: [String] = []
    var tags: [String] = []
    var series: [String] = []
    var universe: [String] = []
    var seriesPosition: Int?
    var runtime: Int?

    var form: PassForm = .none
    var passStart: DateOnly? = .today
    var passEnd: DateOnly? = .today
    var passContext: ConsumptionContext?
    var passNote = ""
    var passRating = 0

    var logging = false
    var celebrate = false
    var saving = false
    var error: String?

    // Facet/AppliesTo of the item's existing tags, so re-saving keeps them.
    private var knownTags: [String: TagInput] = [:]

    init(userMediaItemId: Int) {
        self.userMediaItemId = userMediaItemId
    }

    var detail: ItemDetail? { page.value?.detail }

    // The one running session app-wide, and whether it is this item's.
    var live: LiveSession? { page.value?.live }

    var sessionHere: Bool {
        guard let live, let open = detail?.openPass else { return false }
        return live.entryId == open.entryId
    }

    var dirty: Bool {
        guard let detail else { return false }
        return genres != detail.genres
            || tags != detail.tags.map(\.name)
            || series != (detail.series.map { [$0] } ?? [])
            || universe != (detail.universe.map { [$0] } ?? [])
            || (series.isEmpty ? nil : seriesPosition) != detail.seriesPosition
            || runtime != detail.runtime
    }

    func load() async {
        if page.value == nil { page = .loading }
        do {
            let loaded = try await api.item(ItemArgs(userMediaItemId: userMediaItemId))
            page = .loaded(loaded)
            let detail = loaded.detail
            genres = detail.genres
            tags = detail.tags.map(\.name)
            knownTags = Dictionary(detail.tags.map { ($0.name.lowercased(), $0) }, uniquingKeysWith: { a, _ in a })
            series = detail.series.map { [$0] } ?? []
            universe = detail.universe.map { [$0] } ?? []
            seriesPosition = detail.seriesPosition
            runtime = detail.runtime
        } catch BackendError.empty {
            page = .failed("Item not found.")
        } catch {
            page = .failed(error.localizedDescription)
        }
    }

    func saveDetails() async {
        guard let detail else { return }
        await mutate {
            let tagInputs = tags.map { name in
                knownTags[name.lowercased()] ?? TagInput(name: name, facet: nil, appliesTo: nil)
            }
            if let runtime, runtime > 0, runtime != detail.runtime {
                try await api.setRuntime(SetRuntimeArgs(userMediaItemId: userMediaItemId, value: runtime))
            }
            try await api.updateDetails(UpdateDetailsArgs(userMediaItemId: userMediaItemId, details: WorkDetails(
                genres: genres, tags: tagInputs,
                universe: universe.first, series: series.first,
                seriesPosition: series.isEmpty ? nil : seriesPosition,
                discovery: nil, audioHours: nil)))
            await load()
        }
    }

    func setRating(_ rating: Int?) async {
        guard var loaded = page.value else { return }
        do {
            try await api.setRating(SetRatingArgs(userMediaItemId: userMediaItemId, rating: rating))
            loaded.detail.rating = rating
            page = .loaded(loaded)
        } catch {
            self.error = error.localizedDescription
        }
    }

    func toggleFavorite() async {
        guard var loaded = page.value else { return }
        let next = !loaded.detail.isFavorite
        do {
            try await api.setFavorite(SetFavoriteArgs(userMediaItemId: userMediaItemId, isFavorite: next))
            loaded.detail.isFavorite = next
            page = .loaded(loaded)
        } catch {
            self.error = error.localizedDescription
        }
    }

    func resume(_ pass: ResumablePass) async {
        await mutate {
            _ = try await api.resumePass(ResumePassArgs(entryId: pass.entryId,
                start: PassStart(startDate: .today, context: nil, note: nil)))
            await load()
        }
    }

    // A pass consumed before it was logged is opened and closed in one go.
    func savePass() async {
        let finished = form == .finished
        await mutate {
            let note = passNote.trimmingCharacters(in: .whitespacesAndNewlines)
            let created = try await api.startPass(StartPassArgs(
                userMediaItemId: userMediaItemId,
                start: PassStart(startDate: passStart, context: passContext, note: finished || note.isEmpty ? nil : note),
                allowConcurrent: finished))

            if finished {
                try await api.finishPass(FinishPassArgs(entryId: created.id, finish: PassFinish(
                    endDate: passEnd ?? .today, rating: passRating == 0 ? nil : passRating,
                    effort: nil, note: note.isEmpty ? nil : note, dropped: false)))
                celebrate = true
            }

            form = .none
            passNote = ""
            passRating = 0
            passStart = .today
            passEnd = .today
            passContext = nil
            await load()
        }
    }

    // The row is written before the activity is requested, so a refused
    // activity still leaves a session on record.
    func startSession() async {
        guard let open = detail?.openPass else { return }
        await mutate {
            let session = try await api.startSession(StartSessionArgs(entryId: open.entryId, startedAt: Date()))
            SessionActivity.start(session)
            await load()
        }
    }

    // Closes it bare: nothing logged. The sheet is the way to end one with a note.
    func endSession() async {
        guard let live else { return }
        await mutate {
            try await api.endSession(SessionEnd(sessionId: live.sessionId, endedAt: Date(), pausedMinutes: 0))
            await SessionActivity.end(sessionId: live.sessionId)
            await load()
        }
    }

    func logged(finished: Bool) {
        logging = false
        celebrate = finished
        Task { await load() }
    }

    private func mutate(_ work: () async throws -> Void) async {
        saving = true
        error = nil
        defer { saving = false }
        do {
            try await work()
        } catch {
            self.error = error.localizedDescription
        }
    }
}

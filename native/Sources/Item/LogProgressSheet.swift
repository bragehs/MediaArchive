import SwiftUI

@MainActor
@Observable
final class LogProgressStore {
    enum Mode: Hashable { case progress, finish }

    let entryId: Int
    let mediaType: MediaType
    // Present when this pass has a running session: the sheet then closes it with the note.
    let session: LiveSession?
    var entry: Loadable<EntryEffort> = .loading
    var mode: Mode = .progress

    var runtime: Int?
    var progressEffort: Int?
    var progressHoursLeft: Double?
    var progressNote = ""

    var endDate: DateOnly? = .today
    var finishEffort: Int?
    var finishHours: Double?
    var rating = 0
    var finishNote = ""
    var dropped = false

    var saving = false
    var error: String?

    init(entryId: Int, mediaType: MediaType, session: LiveSession?) {
        self.entryId = entryId
        self.mediaType = mediaType
        self.session = session
    }

    var pausedMinutes: Int { session.map { PauseLog.pausedMinutes(sessionId: $0.sessionId) } ?? 0 }

    // Minutes the sitting actually ran: wall clock less the breaks. A suggestion, never the effort.
    var elapsedMinutes: Int? {
        session.map { max(0, Int(Date().timeIntervalSince($0.startedAt) / 60) - pausedMinutes) }
    }

    var measuredRuntime: Bool { entry.value?.suggestedRuntime != nil }

    // Loaded, not passed in, so no call site can render this in the wrong unit.
    var audiobook: Bool {
        guard let entry = entry.value else { return false }
        return entry.audiobook && (entry.audioHours ?? 0) > 0 && (entry.pageCount ?? 0) > 0
    }

    var needsRuntime: Bool { entry.value.map { !$0.runtimeKnown } ?? false }

    // Audible shows the remaining time, so that is what gets typed — the stored
    // effort stays cumulative pages, converted at this boundary and no deeper.
    var previousHoursLeft: Double? {
        guard let entry = entry.value, let effort = entry.effort,
              let hours = entry.audioHours, hours > 0,
              let pages = entry.pageCount, pages > 0 else { return nil }
        return ((hours - Double(effort) / Double(pages) * hours) * 10).rounded() / 10
    }

    var canSubmit: Bool {
        guard let entry = entry.value, !needsRuntime || (runtime ?? 0) > 0 else { return false }
        switch mode {
        case .progress:
            if audiobook {
                guard let left = progressHoursLeft, let total = entry.audioHours else { return false }
                return left >= 0 && left <= total
            }
            return (progressEffort ?? 0) > 0
        case .finish:
            return endDate != nil
        }
    }

    func load() async {
        do {
            let loaded = try await api.entryEffort(EntryArgs(entryId: entryId, elapsedMinutes: elapsedMinutes))
            entry = .loaded(loaded)
            prefill(loaded)
        } catch {
            entry = .failed(error.localizedDescription)
        }
    }

    // What the sitting measured fills the form; C# decided what was grounded enough to
    // suggest, and everything here stays editable.
    private func prefill(_ entry: EntryEffort) {
        guard let session, let elapsed = elapsedMinutes else { return }
        if let effort = entry.suggestedEffort {
            progressEffort = effort
            finishEffort = effort
        }
        if let left = entry.suggestedHoursLeft {
            progressHoursLeft = left
            // The finish form takes hours listened, the other side of the same figure.
            if let total = entry.audioHours { finishHours = max(0, ((total - left) * 10).rounded() / 10) }
        }
        if let measured = entry.suggestedRuntime {
            runtime = measured
        }
        // A film that ran its length is finished, not in progress; a show's target is one episode.
        if mediaType == .movie, let target = session.targetMinutes, elapsed * 10 >= target * 9 {
            mode = .finish
        }
    }

    // Ask the number you know — episodes — and the sitting has measured the episode length.
    func deriveRuntimeIfMeasured() {
        guard needsRuntime, runtime == nil, let elapsed = elapsedMinutes, elapsed > 0,
              let entry = entry.value, let effort = progressEffort else { return }
        let episodes = effort - (entry.effort ?? 0)
        if episodes > 0 { runtime = elapsed / episodes }
    }

    // One line under the field that was filled: where the number came from.
    var sessionHint: String? {
        guard let elapsed = elapsedMinutes else { return nil }
        return "Estimated from this sitting's \(elapsed) min" + (pausedMinutes > 0 ? " (\(pausedMinutes) paused)" : "")
    }

    // A sitting that produced nothing worth a note: the row still keeps its minutes.
    func endWithoutLogging() async -> Bool {
        guard let session else { return false }
        saving = true
        error = nil
        defer { saving = false }
        do {
            try await api.endSession(SessionEnd(sessionId: session.sessionId, endedAt: Date(), pausedMinutes: pausedMinutes))
            await SessionActivity.end(sessionId: session.sessionId)
            return true
        } catch {
            self.error = error.localizedDescription
            return false
        }
    }

    private func pages(fromHours hours: Double?) -> Int? {
        pagesFromHours(hours, entry.value?.audioHours, entry.value?.pageCount)
    }

    // Returns whether a pass was completed, so the caller can celebrate.
    func submit() async -> Bool? {
        guard let entry = entry.value else { return nil }
        saving = true
        error = nil
        defer { saving = false }

        do {
            if needsRuntime, let runtime {
                try await api.setRuntime(SetRuntimeArgs(userMediaItemId: entry.userMediaItemId, value: runtime))
            }

            // Closed and linked in the same save as the note it produced.
            let end = session.map { SessionEnd(sessionId: $0.sessionId, endedAt: Date(), pausedMinutes: pausedMinutes) }
            let finished: Bool
            switch mode {
            case .progress:
                let effort = audiobook
                    ? pages(fromHours: (entry.audioHours ?? 0) - (progressHoursLeft ?? 0))
                    : progressEffort
                try await api.addNote(AddNoteArgs(entryId: entryId,
                    note: NoteInput(text: progressNote.trimmingCharacters(in: .whitespacesAndNewlines), effortAtTime: effort),
                    session: end))
                finished = false
            case .finish:
                let note = finishNote.trimmingCharacters(in: .whitespacesAndNewlines)
                let effort = audiobook ? pages(fromHours: finishHours) : finishEffort
                try await api.finishPass(FinishPassArgs(entryId: entryId, finish: PassFinish(
                    endDate: endDate ?? .today, rating: rating == 0 ? nil : rating,
                    effort: effort, note: note.isEmpty ? nil : note, dropped: dropped),
                    session: end))
                finished = !dropped
            }
            if let session {
                await SessionActivity.end(sessionId: session.sessionId)
            }
            return finished
        } catch {
            self.error = error.localizedDescription
            return nil
        }
    }
}

// Progress or finish on an open pass. Presented as a sheet from Home and Item.
struct LogProgressSheet: View {
    let title: String
    let mediaType: MediaType
    let onLogged: (_ finished: Bool) -> Void

    @State private var store: LogProgressStore
    @Environment(\.lexicon) private var lexicon
    @Environment(\.dismiss) private var dismiss

    init(entryId: Int, title: String, mediaType: MediaType, session: LiveSession? = nil,
         onLogged: @escaping (_ finished: Bool) -> Void) {
        self.title = title
        self.mediaType = mediaType
        self.onLogged = onLogged
        _store = State(initialValue: LogProgressStore(entryId: entryId, mediaType: mediaType, session: session))
    }

    private var unit: String { lexicon.unit(mediaType) }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 18) {
                HStack(alignment: .firstTextBaseline, spacing: 12) {
                    Eyebrow("Log", size: 10.5, color: Palette.muted, tracking: 0.22)
                    Text("\(title) · \(lexicon.label(mediaType))")
                        .font(Fonts.display(14, bold: true))
                        .foregroundStyle(Palette.ink)
                }
                .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.bottom, 12)
                .overlay(alignment: .bottom) { HairlineRule(height: 2) }

                VStack(alignment: .leading, spacing: 0) {
                    FieldLabel("What are you logging?")
                    SegmentBar(options: [.progress, .finish], selection: $store.mode) {
                        $0 == .progress ? "Progress" : "Finish"
                    }
                }

                if store.needsRuntime {
                    VStack(alignment: .leading, spacing: 0) {
                        FieldLabel(lexicon.type(mediaType).runtimeLabel, required: true)
                        NumberField(value: $store.runtime)
                        Eyebrow(store.measuredRuntime
                                ? "Measured by this sitting"
                                : "Not known for this \(lexicon.label(mediaType).lowercased()) — without it the time never counts.",
                                size: 9.5, tracking: 0.05, bold: false)
                            .padding(.top, 6)
                    }
                }

                switch store.entry {
                case .loading:
                    Aside("Loading…")
                case .failed(let message):
                    Notice(text: message)
                case .loaded(let entry):
                    if store.mode == .progress {
                        progressFields(entry)
                    } else {
                        finishFields(entry)
                    }
                }

                if let error = store.error {
                    Notice(text: error)
                }

                HStack(spacing: 14) {
                    Spacer()
                    Button(store.session == nil ? "Cancel" : "Keep going") { dismiss() }.buttonStyle(GhostButtonStyle())
                    Button(store.saving ? "…" : store.mode == .progress ? "Save progress" : "Finish it") {
                        Task {
                            if let finished = await store.submit() {
                                onLogged(finished)
                            }
                        }
                    }
                    .buttonStyle(PrimaryButtonStyle())
                    .disabledLook(store.saving || !store.canSubmit)
                }
                if store.session != nil {
                    Button {
                        Task { if await store.endWithoutLogging() { onLogged(false) } }
                    } label: {
                        Eyebrow("End without logging", size: 9, color: Palette.ac2, tracking: 0.1)
                    }
                    .buttonStyle(.plain)
                    .disabledLook(store.saving)
                    .frame(maxWidth: .infinity, alignment: .trailing)
                    .padding(.top, -6)
                }
            }
            .padding(26)
        }
        .scrollDismissesKeyboard(.interactively)
        .onChange(of: store.progressEffort) { store.deriveRuntimeIfMeasured() }
        .background(Palette.bg.ignoresSafeArea())
        .presentationBackground(Palette.bg)
        .presentationDetents([.medium, .large])
        .presentationDragIndicator(.visible)
        .overlay(alignment: .top) { Rectangle().fill(Palette.ac).frame(height: 3) }
        .task { await store.load() }
    }

    @ViewBuilder
    private func progressFields(_ entry: EntryEffort) -> some View {
        VStack(alignment: .leading, spacing: 0) {
            if store.audiobook {
                FieldLabel("Hours left", required: true)
                DecimalField(value: $store.progressHoursLeft, placeholder: store.previousHoursLeft.map(trimmed) ?? "")
                hint(store.previousHoursLeft.map { "last logged with \(trimmed($0)) h left" })
            } else {
                FieldLabel("Effort so far (\(unit))", required: true)
                NumberField(value: $store.progressEffort, placeholder: entry.effort.map(String.init) ?? "")
                hint(entry.effort.map { "last logged at \($0) \(unit)" })
            }
        }
        VStack(alignment: .leading, spacing: 0) {
            FieldLabel("Progress note", optional: true)
            TextArea(text: $store.progressNote)
        }
    }

    // The sitting's estimate first, the last logged figure after it; nothing when there is neither.
    @ViewBuilder
    private func hint(_ previous: String?) -> some View {
        let parts = [store.sessionHint, previous.map { store.sessionHint == nil ? $0.prefix(1).uppercased() + $0.dropFirst() : $0 }]
            .compactMap { $0 }
        if !parts.isEmpty {
            Eyebrow(parts.joined(separator: " · "), size: 9.5, tracking: 0.05, bold: false).padding(.top, 6)
        }
    }

    @ViewBuilder
    private func finishFields(_ entry: EntryEffort) -> some View {
        HStack(alignment: .top, spacing: 22) {
            VStack(alignment: .leading, spacing: 0) {
                FieldLabel("End")
                DateField(date: $store.endDate)
            }
            VStack(alignment: .leading, spacing: 0) {
                if store.audiobook {
                    FieldLabel("Hours listened", optional: true)
                    DecimalField(value: $store.finishHours, placeholder: entry.audioHours.map(trimmed) ?? "")
                } else {
                    FieldLabel("Effort (\(unit))", optional: true)
                    NumberField(value: $store.finishEffort)
                }
                hint(nil)
            }
        }
        HStack(alignment: .top, spacing: 22) {
            VStack(alignment: .leading, spacing: 0) {
                FieldLabel("Outcome")
                SegmentBar(options: [false, true], selection: $store.dropped) { $0 ? "Dropped" : "Completed" }
            }
            if !store.dropped {
                VStack(alignment: .leading, spacing: 0) {
                    FieldLabel("Rating")
                    StarRating(value: $store.rating)
                }
            }
        }
        VStack(alignment: .leading, spacing: 0) {
            FieldLabel(store.dropped ? "Why you dropped it" : "Finish note", optional: true)
            TextArea(text: $store.finishNote, placeholder: "How it landed…")
        }
    }
}

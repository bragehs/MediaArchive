import SwiftUI

@MainActor
@Observable
final class LogProgressStore {
    enum Mode: Hashable { case progress, finish }

    let entryId: Int
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

    init(entryId: Int) {
        self.entryId = entryId
    }

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
            entry = .loaded(try await api.entryEffort(EntryArgs(entryId: entryId)))
        } catch {
            entry = .failed(error.localizedDescription)
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

            switch mode {
            case .progress:
                let effort = audiobook
                    ? pages(fromHours: (entry.audioHours ?? 0) - (progressHoursLeft ?? 0))
                    : progressEffort
                try await api.addNote(AddNoteArgs(entryId: entryId,
                    note: NoteInput(text: progressNote.trimmingCharacters(in: .whitespacesAndNewlines), effortAtTime: effort)))
                return false
            case .finish:
                let note = finishNote.trimmingCharacters(in: .whitespacesAndNewlines)
                let effort = audiobook ? pages(fromHours: finishHours) : finishEffort
                try await api.finishPass(FinishPassArgs(entryId: entryId, finish: PassFinish(
                    endDate: endDate ?? .today, rating: rating == 0 ? nil : rating,
                    effort: effort, note: note.isEmpty ? nil : note, dropped: dropped)))
                return !dropped
            }
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

    init(entryId: Int, title: String, mediaType: MediaType, onLogged: @escaping (_ finished: Bool) -> Void) {
        self.title = title
        self.mediaType = mediaType
        self.onLogged = onLogged
        _store = State(initialValue: LogProgressStore(entryId: entryId))
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
                        Eyebrow("Not known for this \(lexicon.label(mediaType).lowercased()) — without it the time never counts.",
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
                    Button("Cancel") { dismiss() }.buttonStyle(GhostButtonStyle())
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
            }
            .padding(26)
        }
        .scrollDismissesKeyboard(.interactively)
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
                if let left = store.previousHoursLeft {
                    Eyebrow("Last logged with \(trimmed(left)) h left", size: 9.5, tracking: 0.05, bold: false).padding(.top, 6)
                }
            } else {
                FieldLabel("Effort so far (\(unit))", required: true)
                NumberField(value: $store.progressEffort, placeholder: entry.effort.map(String.init) ?? "")
                if let previous = entry.effort {
                    Eyebrow("Last logged at \(previous) \(unit)", size: 9.5, tracking: 0.05, bold: false).padding(.top, 6)
                }
            }
        }
        VStack(alignment: .leading, spacing: 0) {
            FieldLabel("Progress note", optional: true)
            TextArea(text: $store.progressNote)
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

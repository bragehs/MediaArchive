import SwiftUI

// One item's record: hero, facts, the open pass or the pass forms, the blurb,
// the classification editor and the pass history.
struct ItemView: View {
    let openLog: Bool

    @State private var store: ItemStore
    @State private var armedLog = false
    @Environment(\.lexicon) private var lexicon
    @Environment(\.dismiss) private var dismiss

    init(userMediaItemId: Int, openLog: Bool = false) {
        self.openLog = openLog
        _store = State(initialValue: ItemStore(userMediaItemId: userMediaItemId))
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                topNav
                switch store.page {
                case .loading:
                    Aside("Loading…").padding(.vertical, 16)
                case .failed(let message):
                    Aside(message).padding(.vertical, 16)
                case .loaded(let page):
                    content(page)
                }
            }
        }
        .page()
        .scrollDismissesKeyboard(.interactively)
        .task {
            await store.load()
            // Set by widget deep links: open the dialog as soon as the item
            // loads, provided a pass is actually open right now.
            if openLog, !armedLog, store.detail?.openPass != nil {
                armedLog = true
                store.logging = true
            }
        }
        .sheet(isPresented: $store.logging) {
            if let detail = store.detail, let open = detail.openPass {
                LogProgressSheet(entryId: open.entryId, title: detail.title, mediaType: detail.mediaType) { finished in
                    store.logged(finished: finished)
                }
            }
        }
        .confetti($store.celebrate)
    }

    private var topNav: some View {
        HStack {
            Button { dismiss() } label: {
                Eyebrow("‹ Back", size: 13, color: Palette.ac, tracking: 0.08, bold: false)
            }
            .buttonStyle(.plain)
            Spacer()
            if let detail = store.detail {
                Button { Task { await store.toggleFavorite() } } label: {
                    Text(detail.isFavorite ? "♥" : "♡")
                        .font(.system(size: 19))
                        .foregroundStyle(detail.isFavorite ? Palette.ac2 : Palette.dim)
                }
                .buttonStyle(.plain)
            }
        }
        .padding(.top, 2)
        .padding(.bottom, 6)
    }

    @ViewBuilder
    private func content(_ page: ItemPage) -> some View {
        let item = page.detail
        let unit = lexicon.unit(item.mediaType)

        hero(item)
        facts(item)

        if let open = item.openPass {
            openPass(open, item: item, unit: unit)
        } else if store.form != .none {
            passForm(item)
        } else {
            passButtons(item, unit: unit)
        }

        if let description = item.description, !description.trimmingCharacters(in: .whitespaces).isEmpty {
            Blurb(text: description)
        }

        SectionHead("Classification", right: store.dirty ? "unsaved" : nil, rightColor: Palette.ac2)
        classification(page, unit: unit)

        if let error = store.error {
            Notice(text: error).padding(.top, 10)
        }

        Button(store.saving ? "…" : "Save classification") { Task { await store.saveDetails() } }
            .buttonStyle(GhostButtonStyle(fullWidth: true))
            .disabledLook(store.saving || !store.dirty)
            .padding(.top, 14)

        SectionHead("Passes", right: "\(page.history.count) total", rightColor: Palette.ac2)
        if page.history.isEmpty {
            Aside("No passes yet.").padding(.vertical, 16)
        } else {
            ForEach(page.history, id: \.entryId) { pass in
                PassRow(pass: pass)
            }
        }
    }

    private func hero(_ item: ItemDetail) -> some View {
        HStack(alignment: .top, spacing: 14) {
            CoverTile(url: item.imageUrl, title: item.title, width: 104, radius: 10, fallbackPadding: 9, fallbackSize: 11)
            VStack(alignment: .leading, spacing: 0) {
                Text(item.title)
                    .font(Fonts.title(21))
                    .foregroundStyle(Palette.ink)
                if let creator = item.creator, !creator.isEmpty {
                    Text(creator).font(Fonts.display(12)).foregroundStyle(Palette.muted).padding(.top, 4)
                }
                Eyebrow("\(lexicon.label(item.mediaType)) · \(lexicon.glyph(item.status)) \(lexicon.label(item.status))",
                        size: 9, color: Palette.accent(item.mediaType), tracking: 0.1, bold: false)
                    .padding(.top, 10)
                StarRating(value: Binding(
                    get: { item.rating ?? 0 },
                    set: { value in Task { await store.setRating(value == 0 ? nil : value) } }),
                    size: 17, fill: Palette.ac,
                    clearLabel: item.rating.map { "\(stars(Double($0))) — clear" } ?? "clear")
                    .padding(.top, 10)
            }
        }
        .padding(.top, 6)
    }

    private func facts(_ item: ItemDetail) -> some View {
        var cells: [(String, String, Bool)] = []
        if let released = item.releaseDate { cells.append((String(released.year), "Released", false)) }
        if let length = item.length { cells.append((String(length), lexicon.unit(item.mediaType), false)) }
        if let external = item.externalRating {
            cells.append((String(format: "%.1f", external),
                          "Public" + (item.externalRatingCount.map { " · \(grouped(Double($0)))" } ?? ""), false))
        }
        cells.append((item.addedDate.formatted("d MMM yyyy"), "Added", true))

        return HStack(spacing: 0) {
            ForEach(Array(cells.enumerated()), id: \.offset) { index, cell in
                VStack(alignment: .leading, spacing: 6) {
                    Text(cell.0)
                        .font(Fonts.title(cell.2 ? 12 : 17))
                        .foregroundStyle(Palette.ac)
                    Eyebrow(cell.1, size: 8, tracking: 0.1, bold: false)
                }
                .padding(.vertical, 11)
                .padding(.trailing, 6)
                .frame(maxWidth: .infinity, alignment: .leading)
                .overlay(alignment: .trailing) {
                    if index < cells.count - 1 { Rectangle().fill(Palette.line).frame(width: 1) }
                }
                .padding(.leading, index == 0 ? 0 : 8)
            }
        }
        .overlay(alignment: .top) { Rectangle().fill(Palette.ink).frame(height: 2) }
        .overlay(alignment: .bottom) { HairlineRule() }
        .padding(.top, 18)
    }

    private func openPassLine(_ open: OpenPassSummary, unit: String) -> String {
        var line = "In progress since \(open.startDate?.formatted("d MMM") ?? "—")"
        if let effort = open.effort {
            line += " · \(effort) \(unit)" + (open.progress.map { " (\(Int($0.rounded()))%)" } ?? "")
        }
        return line
    }

    @ViewBuilder
    private func openPass(_ open: OpenPassSummary, item: ItemDetail, unit: String) -> some View {
        Aside(openPassLine(open, unit: unit), size: 12).padding(.top, 16)
        if let progress = open.progress {
            ProgressBar(percent: progress, height: 5).padding(.top, 8)
        }
        Button("Log progress →") { store.logging = true }
            .buttonStyle(PrimaryButtonStyle(fullWidth: true))
            .padding(.top, 16)
        if let live = store.live, store.sessionHere {
            Aside("Session running since \(live.startedAt.formatted(date: .omitted, time: .shortened))", size: 11)
                .padding(.top, 10)
            Button(store.saving ? "…" : "End the session") { Task { await store.endSession() } }
                .buttonStyle(GhostButtonStyle(fullWidth: true))
                .disabledLook(store.saving)
                .padding(.top, 8)
        } else if store.live == nil {
            Button(store.saving ? "…" : "Start a session") { Task { await store.startSession() } }
                .buttonStyle(GhostButtonStyle(fullWidth: true))
                .disabledLook(store.saving)
                .padding(.top, 8)
        }
    }

    @ViewBuilder
    private func passForm(_ item: ItemDetail) -> some View {
        let finished = store.form == .finished
        VStack(alignment: .leading, spacing: 0) {
            FieldLabel("Start date").padding(.top, 12)
            DateField(date: $store.passStart)
            if finished {
                FieldLabel("End date").padding(.top, 12)
                DateField(date: $store.passEnd)
            }
            HStack(alignment: .top, spacing: 10) {
                VStack(alignment: .leading, spacing: 0) {
                    FieldLabel("How", optional: true).padding(.top, 12)
                    MenuField(selection: $store.passContext, options: lexicon.type(item.mediaType).contexts) {
                        lexicon.label($0)
                    }
                }
                if finished {
                    VStack(alignment: .leading, spacing: 0) {
                        FieldLabel("Rating", optional: true).padding(.top, 12)
                        StarRating(value: $store.passRating, size: 18)
                    }
                }
            }
            FieldLabel(finished ? "Finish note" : "Opening note", optional: true).padding(.top, 12)
            TextArea(text: $store.passNote, placeholder: finished ? "How it landed…" : "Why now, what you expect…")
            HStack(spacing: 10) {
                Button("Cancel") { store.form = .none }
                    .buttonStyle(GhostButtonStyle(fullWidth: true))
                Button(store.saving ? "…" : finished ? "Log the pass" : "Open the pass") {
                    Task { await store.savePass() }
                }
                .buttonStyle(PrimaryButtonStyle(fullWidth: true))
                .disabledLook(store.saving)
            }
            .padding(.top, 12)
        }
        .padding(.top, 14)
    }

    @ViewBuilder
    private func passButtons(_ item: ItemDetail, unit: String) -> some View {
        VStack(spacing: 8) {
            Button(item.passCount == 0 ? "Start a pass" : "Start a new pass") { store.form = .start }
                .buttonStyle(PrimaryButtonStyle(fullWidth: true))
            Button("Log a finished pass") { store.form = .finished }
                .buttonStyle(GhostButtonStyle(fullWidth: true))
            if let resumable = item.resumable {
                let tail = resumable.effort.map { effort in
                    " · \(effort)" + (item.length.map { "/\($0)" } ?? "") + " \(unit)"
                } ?? ""
                Button("Pick up where you left off\(tail)") { Task { await store.resume(resumable) } }
                    .buttonStyle(GhostButtonStyle(fullWidth: true))
                    .disabledLook(store.saving)
            }
        }
        .padding(.top, 16)
    }

    @ViewBuilder
    private func classification(_ page: ItemPage, unit: String) -> some View {
        let item = page.detail
        VStack(alignment: .leading, spacing: 8) {
            VStack(alignment: .leading, spacing: 0) {
                FieldLabel("Genres")
                VocabularyPicker(selected: $store.genres, suggestions: page.vocabulary.genres, placeholder: "Fantasy, Noir…")
            }
            VStack(alignment: .leading, spacing: 0) {
                FieldLabel("Tags")
                VocabularyPicker(selected: $store.tags, suggestions: page.vocabulary.tags, placeholder: "melancholy, slow burn…")
            }
            VStack(alignment: .leading, spacing: 0) {
                FieldLabel("Series")
                VocabularyPicker(selected: $store.series, suggestions: page.vocabulary.series, max: 1, placeholder: "Mistborn Era 1…")
            }
            if !store.series.isEmpty {
                VStack(alignment: .leading, spacing: 0) {
                    FieldLabel("Position", optional: true)
                    NumberField(value: $store.seriesPosition)
                }
            }
            VStack(alignment: .leading, spacing: 0) {
                HStack(spacing: 4) {
                    FieldLabel(lexicon.type(item.mediaType).runtimeLabel)
                    if (item.runtime ?? 0) <= 0 {
                        Aside("unknown — the time never counts without it", size: 10)
                            .padding(.bottom, 6)
                    }
                }
                NumberField(value: $store.runtime)
            }
            VStack(alignment: .leading, spacing: 0) {
                FieldLabel("Universe")
                VocabularyPicker(selected: $store.universe, suggestions: page.vocabulary.universes, max: 1, placeholder: "Cosmere…")
            }
        }
    }
}

private struct PassRow: View {
    let pass: PassSummary
    @Environment(\.lexicon) private var lexicon

    private var label: (String, Color) {
        if pass.endDate == nil { return ("▐▐ In progress", Palette.ac2) }
        if pass.outcome == .dropped { return ("✕ Dropped", Palette.dim) }
        return ("✓ Completed", Palette.ac)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(alignment: .firstTextBaseline, spacing: 10) {
                Eyebrow(label.0, size: 9, color: label.1, tracking: 0.1)
                Eyebrow(meta, size: 10, tracking: 0.05, bold: false)
            }
            if let rated = pass.ratingAtTime {
                Text("★ \(stars(Double(rated)))")
                    .font(Fonts.display(11))
                    .foregroundStyle(Palette.ac2)
                    .padding(.top, 6)
            }
            ForEach(Array(pass.notes.enumerated()), id: \.offset) { _, note in
                if let text = note.text, !text.trimmingCharacters(in: .whitespaces).isEmpty {
                    VStack(alignment: .leading, spacing: 2) {
                        Eyebrow(note.kind.rawValue, size: 8, tracking: 0.1, bold: false)
                        Text(text)
                            .font(Fonts.serif(12.5, italic: true))
                            .foregroundStyle(Palette.muted)
                            .lineSpacing(3)
                    }
                    .padding(.leading, 10)
                    .overlay(alignment: .leading) { Rectangle().fill(Palette.ac).frame(width: 2) }
                    .padding(.top, 8)
                }
            }
        }
        .padding(.vertical, 12)
        .frame(maxWidth: .infinity, alignment: .leading)
        .overlay(alignment: .bottom) { HairlineRule(color: Palette.line2) }
    }

    private var meta: String {
        var text = pass.startDate?.formatted("d MMM yyyy") ?? "—"
        if let end = pass.endDate { text += " → " + end.formatted("d MMM yyyy") }
        if let context = pass.context { text += " · " + lexicon.label(context) }
        return text
    }
}

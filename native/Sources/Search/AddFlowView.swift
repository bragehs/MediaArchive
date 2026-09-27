import SwiftUI

// Search → the work → the form, each its own screen, the header chevron
// popping one level. Embedded in Explore.
struct AddFlowView: View {
    @Bindable var store: AddStore
    @Environment(\.lexicon) private var lexicon

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            header
            VStack(alignment: .leading, spacing: 0) {
                if store.selected == nil {
                    searchScreen
                } else if store.loadingDetail {
                    Aside("Fetching details…").padding(.top, 16)
                } else if store.selected?.mediaType == .show, !store.seasonChosen {
                    seasonScreen
                } else if let detail = store.detail, !store.capturing {
                    WorkView(store: store, detail: detail)
                } else if let detail = store.detail {
                    CaptureForm(store: store, detail: detail)
                }
            }
            .padding(.top, 13)
            .padding(.bottom, 20)
        }
        .task { await store.refreshVocabulary() }
    }

    private var header: some View {
        HStack(spacing: 10) {
            if store.selected != nil {
                Button { store.back() } label: {
                    Text("‹").font(.system(size: 24)).foregroundStyle(Palette.muted).padding(.trailing, 6)
                }
                .buttonStyle(.plain)
            }
            Text(store.selected == nil ? "Add anything" : (store.detail?.title ?? "Loading…"))
                .font(Fonts.title(16))
                .foregroundStyle(Palette.ink)
                .lineLimit(1)
            Spacer()
        }
        .padding(.top, 2)
        .padding(.bottom, 12)
        .overlay(alignment: .bottom) { HairlineRule(color: Palette.line2) }
    }

    @ViewBuilder
    private var searchScreen: some View {
        HStack(spacing: 9) {
            Text("⌕").font(.system(size: 15, weight: .bold)).foregroundStyle(Palette.dim)
            TextField("Title or author, then Enter…", text: $store.query)
                .font(Fonts.display(14))
                .foregroundStyle(Palette.ink)
                .textInputAutocapitalization(.never)
                .autocorrectionDisabled()
                .submitLabel(.search)
                .onSubmit { Task { await store.runSearch() } }
        }
        .padding(.horizontal, 13)
        .padding(.vertical, 11)
        .background(Palette.well, in: RoundedRectangle(cornerRadius: 12))
        .overlay(RoundedRectangle(cornerRadius: 12).stroke(Palette.line, lineWidth: 1))
        .padding(.top, 8)

        SegmentedPills(options: MediaType.allCases, selection: Binding(
            get: { store.mediaType },
            set: { type in Task { await store.setType(type) } })) { lexicon.label($0) }
            .padding(.top, 11)

        if let saved = store.savedMessage {
            Notice(text: saved, kind: .good, trailing: store.savedItemId.map { id in
                AnyView(Button("Open it →") { openItem(id) }
                    .buttonStyle(.plain)
                    .font(Fonts.display(9.5, bold: true))
                    .tracking(1)
                    .textCase(.uppercase)
                    .underline()
                    .foregroundStyle(Palette.ac))
            })
            .padding(.top, 10)
        }

        if store.searching || store.searched {
            VStack(alignment: .leading, spacing: 0) {
                if let error = store.error {
                    Notice(text: error).padding(.bottom, 8)
                }
                if store.searching {
                    Aside("Searching…").padding(.vertical, 10)
                } else if !store.results.isEmpty {
                    Eyebrow("\(store.results.count) \(plural(store.results.count, "result"))", size: 9, tracking: 0.12, bold: false)
                        .padding(.vertical, 4)
                    ForEach(store.results, id: \.externalId) { result in
                        ResultRow(cover: result.imageUrl, type: result.mediaType, title: result.title,
                                  meta: lexicon.label(result.mediaType) + (result.releaseYear.map { " · \($0)" } ?? ""),
                                  action: "+ Add") {
                            Task { await store.select(result) }
                        }
                    }
                } else {
                    Aside("Nothing found for “\(store.query)”.").padding(.vertical, 10)
                }
            }
            .padding(.top, 14)
        }
    }

    @ViewBuilder
    private var seasonScreen: some View {
        Eyebrow("Choose a season", size: 9, tracking: 0.12, bold: false).padding(.vertical, 4)
        ForEach(store.seasons, id: \.seasonNumber) { season in
            ResultRow(cover: season.imageUrl, type: .show, title: season.name,
                      meta: AddStore.seasonMeta(season), action: "Pick") {
                store.pickSeason(season)
            }
        }
        Button("‹ Back to search") { store.reset() }
            .buttonStyle(.plain)
            .font(Fonts.display(11))
            .tracking(1.1)
            .textCase(.uppercase)
            .foregroundStyle(Palette.muted)
            .padding(.top, 14)
    }
}

// A search hit or a season: small cover, title, meta, an action pill.
private struct ResultRow: View {
    let cover: String?
    let type: MediaType
    let title: String
    let meta: String
    let action: String
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            HStack(spacing: 12) {
                // Search is text-only by design: the tile shows art only when the provider has it.
                CoverImage(url: cover, title: "", fallbackSize: 0)
                    .frame(width: 38, height: 57)
                    .clipShape(RoundedRectangle(cornerRadius: 6))
                    .overlay(RoundedRectangle(cornerRadius: 6).stroke(Palette.line, lineWidth: 1))
                VStack(alignment: .leading, spacing: 3) {
                    Text(title).font(Fonts.title(14.5)).foregroundStyle(Palette.ink).multilineTextAlignment(.leading)
                    Eyebrow(meta, size: 10, tracking: 0.06, bold: false)
                }
                Spacer(minLength: 8)
                Eyebrow(action, size: 9, color: Palette.muted, tracking: 0.1)
                    .padding(.horizontal, 11)
                    .padding(.vertical, 7)
                    .overlay(RoundedRectangle(cornerRadius: 8).stroke(Palette.line, lineWidth: 1))
            }
            .padding(.vertical, 11)
            .frame(maxWidth: .infinity, alignment: .leading)
            .overlay(alignment: .bottom) { HairlineRule(color: Palette.line2) }
        }
        .buttonStyle(.plain)
    }
}

// The picked work: cover leads, then a primary ADD →, then blurb and facts.
private struct WorkView: View {
    @Bindable var store: AddStore
    let detail: MediaItemDto
    @Environment(\.lexicon) private var lexicon

    var body: some View {
        HStack(alignment: .top, spacing: 14) {
            CoverTile(url: detail.imageUrl, title: detail.title, width: 104, radius: 10, fallbackPadding: 12, fallbackSize: 14)
                .overlay(alignment: .bottom) {
                    if detail.imageUrl == nil {
                        Eyebrow("No cover", size: 8.5, color: Palette.ac2, tracking: 0.12, bold: false)
                            .frame(maxWidth: .infinity)
                            .padding(.vertical, 6)
                            .background(Color.black.opacity(0.55))
                    }
                }

            VStack(alignment: .leading, spacing: 0) {
                Text(detail.title).font(Fonts.display(21, bold: true)).foregroundStyle(Palette.ink)
                Eyebrow([lexicon.label(detail.mediaType), detail.creator, detail.releaseYear.map(String.init)]
                            .compactMap { $0 }.joined(separator: " · "),
                        size: 10, color: Palette.muted, tracking: 0.1, bold: false)
                    .padding(.top, 6)

                Button("Add →") { store.capturing = true }
                    .buttonStyle(PrimaryButtonStyle(fullWidth: true))
                    .padding(.top, 14)

                if let description = detail.description, !description.trimmingCharacters(in: .whitespaces).isEmpty {
                    Blurb(text: description)
                }

                facts
            }
        }
        .padding(.top, 16)
        .overlay(alignment: .top) { Rectangle().fill(Palette.ink).frame(height: 2) }
    }

    @ViewBuilder
    private var facts: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(alignment: .top, spacing: 14) {
                if let length = detail.length {
                    fact(String(length), lexicon.unit(detail.mediaType))
                }
                if let rating = detail.externalRating {
                    fact(String(format: "%.1f", rating),
                         detail.externalSource + (detail.externalRatingCount.map { " · \($0)" } ?? ""))
                }
            }
            let genres = detail.genres.compactMap { $0 }
            if !genres.isEmpty {
                vocabFact(genres, all: $store.allGenres, label: "Genres per \(detail.externalSource)")
            }
            if !detail.tags.isEmpty {
                vocabFact(detail.tags, all: $store.allTags, label: "Tags per \(detail.externalSource)")
            }
        }
        .padding(.top, 16)
        .overlay(alignment: .top) { HairlineRule() }
    }

    private func fact(_ value: String, _ label: String) -> some View {
        VStack(alignment: .leading, spacing: 7) {
            Text(value).font(Fonts.title(20)).foregroundStyle(Palette.ac)
            Eyebrow(label, size: 9, tracking: 0.14, bold: false)
        }
        .padding(.top, 12)
        .frame(minWidth: 90, alignment: .leading)
    }

    // Providers can return dozens of keywords; show a handful, keep the rest a tap away.
    private func vocabFact(_ values: [String], all: Binding<Bool>, label: String) -> some View {
        VStack(alignment: .leading, spacing: 7) {
            HStack(alignment: .firstTextBaseline, spacing: 6) {
                Text((all.wrappedValue ? values : Array(values.prefix(AddStore.vocabPreview))).joined(separator: " · "))
                    .font(Fonts.display(12.5, bold: true))
                    .foregroundStyle(Palette.muted)
                    .fixedSize(horizontal: false, vertical: true)
                if values.count > AddStore.vocabPreview {
                    Button(all.wrappedValue ? "less" : "+\(values.count - AddStore.vocabPreview)") { all.wrappedValue.toggle() }
                        .buttonStyle(.plain)
                        .font(Fonts.display(9, bold: true))
                        .textCase(.uppercase)
                        .underline()
                        .foregroundStyle(Palette.ac)
                }
            }
            Eyebrow(label, size: 9, tracking: 0.14, bold: false)
        }
        .padding(.top, 12)
    }
}

// The form: the work's own metadata, and — behind a single link — the backfill pass.
private struct CaptureForm: View {
    @Bindable var store: AddStore
    let detail: MediaItemDto
    @Environment(\.lexicon) private var lexicon

    private var unit: String { lexicon.unit(detail.mediaType) }

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack(alignment: .firstTextBaseline, spacing: 12) {
                Eyebrow("Adding", size: 10.5, color: Palette.muted, tracking: 0.22)
                Text([detail.title, lexicon.label(detail.mediaType), detail.creator].compactMap { $0 }.joined(separator: " · "))
                    .font(Fonts.display(14, bold: true))
                    .foregroundStyle(Palette.ink)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.bottom, 12)
            .overlay(alignment: .bottom) { HairlineRule(height: 2) }

            labelled("Genres") {
                VocabularyPicker(selected: $store.genres, suggestions: store.vocab.genres, placeholder: "pick or create…")
            }
            labelled("Tags") {
                VocabularyPicker(selected: $store.tags, suggestions: store.vocab.tags, placeholder: "pick or create…")
            }

            if !store.newTagNames.isEmpty {
                newTags
            }

            HStack(alignment: .top, spacing: 22) {
                labelled("Universe") {
                    VocabularyPicker(selected: $store.universe, suggestions: store.vocab.universes, max: 1, placeholder: "e.g. Cosmere…")
                }
                labelled("Series") {
                    VocabularyPicker(selected: $store.series, suggestions: store.vocab.series, max: 1, placeholder: "e.g. Mistborn Era 1…")
                }
            }
            if !store.series.isEmpty {
                labelled("Position") { NumberField(value: $store.seriesPosition) }
                    .frame(width: 90)
            }

            HStack(alignment: .top, spacing: 22) {
                labelled("Discovery") {
                    MenuField(selection: $store.discovery, options: DiscoverySource.allCases) { lexicon.label($0) }
                }
                labelled("Release date") {
                    DateField(date: $store.releaseDate)
                    if let imported = detail.releaseDate, imported != store.releaseDate {
                        Button("revert to \(imported.year) from \(detail.externalSource)") { store.releaseDate = imported }
                            .buttonStyle(HintButtonStyle())
                            .padding(.top, 5)
                    }
                }
            }
            if detail.mediaType == .book {
                labelled("Audiobook length (hrs)", optional: true) {
                    DecimalField(value: $store.audioHours, placeholder: "e.g. 24.5")
                }
            }

            Button(store.backfill
                   ? "← never mind, just add it to the library"
                   : "\(AddStore.alreadyPrompt(detail.mediaType)) Log a finished pass →") {
                store.backfill.toggle()
            }
            .buttonStyle(HintButtonStyle())

            if store.backfill {
                backfillFields
            }

            if let error = store.error {
                Notice(text: error)
            }

            VStack(alignment: .trailing, spacing: 10) {
                if let hint = store.footHint {
                    Aside(hint, size: 11.5).frame(maxWidth: .infinity, alignment: .leading)
                }
                HStack(spacing: 14) {
                    Spacer()
                    Button("Cancel") { store.back() }.buttonStyle(GhostButtonStyle())
                    Button(store.saving ? "…" : store.backfill ? "Log it as finished" : "Add to library") {
                        Task { await store.submit() }
                    }
                    .buttonStyle(PrimaryButtonStyle())
                    .disabledLook(store.saving || !store.canSubmit)
                }
            }
            .padding(.top, 4)
        }
        .padding(.top, 14)
    }

    private var newTags: some View {
        VStack(alignment: .leading, spacing: 7) {
            HStack(spacing: 4) {
                FieldLabel("New \(plural(store.newTagNames.count, "tag")) — classify once")
                Eyebrow("*", size: 9.5, color: Palette.ac).padding(.bottom, 6)
            }
            ForEach(store.newTagNames, id: \.self) { tag in
                HStack(spacing: 8) {
                    Chip(text: tag)
                    MenuField(selection: Binding(get: { store.facet(of: tag) }, set: { store.setFacet($0, of: tag) }),
                              options: TagFacet.allCases, label: { $0.rawValue }, empty: "facet…")
                    MenuField(selection: Binding(get: { store.appliesTo(of: tag) }, set: { store.setAppliesTo($0, of: tag) }),
                              options: MediaType.allCases, label: { lexicon.label($0) + " only" }, empty: "All media")
                }
            }
        }
        .padding(.leading, 12)
        .overlay(alignment: .leading) { Rectangle().fill(Palette.ac).frame(width: 2) }
    }

    @ViewBuilder
    private var backfillFields: some View {
        HStack(alignment: .top, spacing: 12) {
            labelled("Start") { DateField(date: $store.startDate) }
            labelled("End") { DateField(date: $store.endDate) }
        }
        labelled("Context") {
            MenuField(selection: $store.context, options: lexicon.type(detail.mediaType).contexts) { lexicon.label($0) }
        }
        HStack(alignment: .top, spacing: 22) {
            if store.isAudiobook {
                labelled("Hours listened") {
                    DecimalField(value: $store.hoursListened, placeholder: store.audioHours.map(trimmed) ?? "")
                }
            } else {
                labelled("Effort (\(unit))") {
                    NumberField(value: $store.effort, placeholder: detail.length.map(String.init) ?? "")
                    if let suggested = detail.length, store.effort != suggested {
                        Button("use \(suggested) \(unit) from \(detail.externalSource)") { store.effort = suggested }
                            .buttonStyle(HintButtonStyle())
                            .padding(.top, 5)
                    }
                }
            }
            labelled("Rating") { StarRating(value: $store.rating, size: 18) }
        }
        labelled("Start note", optional: true) {
            TextArea(text: $store.startNote, placeholder: "What you expected going in…")
        }
        labelled("Finish note", optional: true) {
            TextArea(text: $store.note, placeholder: "How it landed…")
        }
    }

    private func labelled<Content: View>(_ label: String, optional: Bool = false,
                                         @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 0) {
            FieldLabel(label, optional: optional)
            content()
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

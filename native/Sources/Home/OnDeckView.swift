import SwiftUI

@MainActor
@Observable
final class OnDeckStore {
    enum Sort: String, CaseIterable, Hashable {
        case newest, oldest, shortest, title, year

        var label: String {
            switch self {
            case .newest: "Newest added"
            case .oldest: "Oldest added"
            case .shortest: "Shortest first"
            case .title: "Title"
            case .year: "Year"
            }
        }
    }

    var items: Loadable<[OnDeckItem]> = .loading
    var query = ""
    var type: MediaType?
    var genre: String?
    var sort: Sort = .newest

    func load() async {
        do { items = .loaded(try await api.onDeck()) } catch { items = .failed(error.localizedDescription) }
    }

    var rows: [OnDeckItem] {
        (items.value ?? []).filter { matches($0) && fits($0, type: type) && fits($0, genre: genre) }.sorted(by: ordered)
    }

    func count(_ type: MediaType) -> Int {
        (items.value ?? []).count { matches($0) && fits($0, type: type) && fits($0, genre: genre) }
    }

    func count(_ genre: String) -> Int {
        (items.value ?? []).count { matches($0) && fits($0, type: type) && fits($0, genre: genre) }
    }

    var types: [MediaType] {
        MediaType.allCases.filter { type in (items.value ?? []).contains { $0.mediaType == type } }
    }

    var genres: [String] {
        Set((items.value ?? []).filter { matches($0) && fits($0, type: type) }.flatMap(\.genres)).sorted()
    }

    private func matches(_ item: OnDeckItem) -> Bool {
        let text = query.trimmingCharacters(in: .whitespaces).lowercased()
        guard !text.isEmpty else { return true }
        return item.title.lowercased().contains(text) || (item.creator?.lowercased().contains(text) ?? false)
    }

    private func fits(_ item: OnDeckItem, type: MediaType?) -> Bool { type == nil || item.mediaType == type }

    private func fits(_ item: OnDeckItem, genre: String?) -> Bool { genre.map(item.genres.contains) ?? true }

    // Items without a length go last rather than first.
    private func ordered(_ a: OnDeckItem, _ b: OnDeckItem) -> Bool {
        switch sort {
        case .oldest: a.addedDate < b.addedDate
        case .newest: a.addedDate > b.addedDate
        case .shortest: (a.estimatedMinutes ?? .max, a.title) < (b.estimatedMinutes ?? .max, b.title)
        case .title: a.title.lowercased() < b.title.lowercased()
        case .year: (a.year ?? 0, a.title) > (b.year ?? 0, b.title)
        }
    }
}

struct OnDeckView: View {
    @State private var store = OnDeckStore()
    @Environment(\.lexicon) private var lexicon
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                Crumb("Now", trail: "On deck") { dismiss() }
                switch store.items {
                case .loading:
                    Aside("Loading…").frame(maxWidth: .infinity).padding(.vertical, 24)
                case .failed(let message):
                    Notice(text: message)
                case .loaded(let items) where items.isEmpty:
                    Aside("Nothing lined up yet.").frame(maxWidth: .infinity).padding(.vertical, 24)
                case .loaded(let items):
                    controls
                    SectionHead(store.sort.label, right: "\(store.rows.count) of \(items.count) lined up")
                    if store.rows.isEmpty {
                        Aside("Nothing lined up matches.").frame(maxWidth: .infinity).padding(.vertical, 24)
                    }
                    LazyVStack(alignment: .leading, spacing: 0) {
                        ForEach(store.rows, id: \.userMediaItemId) { item in
                            OnDeckRow(item: item)
                        }
                    }
                    .padding(.bottom, 8)
                }
            }
        }
        .page()
        .scrollDismissesKeyboard(.interactively)
        .task { await store.load() }
    }

    private var controls: some View {
        VStack(spacing: 8) {
            HStack(spacing: 8) {
                SearchField(text: $store.query, prompt: "Search on deck…") { store.query = "" }
                Menu {
                    Picker("Sort", selection: $store.sort) {
                        ForEach(OnDeckStore.Sort.allCases, id: \.self) { Text($0.label).tag($0) }
                    }
                } label: {
                    FrameChip { Text("⇅").font(.system(size: 13)).foregroundStyle(Palette.muted) }
                }
            }
            HStack(spacing: 8) {
                FilterMenu(all: "All types", selection: $store.type, options: store.types,
                           label: { "\(lexicon.label($0))s" }, count: store.count)
                FilterMenu(all: "All genres", selection: $store.genre, options: store.genres,
                           label: { $0 }, count: store.count)
            }
        }
        .padding(.top, 4)
        .onChange(of: store.type) { if let genre = store.genre, !store.genres.contains(genre) { store.genre = nil } }
    }
}

private struct OnDeckRow: View {
    let item: OnDeckItem
    @Environment(\.lexicon) private var lexicon

    var body: some View {
        Button { openItem(item.userMediaItemId) } label: {
            HStack(alignment: .top, spacing: 12) {
                CoverTile(url: item.imageUrl, title: item.title, width: 46, radius: 6, fallbackPadding: 4, fallbackSize: 7)
                VStack(alignment: .leading, spacing: 0) {
                    Text(item.title)
                        .font(Fonts.title(15))
                        .foregroundStyle(Palette.ink)
                        .multilineTextAlignment(.leading)
                        .lineLimit(2)
                    Eyebrow(byline, size: 9, tracking: 0.1, bold: false).padding(.top, 5)
                    Aside(detail, size: 12, color: Palette.muted).padding(.top, 5)
                }
                Spacer(minLength: 0)
            }
            .padding(.vertical, 11)
            .frame(maxWidth: .infinity, alignment: .leading)
            .overlay(alignment: .bottom) { HairlineRule() }
        }
        .buttonStyle(.plain)
    }

    private var byline: String {
        [item.creator, lexicon.label(item.mediaType), item.year.map(String.init)].compactMap { $0 }.joined(separator: " · ")
    }

    private var detail: String {
        var parts: [String] = []
        if let length = item.length { parts.append(lengthText(length)) }
        parts.append("added " + age(DateOnly.today.dayNumber - item.addedDate.dayNumber))
        if let discovery = item.discovery { parts.append(lexicon.label(discovery).lowercased()) }
        return parts.joined(separator: " · ")
    }

    private func lengthText(_ length: Int) -> String {
        if item.mediaType == .movie, length >= 60 { return "\(length / 60) h \(length % 60) min" }
        return "\(length) \(lexicon.unit(item.mediaType))"
    }
}

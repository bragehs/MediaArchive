import SwiftUI

// The wall: every work you have been through, as posters. The genre map is a
// screen behind it rather than the tab itself.
struct LibraryView: View {
    @Bindable private var store = LibraryStore.shared
    @Environment(\.lexicon) private var lexicon

    private static let types: [LibraryStore.TypeFilter] =
        [.all, .only(.book), .only(.game), .only(.movie), .only(.show)]

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                controls

                if let error = store.error {
                    Notice(text: error).padding(.top, 12)
                } else if !store.loaded {
                    Aside("Loading…").frame(maxWidth: .infinity).padding(.vertical, 30)
                } else {
                    wall
                }
            }
        }
        .page()
        .scrollDismissesKeyboard(.interactively)
        .task { await store.load() }
        .task(id: store.query) {
            try? await Task.sleep(for: .milliseconds(170))
            await store.search()
        }
    }

    private var controls: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 8) {
                SearchField(text: $store.query, clear: store.clearSearch)
                sortMenu
                Button { Router.shared.push(.constellation) } label: {
                    FrameChip { Eyebrow("✦ Map", size: 10, color: Palette.ac, tracking: 0.1) }
                }
                .buttonStyle(.plain)
            }

            SegmentedPills(options: Self.types, selection: $store.type) { pill($0) }

            if store.genre != nil || !store.genreMatches.isEmpty {
                FlowLayout(spacing: 7) {
                    if let genre = store.genre {
                        Chip(text: genre) { store.genre = nil }
                    }
                    ForEach(store.genreMatches.filter { $0 != store.genre }, id: \.self) { genre in
                        Button { store.filter(byGenre: genre) } label: {
                            HStack(spacing: 6) {
                                Eyebrow(genre, size: 9.5, color: Palette.muted, tracking: 0.08)
                                Text("→").font(.system(size: 10)).foregroundStyle(Palette.dim)
                            }
                            .padding(.horizontal, 9)
                            .padding(.vertical, 5)
                            .overlay(Capsule().stroke(Palette.line, lineWidth: 1))
                        }
                        .buttonStyle(.plain)
                    }
                }
            }
        }
        .padding(.top, 4)
    }

    private var sortMenu: some View {
        Menu {
            Picker("Sort", selection: $store.sort) {
                ForEach(LibraryStore.Sort.allCases, id: \.self) { Text($0.label).tag($0) }
            }
            Toggle("Hide dropped", isOn: $store.hideDropped)
        } label: {
            FrameChip { Text("⇅").font(.system(size: 13)).foregroundStyle(Palette.muted) }
        }
    }

    @ViewBuilder
    private var wall: some View {
        let rows = store.rows
        if rows.isEmpty {
            Aside(store.hits == nil ? "Nothing finished or dropped yet." : "No matches in the archive.")
                .frame(maxWidth: .infinity)
                .padding(.vertical, 30)
        } else if let groups = store.groups(of: rows) {
            ForEach(groups, id: \.year) { group in
                SectionHead("\(group.year)", right: works(group.items.count))
                grid(group.items)
            }
        } else {
            SectionHead(store.sort.label, right: works(rows.count))
            grid(rows)
        }
    }

    private func grid(_ items: [LibraryItem]) -> some View {
        LazyVGrid(columns: Array(repeating: GridItem(.flexible(), spacing: 9), count: 4),
                  alignment: .leading, spacing: 12) {
            ForEach(items, id: \.userMediaItemId) { item in
                Button { openItem(item.userMediaItemId) } label: { tile(item) }
                    .buttonStyle(.plain)
            }
        }
        .padding(.bottom, 8)
    }

    private func tile(_ item: LibraryItem) -> some View {
        VStack(alignment: .leading, spacing: 5) {
            CoverTile(url: item.imageUrl, title: item.title, radius: 6, fallbackPadding: 4, fallbackSize: 7)
                .opacity(item.status == .completed ? 1 : 0.45)
                .overlay(alignment: .topTrailing) { badge(item) }
                .overlay(alignment: .bottomTrailing) {
                    if item.status != .completed { StatusGlyph(status: item.status).padding(2) }
                }
            Text(item.title)
                .font(Fonts.title(9.5))
                .foregroundStyle(Palette.ink)
                .multilineTextAlignment(.leading)
                .lineLimit(2)
        }
    }

    @ViewBuilder
    private func badge(_ item: LibraryItem) -> some View {
        let marks = [item.isFavorite ? "♥" : nil, item.rating.map { "★ \(stars(Double($0)))" }]
        let text = marks.compactMap { $0 }.joined(separator: " ")
        if !text.isEmpty {
            Text(text)
                .font(Fonts.display(8, bold: true))
                .foregroundStyle(Palette.ac2)
                .padding(.horizontal, 4)
                .padding(.vertical, 2)
                .background(Color.black.opacity(0.7), in: RoundedRectangle(cornerRadius: 4))
                .padding(3)
        }
    }

    private func pill(_ filter: LibraryStore.TypeFilter) -> String {
        let count = store.count(filter)
        guard case .only(let type) = filter else { return "All \(count)" }
        return "\(lexicon.label(type))s \(count)"
    }

    private func works(_ count: Int) -> String { "\(count) \(plural(count, "work"))" }
}

// The framed control the header's buttons share.
struct FrameChip<Content: View>: View {
    @ViewBuilder let content: Content

    var body: some View {
        content
            .frame(height: 38)
            .padding(.horizontal, 12)
            .overlay(RoundedRectangle(cornerRadius: 10).stroke(Palette.line, lineWidth: 1))
    }
}

struct SearchField: View {
    @Binding var text: String
    let clear: () -> Void

    @FocusState private var focused: Bool

    var body: some View {
        HStack(spacing: 9) {
            Text("⌕").font(.system(size: 14, weight: .bold)).foregroundStyle(Palette.dim)
            TextField("Search the archive…", text: $text)
                .font(Fonts.display(13))
                .foregroundStyle(Palette.ink)
                .textInputAutocapitalization(.never)
                .autocorrectionDisabled()
                .focused($focused)
            if !text.isEmpty {
                Button { clear(); focused = true } label: {
                    Text("✕").font(.system(size: 13)).foregroundStyle(Palette.dim)
                }
                .buttonStyle(.plain)
            }
        }
        .padding(.horizontal, 12)
        .frame(height: 38)
        .overlay(RoundedRectangle(cornerRadius: 10).stroke(Palette.line, lineWidth: 1))
    }
}

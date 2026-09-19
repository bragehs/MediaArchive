import SwiftUI

@MainActor
@Observable
final class ProfileStore {
    var snapshot: Loadable<ProfileSnapshot> = .loading
    var recordsTab: MediaType = .book
    var allTime = false

    func load() async {
        if snapshot.value == nil { snapshot = .loading }
        do {
            snapshot = .loaded(try await api.profile())
        } catch {
            snapshot = .failed(error.localizedDescription)
        }
    }

    var currentPanel: TypePanel? {
        guard let panels = snapshot.value?.panels else { return nil }
        return panels.first { $0.mediaType == recordsTab } ?? panels.first
    }
}

// The taste dashboard: time spent across every medium, the hall of fame, a way
// into universes and creators, and one records pane per medium.
struct ProfileView: View {
    @State private var store = ProfileStore()

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                switch store.snapshot {
                case .loading:
                    Aside("Loading…").padding(.vertical, 10)
                case .failed(let message):
                    Notice(text: message)
                case .loaded(let snapshot) where snapshot.itemsLogged == 0:
                    Aside("Nothing logged yet — the profile fills in as you add things.").padding(.vertical, 10)
                case .loaded(let snapshot):
                    content(snapshot)
                }
            }
        }
        .page()
        .task { await store.load() }
    }

    @ViewBuilder
    private func content(_ snapshot: ProfileSnapshot) -> some View {
        TimeSpentHero(snapshot: snapshot)

        if !snapshot.hallOfFame.isEmpty {
            SectionHead("Hall of fame")
            LazyVGrid(columns: Array(repeating: GridItem(.flexible(), spacing: 9), count: 4), spacing: 9) {
                ForEach(snapshot.hallOfFame, id: \.userMediaItemId) { fame in
                    Button { openItem(fame.userMediaItemId) } label: {
                        CoverTile(url: fame.imageUrl, title: fame.title, radius: 7)
                            .overlay(alignment: .bottomTrailing) {
                                Text(fame.isFavorite ? "♥" : "★ \(stars(Double(fame.rating)))")
                                    .font(Fonts.display(9, bold: true))
                                    .foregroundStyle(Palette.ac2)
                                    .padding(.horizontal, 5)
                                    .padding(.vertical, 2)
                                    .background(Color.black.opacity(0.82), in: RoundedRectangle(cornerRadius: 5))
                                    .padding(4)
                            }
                    }
                    .buttonStyle(.plain)
                }
            }
        }

        if !snapshot.universes.isEmpty || !snapshot.canon.isEmpty {
            VStack(spacing: 0) {
                if !snapshot.universes.isEmpty {
                    PortalRow(kick: "Universes",
                              detail: "\(snapshot.universes.count) · \(snapshot.universes.reduce(0) { $0 + $1.works }) works",
                              covers: snapshot.universes.compactMap(\.covers.first).prefix(3).map(\.imageUrl)) {
                        Router.shared.push(.universes)
                    }
                    if !snapshot.canon.isEmpty { HairlineRule(color: Palette.line2) }
                }
                if !snapshot.canon.isEmpty {
                    PortalRow(kick: "Creators",
                              detail: "\(snapshot.canon.count) · " + snapshot.canon.prefix(2).map(\.name).joined(separator: ", ") + (snapshot.canon.count > 2 ? "…" : ""),
                              covers: []) {
                        Router.shared.push(.creators)
                    }
                }
            }
            .padding(.top, 22)
            .overlay(alignment: .top) { HairlineRule(color: Palette.line2) }
        }

        SectionHead("Records")
        if !snapshot.panels.isEmpty {
            RecordsPane(snapshot: snapshot, store: store)
        }
    }
}

// A whole section collapsed to one line: what it holds, a peek, and a way in.
private struct PortalRow: View {
    let kick: String
    let detail: String
    let covers: [String?]
    let open: () -> Void

    var body: some View {
        Button(action: open) {
            HStack(spacing: 10) {
                Eyebrow(kick, size: 9.5, color: Palette.muted, tracking: 0.18)
                Aside(detail, size: 11.5, color: Palette.dim).lineLimit(1)
                Spacer(minLength: 6)
                HStack(spacing: -6) {
                    ForEach(Array(covers.enumerated()), id: \.offset) { _, url in
                        CoverImage(url: url, title: "", fallbackPadding: 2, fallbackSize: 5)
                            .frame(width: 16, height: 24)
                            .clipShape(RoundedRectangle(cornerRadius: 3))
                            .overlay(RoundedRectangle(cornerRadius: 3).stroke(Palette.line2, lineWidth: 1))
                    }
                }
                Text("›").font(Fonts.display(15)).foregroundStyle(Palette.dim)
            }
            .padding(.vertical, 13)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }
}

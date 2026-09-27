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
            HallOfFame(items: snapshot.hallOfFame)
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

// The best of everything logged, gilded: marigold is the app's gold.
private struct HallOfFame: View {
    let items: [FameItem]

    private var gold: LinearGradient {
        LinearGradient(colors: [Palette.ac2, Palette.ac2.opacity(0.35)], startPoint: .topLeading, endPoint: .bottomTrailing)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(alignment: .firstTextBaseline) {
                Eyebrow("✦ Hall of fame", size: 10.5, color: Palette.ac2, tracking: 0.2)
                Spacer()
                Eyebrow("\(items.count) \(plural(items.count, "work"))", size: 9, color: Palette.ac2.opacity(0.7), tracking: 0.1, bold: false)
            }
            .padding(.bottom, 12)

            LazyVGrid(columns: Array(repeating: GridItem(.flexible(), spacing: 12), count: 3), spacing: 14) {
                ForEach(items, id: \.userMediaItemId) { fame in
                    Button { openItem(fame.userMediaItemId) } label: {
                        CoverTile(url: fame.imageUrl, title: fame.title, radius: 8)
                            .overlay(RoundedRectangle(cornerRadius: 8).strokeBorder(gold, lineWidth: 1.5))
                            .shadow(color: Palette.ac2.opacity(0.28), radius: 12, y: 4)
                            .overlay(alignment: .bottomLeading) {
                                Text(fame.isFavorite ? "♥" : "★ \(stars(Double(fame.rating)))")
                                    .font(Fonts.display(9.5, bold: true))
                                    .foregroundStyle(Palette.onAc)
                                    .padding(.horizontal, 7)
                                    .padding(.vertical, 3)
                                    .background(Palette.ac2, in: Capsule())
                                    .padding(6)
                            }
                    }
                    .buttonStyle(.plain)
                }
            }
        }
        .padding(14)
        .background(
            LinearGradient(colors: [Palette.ac2.opacity(0.14), Palette.ac2.opacity(0.03)], startPoint: .top, endPoint: .bottom),
            in: RoundedRectangle(cornerRadius: 14))
        .overlay(RoundedRectangle(cornerRadius: 14).stroke(Palette.ac2.opacity(0.35), lineWidth: 1))
        .padding(.top, 24)
    }
}

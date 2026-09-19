import SwiftUI

// Every world you hold a piece of, with what it cost you. Reads the profile
// snapshot rather than a route of its own — the aggregate is already page-shaped
// and the archive is a few hundred rows.
struct UniversesView: View {
    @State private var snapshot: Loadable<ProfileSnapshot> = .loading
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                Crumb("Profile", trail: "Universes") { dismiss() }

                switch snapshot {
                case .loading:
                    Aside("Loading…").padding(.vertical, 10)
                case .failed(let message):
                    Notice(text: message)
                case .loaded(let snapshot) where snapshot.universes.isEmpty:
                    Aside("Nothing in the archive belongs to a universe yet.").padding(.vertical, 10)
                case .loaded(let snapshot):
                    ForEach(snapshot.universes, id: \.name) { universe in
                        UniverseSection(universe: universe)
                    }
                }
            }
        }
        .page()
        .task {
            do { snapshot = .loaded(try await api.profile()) }
            catch { snapshot = .failed(error.localizedDescription) }
        }
    }
}

struct UniverseSection: View {
    let universe: UniverseCard

    var body: some View {
        SectionHead(universe.name,
                    right: "\(universe.works) \(plural(universe.works, "work"))"
                        + (universe.avgRating.map { " · ★ \(stars($0))" } ?? ""))

        if !universe.effort.isEmpty {
            Aside(universe.effort.map { "\(trimmed($0.value)) \($0.unit)" }.joined(separator: " · "),
                  size: 12.5, color: Palette.muted)
                .padding(.top, -2)
                .padding(.bottom, 12)
        }

        FlowLayout(spacing: 8) {
            ForEach(universe.covers, id: \.userMediaItemId) { cover in
                Button { Router.shared.push(.item(cover.userMediaItemId)) } label: {
                    CoverImage(url: cover.imageUrl, title: cover.title, fallbackPadding: 4, fallbackSize: 7)
                        .frame(width: 48, height: 72)
                        .opacity(cover.status == .interested ? 0.45 : 1)
                        .clipShape(RoundedRectangle(cornerRadius: 6))
                        .overlay(RoundedRectangle(cornerRadius: 6).stroke(Palette.line2, lineWidth: 1))
                        .overlay(alignment: .bottomTrailing) { StatusGlyph(status: cover.status).padding(2) }
                }
                .buttonStyle(.plain)
            }
        }
    }
}

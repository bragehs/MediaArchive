import SwiftUI

// The canon, ranked by how much of them you've taken in. Each medium counts only
// its primary credit, so one film trilogy can't flood the list with writers.
struct CreatorsView: View {
    @State private var snapshot: Loadable<ProfileSnapshot> = .loading
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                Crumb("Profile", trail: "Creators") { dismiss() }

                switch snapshot {
                case .loading:
                    Aside("Loading…").padding(.vertical, 10)
                case .failed(let message):
                    Notice(text: message)
                case .loaded(let snapshot) where snapshot.canon.isEmpty:
                    Aside("Nothing credited yet.").padding(.vertical, 10)
                case .loaded(let snapshot):
                    let peak = snapshot.canon.map(\.works).max() ?? 1
                    ForEach(Array(snapshot.canon.enumerated()), id: \.element.name) { index, creator in
                        CreatorRow(rank: index + 1, creator: creator, peak: peak)
                            .overlay(alignment: .bottom) {
                                if index < snapshot.canon.count - 1 { HairlineRule(color: Palette.line2) }
                            }
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

private struct CreatorRow: View {
    let rank: Int
    let creator: CreatorLine
    let peak: Int

    @Environment(\.lexicon) private var lexicon

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: 10) {
            Text("\(rank)")
                .font(Fonts.display(10))
                .foregroundStyle(Palette.dim)
                .frame(width: 16, alignment: .trailing)

            VStack(alignment: .leading, spacing: 5) {
                Text(creator.name).font(Fonts.title(13.5)).foregroundStyle(Palette.ink).lineLimit(1)
                HStack(spacing: 7) {
                    // Width against the most-read creator, so the list has a shape
                    // before you read a single number.
                    Capsule()
                        .fill(Palette.ac.opacity(0.75))
                        .frame(width: max(3, 54 * CGFloat(creator.works) / CGFloat(max(1, peak))), height: 4)
                    Text(creator.types.map { lexicon.label($0).lowercased() + plural(creator.works, "") }
                            .joined(separator: " · "))
                        .font(Fonts.serif(11.5, italic: true))
                        .foregroundStyle(Palette.dim)
                        .lineLimit(1)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)

            Text("\(creator.works)").font(Fonts.title(14)).foregroundStyle(Palette.ink)

            if let rating = creator.avgRating {
                Text("★ \(stars(rating))").font(Fonts.display(11, bold: true)).foregroundStyle(Palette.ac2)
            }
        }
        .padding(.vertical, 10)
    }
}

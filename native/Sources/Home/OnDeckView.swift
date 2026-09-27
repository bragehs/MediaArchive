import SwiftUI

// Everything lined up, as a grid; Now shows one rail of it.
struct OnDeckView: View {
    @State private var cards: Loadable<[CoverCard]> = .loading
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                Crumb("Now", trail: "On deck") { dismiss() }
                SectionHead("On deck", right: cards.value.map { "\($0.count) lined up" })
                switch cards {
                case .loading:
                    Aside("Loading…").frame(maxWidth: .infinity).padding(.vertical, 24)
                case .failed(let message):
                    Notice(text: message)
                case .loaded(let cards) where cards.isEmpty:
                    Aside("Nothing lined up yet.").frame(maxWidth: .infinity).padding(.vertical, 24)
                case .loaded(let cards):
                    LazyVGrid(columns: Array(repeating: GridItem(.flexible(), spacing: 12), count: 3), alignment: .leading, spacing: 14) {
                        ForEach(cards) { card in
                            Button { openItem(card.userMediaItemId) } label: {
                                VStack(alignment: .leading, spacing: 7) {
                                    CoverTile(url: card.imageUrl, title: card.title)
                                    Text(card.title)
                                        .font(Fonts.title(12))
                                        .foregroundStyle(Palette.ink)
                                        .multilineTextAlignment(.leading)
                                        .lineLimit(2)
                                }
                            }
                            .buttonStyle(.plain)
                        }
                    }
                    .padding(.top, 10)
                    .padding(.bottom, 8)
                }
            }
        }
        .page()
        .task {
            do { cards = .loaded(try await api.backlog()) } catch { cards = .failed(error.localizedDescription) }
        }
    }
}

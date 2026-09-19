import SwiftUI

// Two jobs that are both "what next?": find something new, or pick from the backlog.
struct ExploreView: View {
    private enum Mode: Hashable { case search, upNext }

    @State private var mode: Mode = .search
    @State private var add = AddStore()
    @State private var upNext: Loadable<[CoverCard]>?

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                SegmentedPills(options: [.search, .upNext], selection: $mode) { $0 == .search ? "Search" : "Up next" }
                    .padding(.top, 4)
                    .padding(.bottom, 14)

                switch mode {
                case .search:
                    AddFlowView(store: add)
                case .upNext:
                    backlog
                }
            }
        }
        .page()
        .scrollDismissesKeyboard(.interactively)
        .onChange(of: mode) { if mode == .upNext { Task { await loadUpNext() } } }
    }

    @ViewBuilder
    private var backlog: some View {
        switch upNext {
        case nil, .loading:
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
            .padding(.bottom, 8)
        }
    }

    private func loadUpNext() async {
        if upNext?.value == nil { upNext = .loading }
        do {
            upNext = .loaded(try await api.backlog())
        } catch {
            upNext = .failed(error.localizedDescription)
        }
    }
}

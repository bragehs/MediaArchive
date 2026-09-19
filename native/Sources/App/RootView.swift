import SwiftUI

// Boots the lexicon, then hands over to the shell. Until then, the brand on the ground.
struct RootView: View {
    @State private var lexicon: Loadable<Lexicon> = .loading

    var body: some View {
        ZStack {
            Palette.bg.ignoresSafeArea()
            switch lexicon {
            case .loading:
                Brand(size: 23)
            case .failed(let message):
                VStack(spacing: 12) {
                    Brand(size: 23)
                    Aside(message, color: Palette.badInk)
                    Button("Retry") { Task { await boot() } }
                        .buttonStyle(GhostButtonStyle())
                }
                .padding(24)
            case .loaded(let lexicon):
                Shell()
                    .environment(\.lexicon, lexicon)
            }
        }
        .task { await boot() }
    }

    private func boot() async {
        lexicon = .loading
        do {
            lexicon = .loaded(try await api.lexicon())
        } catch {
            lexicon = .failed(error.localizedDescription)
        }
    }
}

private struct LexiconKey: EnvironmentKey {
    static let defaultValue = Lexicon(types: [], statuses: [], contexts: [], discovery: [], kinds: [], facets: [])
}

extension EnvironmentValues {
    var lexicon: Lexicon {
        get { self[LexiconKey.self] }
        set { self[LexiconKey.self] = newValue }
    }
}

import SwiftUI

struct SearchView: View {
    @State private var add = AddStore()
    @FocusState private var focused: Bool
    @Environment(\.lexicon) private var lexicon

    private var router: Router { Router.shared }

    private var scope: Binding<MediaType> {
        Binding(get: { add.mediaType }, set: { type in Task { await add.setType(type) } })
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                // Picked here because the system's search scopes hang off the navigation bar, which the shell hides.
                if add.selected == nil {
                    SegmentedPills(options: MediaType.allCases, selection: scope) { lexicon.label($0) }
                        .padding(.top, 4)
                        .padding(.bottom, 6)
                }
                if add.searching || add.searched || add.selected != nil {
                    AddFlowView(store: add)
                } else {
                    Aside("A title or an author, then Return.")
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 24)
                }
            }
        }
        .page()
        .scrollDismissesKeyboard(.interactively)
        .searchable(text: $add.query, prompt: "Title or author")
        .searchFocused($focused)
        .onSubmit(of: .search) { Task { await add.runSearch() } }
        .onChange(of: add.query) { if add.query.isEmpty { add.clearResults() } }
        .onAppear { focused = true }
        .onChange(of: router.selected) { if router.selected == .search { focused = true } }
    }
}

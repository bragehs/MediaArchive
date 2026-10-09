import SwiftUI

struct ExploreView: View {
    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                SectionHead("Explore")
                Aside("Nothing lives here yet.").padding(.vertical, 10)
            }
        }
        .page()
    }
}

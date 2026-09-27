import SwiftUI

// Reserved for discovery once there is something to discover; search is its own tab.
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

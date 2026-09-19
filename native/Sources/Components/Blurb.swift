import SwiftUI

// A description clamped to three lines, with "show more" only when there is more.
struct Blurb: View {
    let text: String

    @State private var open = false
    @State private var clampedHeight: CGFloat = 0
    @State private var fullHeight: CGFloat = 0

    private var overflows: Bool { fullHeight > clampedHeight + 1 }

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            prose
                .lineLimit(open ? nil : 3)
                .onGeometryChange(for: CGFloat.self, of: { $0.size.height }) { clampedHeight = $0 }
                .background {
                    prose
                        .fixedSize(horizontal: false, vertical: true)
                        .hidden()
                        .onGeometryChange(for: CGFloat.self, of: { $0.size.height }) { fullHeight = $0 }
                }

            if overflows || open {
                Button(open ? "show less" : "show more") { open.toggle() }
                    .buttonStyle(.plain)
                    .font(Fonts.display(9, bold: true))
                    .tracking(0.9)
                    .textCase(.uppercase)
                    .underline()
                    .foregroundStyle(Palette.ac)
            }
        }
        .padding(.top, 14)
        .onChange(of: text) { open = false }
    }

    private var prose: some View {
        Text(text)
            .font(Fonts.serif(13.5))
            .lineSpacing(3)
            .foregroundStyle(Palette.muted)
            .frame(maxWidth: .infinity, alignment: .leading)
    }
}

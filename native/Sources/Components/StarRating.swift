import SwiftUI

// Half-star scale, 0–10 with 10 = five stars. An accent layer clipped over a
// dim one, ten invisible hit zones on top — one per half step.
struct StarRating: View {
    @Binding var value: Int
    var size: CGFloat = 20
    var fill: Color = Palette.ac2
    var clearLabel: String? = nil

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            ZStack(alignment: .leading) {
                stars.foregroundStyle(Palette.line)
                stars
                    .foregroundStyle(fill)
                    .mask(alignment: .leading) {
                        GeometryReader { geometry in
                            Rectangle()
                                .frame(width: geometry.size.width * CGFloat(value) / 10)
                                .frame(maxWidth: .infinity, alignment: .leading)
                        }
                    }
                HStack(spacing: 0) {
                    ForEach(1...10, id: \.self) { half in
                        Color.clear
                            .contentShape(Rectangle())
                            .onTapGesture { value = half }
                    }
                }
            }
            .fixedSize()

            if value > 0 {
                Button(clearLabel ?? "clear") { value = 0 }
                    .buttonStyle(.plain)
                    .font(Fonts.display(10))
                    .foregroundStyle(Palette.dim)
            }
        }
    }

    private var stars: Text {
        Text("★★★★★")
            .font(.system(size: size))
            .tracking(size * 0.15)
    }
}

// Read-only stars with a fractional fill, for lists.
struct StarsInline: View {
    let tenScale: Int
    var size: CGFloat = 12

    var body: some View {
        ZStack(alignment: .leading) {
            stars.foregroundStyle(Palette.line)
            stars
                .foregroundStyle(Palette.ac2)
                .mask(alignment: .leading) {
                    GeometryReader { geometry in
                        Rectangle()
                            .frame(width: geometry.size.width * CGFloat(tenScale) / 10)
                            .frame(maxWidth: .infinity, alignment: .leading)
                    }
                }
        }
        .fixedSize()
    }

    private var stars: Text {
        Text("★★★★★").font(.system(size: size)).tracking(1)
    }
}

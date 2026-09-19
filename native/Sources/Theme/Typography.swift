import SwiftUI

// Cinzel Decorative for titles, Cinzel for display and body, EB Garamond for
// running text — the four files ship in the app bundle (UIAppFonts).
enum Fonts {
    static func title(_ size: CGFloat) -> Font {
        .custom("CinzelDecorative-Bold", size: size)
    }

    static func display(_ size: CGFloat, bold: Bool = false) -> Font {
        .custom(bold ? "CinzelRoman-Bold" : "Cinzel-Regular", size: size)
    }

    static func serif(_ size: CGFloat, italic: Bool = false, bold: Bool = false) -> Font {
        switch (italic, bold) {
        case (true, true): .custom("EBGaramond-SemiBoldItalic", size: size)
        case (true, false): .custom("EBGaramond-Italic", size: size)
        case (false, true): .custom("EBGaramond-SemiBold", size: size)
        case (false, false): .custom("EBGaramond-Regular", size: size)
        }
    }
}

// Uppercase display text with wide tracking: the `.label` / `.fl` / `.mk` family.
struct Eyebrow: View {
    let text: String
    var size: CGFloat = 9.5
    var color: Color = Palette.dim
    var tracking: CGFloat = 0.12
    var bold: Bool = true

    init(_ text: String, size: CGFloat = 9.5, color: Color = Palette.dim,
         tracking: CGFloat = 0.12, bold: Bool = true) {
        self.text = text
        self.size = size
        self.color = color
        self.tracking = tracking
        self.bold = bold
    }

    var body: some View {
        Text(text.uppercased())
            .font(Fonts.display(size, bold: bold))
            .tracking(size * tracking)
            .foregroundStyle(color)
    }
}

// The italic serif aside used for loading, empty and footnote copy.
struct Aside: View {
    let text: String
    var size: CGFloat = 13
    var color: Color = Palette.dim

    init(_ text: String, size: CGFloat = 13, color: Color = Palette.dim) {
        self.text = text
        self.size = size
        self.color = color
    }

    var body: some View {
        Text(text)
            .font(Fonts.serif(size, italic: true))
            .foregroundStyle(color)
    }
}

// The `.msec` / `.isec` / `.psec` rule: kicker left, note right, hairline under.
struct SectionHead: View {
    let kick: String
    var right: String? = nil
    var rightColor: Color = Palette.dim

    init(_ kick: String, right: String? = nil, rightColor: Color = Palette.dim) {
        self.kick = kick
        self.right = right
        self.rightColor = rightColor
    }

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: 10) {
            Eyebrow(kick, size: 9.5, color: Palette.muted, tracking: 0.18)
            Spacer()
            if let right {
                Eyebrow(right, size: 9, color: rightColor, tracking: 0.1, bold: false)
            }
        }
        .padding(.top, 24)
        .padding(.bottom, 8)
        .overlay(alignment: .bottom) { Rectangle().fill(Palette.line).frame(height: 2) }
        .padding(.bottom, 12)
    }
}

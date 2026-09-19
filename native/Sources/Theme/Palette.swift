import SwiftUI

enum Palette {
    // colors:start — generated from colors.json by scripts/sync-colors.sh
    static let bg = Color(red: 0x14 / 255, green: 0x14 / 255, blue: 0x14 / 255)
    static let panel = Color(red: 0x25 / 255, green: 0x3b / 255, blue: 0x23 / 255)
    static let panel2 = Color(red: 0x2e / 255, green: 0x4a / 255, blue: 0x2b / 255)
    static let sink = Color(red: 0x13 / 255, green: 0x1f / 255, blue: 0x10 / 255)
    static let ink = Color(red: 0xfa / 255, green: 0xfc / 255, blue: 0xf8 / 255)
    static let muted = Color(red: 0xcf / 255, green: 0xd6 / 255, blue: 0xca / 255)
    static let dim = Color(red: 0x95 / 255, green: 0x9f / 255, blue: 0x90 / 255)
    static let onAc = Color(red: 0x13 / 255, green: 0x20 / 255, blue: 0x11 / 255)
    static let ac = Color(red: 0x8b / 255, green: 0xbe / 255, blue: 0x5a / 255)
    static let ac2 = Color(red: 0xd6 / 255, green: 0xa7 / 255, blue: 0x4a / 255)
    static let sage = Color(red: 0xa9 / 255, green: 0xc6 / 255, blue: 0x8e / 255)
    static let bad = Color(red: 0xbe / 255, green: 0x5a / 255, blue: 0x5a / 255)
    static let badInk = Color(red: 0xe8 / 255, green: 0xa0 / 255, blue: 0xa0 / 255)
    static let book = Color(red: 0x8b / 255, green: 0xbe / 255, blue: 0x5a / 255)
    static let game = Color(red: 0xd6 / 255, green: 0xa7 / 255, blue: 0x4a / 255)
    static let movie = Color(red: 0xa9 / 255, green: 0xc6 / 255, blue: 0x8e / 255)
    static let show = Color(red: 0x8a / 255, green: 0x7f / 255, blue: 0x6a / 255)
    // colors:end

    // hairlines are ink at low alpha, not palette entries
    static let line = Color(red: 242 / 255, green: 239 / 255, blue: 230 / 255).opacity(0.18)
    static let line2 = Color(red: 242 / 255, green: 239 / 255, blue: 230 / 255).opacity(0.09)
    static let well = Color.black.opacity(0.22)

    static func accent(_ type: MediaType) -> Color {
        switch type {
        case .book: book
        case .game: game
        case .movie: movie
        case .show: show
        }
    }

    // The cover tile behind missing or loading art.
    static let coverFallback = LinearGradient(
        colors: [panel, sink], startPoint: .topLeading, endPoint: .bottomTrailing)
}

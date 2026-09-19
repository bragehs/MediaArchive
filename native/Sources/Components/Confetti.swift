import SwiftUI

// A one-shot burst over whatever presents it; removes itself when the last
// piece has faded. Ported from confetti.js.
struct ConfettiOverlay: View {
    @Binding var isPresented: Bool

    private static let colors: [Color] = [
        Palette.ac, Palette.ac2,
        Color(red: 0xec / 255, green: 0x8c / 255, blue: 0x58 / 255),
        Color(red: 0xe8 / 255, green: 0x8c / 255, blue: 0xb4 / 255),
        Color(red: 0x8c / 255, green: 0xc8 / 255, blue: 0xe0 / 255),
        Palette.ink,
    ]

    private struct Piece {
        var x, y, vx, vy, size, rotation, spin: Double
        var color: Color
        var life: Double = 0
        var max: Double
    }

    @State private var pieces: [Piece] = []
    @State private var start = Date()

    var body: some View {
        GeometryReader { geometry in
            TimelineView(.animation) { timeline in
                Canvas { context, size in
                    let frame = (timeline.date.timeIntervalSince(start)) * 60
                    for piece in pieces {
                        let life = min(frame, piece.max)
                        if frame > piece.max { continue }
                        // Integrate analytically: constant drag on x, gravity on y.
                        let t = life
                        let x = piece.x + piece.vx * (1 - pow(0.99, t)) / 0.01
                        let y = piece.y + piece.vy * t + 0.14 * t * t
                        let fade = life > piece.max - 22 ? (piece.max - life) / 22 : 1
                        var layer = context
                        layer.opacity = max(0, fade)
                        layer.translateBy(x: x, y: y)
                        layer.rotate(by: .radians(piece.rotation + piece.spin * t))
                        layer.fill(Path(CGRect(x: -piece.size / 2, y: -piece.size / 2,
                                               width: piece.size, height: piece.size * 0.62)),
                                   with: .color(piece.color))
                    }
                }
            }
            .onAppear { seed(in: geometry.size) }
            .task {
                try? await Task.sleep(for: .seconds(4.3))
                isPresented = false
            }
        }
        .allowsHitTesting(false)
        .ignoresSafeArea()
    }

    private func seed(in size: CGSize) {
        start = Date()
        pieces = (0..<150).map { index in
            let angle = Double.random(in: 0..<(2 * .pi))
            let speed = Double.random(in: 4..<13)
            return Piece(
                x: size.width / 2, y: size.height * 0.4,
                vx: cos(angle) * speed, vy: sin(angle) * speed - 6,
                size: Double.random(in: 5..<11), rotation: Double.random(in: 0..<6.28),
                spin: Double.random(in: -0.15..<0.15),
                color: Self.colors[index % Self.colors.count],
                max: Double.random(in: 90..<135))
        }
    }
}

extension View {
    func confetti(_ isPresented: Binding<Bool>) -> some View {
        overlay {
            if isPresented.wrappedValue {
                ConfettiOverlay(isPresented: isPresented)
            }
        }
    }
}

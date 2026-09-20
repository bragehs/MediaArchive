import CoreGraphics
import Foundation

// One territory per genre: a hub orb, its label just outside it, and that
// genre's covers packed in rings around both, each spoked back to the orb.
// Positions are allocated, never negotiated — territories are disjoint
// circles, so nothing can collide until you drag one somewhere yourself.
struct ConstellationMap {
    static let hues: [(Double, Double, Double)] = [
        (200, 84, 150), (92, 196, 224), (214, 167, 74), (139, 190, 90), (236, 140, 88),
        (232, 140, 180), (150, 180, 210), (120, 200, 140), (176, 150, 110), (220, 96, 96), (236, 206, 92),
        (169, 198, 142),
    ]

    static let coverWidth = 22.0, coverHeight = 33.0, gap = 4.0
    // Below this on-screen width a cover is a tile of genre colour, above it the art.
    static let artWidth = 11.0

    struct Cover {
        let itemIndex: Int
        let x, y: Double
    }

    struct Territory {
        let genre: String
        let hue: (Double, Double, Double)
        let hubRadius: Double
        let labelSize: Double
        let labelHalf: Double
        let labelY: Double
        let radius: Double
        let covers: [Cover]
        var x = 0.0, y = 0.0
    }

    private(set) var items: [LibraryItem] = []
    private(set) var territories: [Territory] = []

    init() {}

    init(_ items: [LibraryItem]) {
        self.items = items

        var members: [String: [Int]] = [:]
        for (index, item) in items.enumerated() {
            for genre in Set(item.genres) { members[genre, default: []].append(index) }
        }

        let ordered = members.keys.sorted { (members[$0]!.count, $1) > (members[$1]!.count, $0) }
        territories = ordered.enumerated().map { rank, genre in
            build(genre: genre, indices: members[genre]!, hue: Self.hues[rank % Self.hues.count])
        }
        place()
    }

    private func build(genre: String, indices: [Int], hue: (Double, Double, Double)) -> Territory {
        let count = indices.count
        let hubRadius = 12 + sqrt(Double(count)) * 7
        let labelSize = min(max(9, 8 + sqrt(Double(count)) * 2.2), 20)
        let labelHalf = Double(genre.count) * labelSize * 0.32

        // The label goes in the gap between the orb and the first ring, so the
        // ring has to clear its width as well as its height — a long genre name
        // otherwise reaches under the covers sitting below-left and below-right.
        var covers: [Cover] = []
        var ring = max(hubRadius + labelSize * 2.2 + Self.coverHeight / 2,
                       labelHalf + Self.coverWidth / 2 + 8)

        while covers.count < count {
            // The tall side clears every direction: two tiles a chord apart are
            // furthest-worst on the diagonal, where h/√2 still exceeds the width.
            let capacity = max(1, Int((2 * .pi * ring / (Self.coverHeight + Self.gap)).rounded(.down)))
            let take = min(capacity, count - covers.count)
            // From the top, so a territory holding one or two works reads as
            // balanced above its label rather than lopsided to one side.
            let offset = Double(covers.count) * 0.7 - .pi / 2
            for slot in 0..<take {
                let angle = Double(slot) / Double(take) * 2 * .pi + offset
                covers.append(Cover(itemIndex: indices[covers.count],
                                    x: cos(angle) * ring, y: sin(angle) * ring))
            }
            ring += Self.coverHeight + Self.gap
        }

        let packed = ring - Self.coverHeight - Self.gap + hypot(Self.coverWidth, Self.coverHeight) / 2
        return Territory(genre: genre, hue: hue, hubRadius: hubRadius,
                         labelSize: labelSize, labelHalf: labelHalf,
                         labelY: hubRadius + labelSize * 1.05,
                         radius: packed + 4, covers: covers)
    }

    // Biggest first onto a golden-angle spiral, the step scaled to what is being
    // placed, each candidate cleared against everything already down.
    private mutating func place() {
        territories.sort { $0.radius > $1.radius }
        for index in territories.indices where index > 0 {
            let radius = territories[index].radius
            let step = max(14, radius * 0.5)
            var found = false
            for attempt in 1...6000 {
                let angle = Double(attempt) * 2.39996
                let distance = step * (Double(attempt)).squareRoot()
                let x = cos(angle) * distance, y = sin(angle) * distance
                if territories[..<index].allSatisfy({ hypot(x - $0.x, y - $0.y) >= radius + $0.radius + 8 }) {
                    territories[index].x = x
                    territories[index].y = y
                    found = true
                    break
                }
            }
            // The fallback clears everything already down rather than stacking at the origin.
            if !found {
                let reach = territories[..<index].map { hypot($0.x, $0.y) + $0.radius }.max() ?? 0
                territories[index].x = reach + radius + 8
            }
        }
    }

    var bounds: CGRect {
        guard !territories.isEmpty else { return .zero }
        let minX = territories.map { $0.x - $0.radius }.min()!
        let maxX = territories.map { $0.x + $0.radius }.max()!
        let minY = territories.map { $0.y - $0.radius }.min()!
        let maxY = territories.map { $0.y + $0.radius }.max()!
        return CGRect(x: minX, y: minY, width: max(maxX - minX, 1), height: max(maxY - minY, 1))
    }

    enum Hit {
        case genre(String)
        case cover(Int)
    }

    // Which orb a press landed on, so a drag can carry the whole territory.
    func hub(world point: CGPoint) -> Int? {
        territories.firstIndex { hypot(point.x - $0.x, point.y - $0.y) <= $0.hubRadius + 6 }
    }

    mutating func move(_ index: Int, by delta: CGPoint) {
        territories[index].x += delta.x
        territories[index].y += delta.y
    }

    func hit(world point: CGPoint) -> Hit? {
        for territory in territories {
            let dx = point.x - territory.x, dy = point.y - territory.y
            guard hypot(dx, dy) <= territory.radius else { continue }
            for cover in territory.covers
            where abs(dx - cover.x) <= Self.coverWidth / 2 && abs(dy - cover.y) <= Self.coverHeight / 2 {
                return .cover(cover.itemIndex)
            }
            if hypot(dx, dy) <= territory.hubRadius + 6 { return .genre(territory.genre) }
            if abs(dx) <= territory.labelHalf + 4,
               abs(dy - territory.labelY) <= territory.labelSize { return .genre(territory.genre) }
        }
        return nil
    }
}

// Where the map sits under the viewport. The map never moves, so this is the
// only thing a gesture changes.
struct MapCamera {
    var x = 0.0, y = 0.0, z = 1.0

    func screen(_ size: CGSize) -> CGPoint {
        CGPoint(x: size.width / 2 + x, y: size.height / 2 + y)
    }

    func world(_ point: CGPoint, in size: CGSize) -> CGPoint {
        let origin = screen(size)
        return CGPoint(x: (point.x - origin.x) / z, y: (point.y - origin.y) / z)
    }

    static func fitting(_ bounds: CGRect, in size: CGSize, padding: Double = 34) -> MapCamera {
        guard bounds.width > 1 else { return MapCamera() }
        let zoom = min(max(min((size.width - padding * 2) / bounds.width,
                               (size.height - padding * 2) / bounds.height), 0.12), 1.4)
        return MapCamera(x: -bounds.midX * zoom, y: -bounds.midY * zoom, z: zoom)
    }
}

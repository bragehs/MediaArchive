import CoreGraphics
import Foundation

// The Library's genre graph, ported from constellation.js with its constants:
// one hub per genre, one dot per (item, genre), copies of a shared work
// twin-linked so it bridges the hubs it belongs to. Plain state, no
// observation — the canvas reads and steps it every frame.
final class ConstellationGraph {
    static let hues: [(Double, Double, Double)] = [
        (200, 84, 150), (92, 196, 224), (214, 167, 74), (139, 190, 90), (236, 140, 88),
        (232, 140, 180), (150, 180, 210), (120, 200, 140), (176, 150, 110), (220, 96, 96), (236, 206, 92),
        (169, 198, 142), (180, 140, 220), (110, 200, 200), (210, 120, 140), (160, 200, 110),
    ]

    // Covers resolve in as you zoom: below fadeLow a dot, above fadeHigh a poster.
    static let coverWidth = 22.0, coverHeight = 33.0, fadeLow = 0.9, fadeHigh = 1.6

    enum Kind { case genre, item }

    final class Node {
        let id: String
        let kind: Kind
        let name: String
        let hue: (Double, Double, Double)
        let radius: Double
        let separation: Double
        let count: Int
        let itemIndex: Int
        let genre: String
        var x, y: Double
        var vx = 0.0, vy = 0.0
        var dx = 0.0, dy = 0.0
        let phase: Double

        init(id: String, kind: Kind, name: String, hue: (Double, Double, Double), radius: Double,
             separation: Double, count: Int, itemIndex: Int, genre: String, x: Double, y: Double, phase: Double) {
            self.id = id
            self.kind = kind
            self.name = name
            self.hue = hue
            self.radius = radius
            self.separation = separation
            self.count = count
            self.itemIndex = itemIndex
            self.genre = genre
            self.x = x
            self.y = y
            self.phase = phase
        }
    }

    struct Link {
        let source: Node
        let target: Node
        let twin: Bool
    }

    struct Camera {
        var x = 0.0, y = 0.0, z = 0.66
    }

    private(set) var items: [LibraryItem] = []
    private(set) var nodes: [Node] = []
    private(set) var links: [Link] = []
    private var adjacency: [String: Set<String>] = [:]
    private(set) var genreNodes: [String: Node] = [:]
    private(set) var genreItems: [String: [Int]] = [:]

    var camera = Camera()
    var cameraTarget: (x: Double, y: Double)?
    var alpha = 1.0
    var dragging: Node?
    var selected: Node?
    var highlighted: Set<String>?
    var searchHits: Set<String>?
    var viewport = CGSize(width: 1, height: 1)

    // Genres are stored lower case; capitalising is the view's job.
    static func capitalised(_ name: String) -> String { name.capitalized }

    func build(_ items: [LibraryItem]) {
        self.items = items
        nodes = []
        links = []
        adjacency = [:]
        genreNodes = [:]
        genreItems = [:]

        var rng = SeededRandom(seed: 1337)
        for (index, item) in items.enumerated() {
            for genre in genres(of: item) { genreItems[genre, default: []].append(index) }
        }

        let ordered = genreItems.keys.sorted { (genreItems[$0]!.count, $1) > (genreItems[$1]!.count, $0) }
        var hueOf: [String: (Double, Double, Double)] = [:]
        for (i, genre) in ordered.enumerated() {
            hueOf[genre] = Self.hues[i % Self.hues.count]
            let angle = Double(i) / Double(ordered.count) * 6.2832
            let count = genreItems[genre]!.count
            let node = Node(id: "g:\(genre)", kind: .genre, name: genre, hue: hueOf[genre]!,
                            radius: 16 + sqrt(Double(count)) * 10, separation: 16 + sqrt(Double(count)) * 10,
                            count: count, itemIndex: -1, genre: genre,
                            x: cos(angle) * 230, y: sin(angle) * 230, phase: Double(nodes.count) * 1.7)
            add(node)
            genreNodes[genre] = node
        }

        var copies: [Int: [Node]] = [:]
        for (index, item) in items.enumerated() {
            let itemGenres = genres(of: item)
            if itemGenres.isEmpty { continue }
            let radius = 4 + Double(item.rating ?? 0) / 10 * 4
            for genre in itemGenres {
                guard let hub = genreNodes[genre], let list = genreItems[genre] else { continue }
                let position = list.firstIndex(of: index) ?? 0
                let angle = Double(position) / Double(list.count) * 6.2832 + rng.next() * 0.6
                let orbit = hub.radius + 40
                // The dot stays small; separation uses the cover's half-diagonal so
                // posters have room to resolve without colliding.
                let node = Node(id: "i:\(index)@\(genre)", kind: .item, name: item.title, hue: hub.hue,
                                radius: radius, separation: hypot(Self.coverWidth, Self.coverHeight) / 2,
                                count: 0, itemIndex: index, genre: genre,
                                x: hub.x + cos(angle) * orbit, y: hub.y + sin(angle) * orbit,
                                phase: Double(nodes.count) * 1.7)
                add(node)
                link(node, hub, twin: false)
                copies[index, default: []].append(node)
            }
        }
        for list in copies.values {
            for a in 0..<list.count {
                for b in (a + 1)..<list.count { link(list[a], list[b], twin: true) }
            }
        }
    }

    private func add(_ node: Node) {
        nodes.append(node)
        adjacency[node.id] = []
    }

    private func link(_ a: Node, _ b: Node, twin: Bool) {
        links.append(Link(source: a, target: b, twin: twin))
        adjacency[a.id, default: []].insert(b.id)
        adjacency[b.id, default: []].insert(a.id)
    }

    private func genres(of item: LibraryItem) -> [String] {
        var seen = Set<String>()
        return item.genres.filter { seen.insert($0).inserted }
    }

    func itemIndices(of node: Node) -> [Int] {
        node.kind == .item ? [node.itemIndex] : (genreItems[node.name] ?? [])
    }

    func connectedIds(_ node: Node) -> Set<String> {
        var set: Set<String> = [node.id]
        if node.kind == .item {
            for other in nodes where other.kind == .item && other.itemIndex == node.itemIndex {
                set.insert(other.id)
                set.formUnion(adjacency[other.id] ?? [])
            }
            return set
        }
        for instance in adjacency[node.id] ?? [] {
            set.insert(instance)
            for next in adjacency[instance] ?? [] {
                set.insert(next)
                if next.hasPrefix("i") { set.formUnion(adjacency[next] ?? []) }
            }
        }
        return set
    }

    func isActive(_ id: String) -> Bool {
        if let highlighted { return highlighted.contains(id) }
        if let searchHits { return searchHits.contains(id) }
        return true
    }

    var dimming: Bool { highlighted != nil || searchHits != nil }

    // One relaxation step of the force layout; alpha cools it to a standstill.
    func step() {
        if alpha < 0.004 { return }
        alpha *= 0.987
        let count = nodes.count
        for i in 0..<count {
            let a = nodes[i]
            for j in (i + 1)..<count {
                let b = nodes[j]
                var dx = a.x - b.x, dy = a.y - b.y
                var d2 = dx * dx + dy * dy
                if d2 == 0 { d2 = 0.01 }
                let bothGenres = a.kind == .genre && b.kind == .genre
                let repulsion = bothGenres ? 11000.0 : (a.kind == .item && b.kind == .item ? 1400.0 : 2000.0)
                let minimum = a.separation + b.separation + 22
                var force = repulsion / d2
                if d2 < minimum * minimum { force += (minimum * minimum - d2) / d2 * 1.1 }
                let d = sqrt(d2)
                dx /= d
                dy /= d
                a.vx += dx * force * alpha
                a.vy += dy * force * alpha
                b.vx -= dx * force * alpha
                b.vy -= dy * force * alpha
            }
        }
        for l in links {
            var dx = l.target.x - l.source.x, dy = l.target.y - l.source.y
            var d = hypot(dx, dy)
            if d == 0 { d = 0.01 }
            if !l.twin {
                let rest = l.target.radius + 40
                let force = (d - rest) * 0.05 * alpha
                dx /= d
                dy /= d
                l.source.vx += dx * force
                l.source.vy += dy * force
                l.target.vx -= dx * force * 0.02
                l.target.vy -= dy * force * 0.02
            } else {
                let force = (d - 90) * 0.006 * alpha
                dx /= d
                dy /= d
                l.source.vx += dx * force
                l.source.vy += dy * force
                l.target.vx -= dx * force
                l.target.vy -= dy * force
            }
        }
        for a in nodes {
            a.vx -= a.x * 0.0018 * alpha
            a.vy -= a.y * 0.0018 * alpha
            a.vx *= 0.85
            a.vy *= 0.85
            if a !== dragging {
                a.x += a.vx
                a.y += a.vy
            }
        }
    }

    // Settle the layout before the first frame, then let it breathe.
    func relax() {
        alpha = 1
        var guardCount = 0
        while alpha > 0.02 && guardCount < 4000 {
            step()
            guardCount += 1
        }
        alpha = 1
    }

    func fitView(padding: Double = 90) {
        var minX = 1e9, maxX = -1e9, minY = 1e9, maxY = -1e9
        for n in nodes {
            minX = min(minX, n.x - n.radius)
            maxX = max(maxX, n.x + n.radius)
            minY = min(minY, n.y - n.radius)
            maxY = max(maxY, n.y + n.radius)
        }
        let width = max(maxX - minX, 1), height = max(maxY - minY, 1)
        let vw = viewport.width, vh = viewport.height
        camera.z = min(max(min((vw - padding * 2) / width, (vh - padding * 2) / height), 0.26), 1.15)
        camera.x = -((minX + maxX) / 2) * camera.z
        camera.y = -((minY + maxY) / 2) * camera.z
    }

    func toWorld(_ point: CGPoint) -> (x: Double, y: Double) {
        ((point.x - (viewport.width / 2 + camera.x)) / camera.z,
         (point.y - (viewport.height * 0.46 + camera.y)) / camera.z)
    }

    func node(at point: CGPoint) -> Node? {
        let w = toWorld(point)
        var best: Node?
        var bestDistance = 1e9
        let zoomedIn = camera.z > (Self.fadeLow + Self.fadeHigh) / 2
        for a in nodes {
            let d = hypot(a.x - w.x, a.y - w.y)
            // Once the poster is showing, the tap target is the poster, not the dot.
            let hit = a.kind == .item
                ? (zoomedIn ? max(Self.coverWidth, Self.coverHeight) / 2 : a.radius + 11)
                : a.radius + 4
            if d < hit && d < bestDistance {
                bestDistance = d
                best = a
            }
        }
        return best
    }

    func select(_ node: Node?) {
        selected = node
        guard let node else { highlighted = nil; return }
        highlighted = connectedIds(node)
        alpha = max(alpha, 0.4)
    }

    func center(on node: Node) {
        cameraTarget = (-node.x * camera.z, -node.y * camera.z)
        alpha = max(alpha, 0.2)
    }

    // Eases toward a requested centre; called once per frame.
    func glide() {
        guard let target = cameraTarget else { return }
        camera.x += (target.x - camera.x) * 0.18
        camera.y += (target.y - camera.y) * 0.18
        if abs(target.x - camera.x) < 0.5 && abs(target.y - camera.y) < 0.5 {
            camera.x = target.x
            camera.y = target.y
            cameraTarget = nil
        }
    }
}

// mulberry32 — the same generator the JS used, so the initial scatter matches.
struct SeededRandom {
    private var state: UInt32

    init(seed: UInt32) { state = seed }

    mutating func next() -> Double {
        state = state &+ 0x6D2B79F5
        var t = UInt32(truncatingIfNeeded: UInt64(state ^ (state >> 15)) * UInt64(1 | state))
        t = (t &+ UInt32(truncatingIfNeeded: UInt64(t ^ (t >> 7)) * UInt64(61 | t))) ^ t
        return Double((t ^ (t >> 14))) / 4294967296
    }
}

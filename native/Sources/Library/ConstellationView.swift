import SwiftUI

// The map: the archive as a genre graph. It picks a genre and hands you back
// to the wall — the list of works in a genre is the wall's job, not a sheet's.
struct ConstellationView: View {
    let isActive: Bool

    private let store = LibraryStore.shared
    @Environment(\.dismiss) private var dismiss

    private static let ground = Color(red: 0x0a / 255, green: 0x0a / 255, blue: 0x0a / 255)

    var body: some View {
        ZStack(alignment: .top) {
            Self.ground.ignoresSafeArea()

            ConstellationCanvas(store: store, isActive: isActive, pick: pick)
                .ignoresSafeArea(edges: .bottom)

            if let error = store.error {
                Notice(text: error).padding(20).padding(.top, 60)
            } else if !store.loaded {
                Aside("Loading…").padding(.top, 80)
            }

            HStack {
                Crumb("Library", trail: "Map") { dismiss() }
                Spacer()
            }
            .padding(.horizontal, 18)

            if store.loaded {
                HudChip(text: countLabel)
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .bottomTrailing)
                    .padding(14)
            }
        }
        .task { await store.load() }
    }

    private var countLabel: String {
        "\(store.graph.items.count) works · \(store.graph.genreNodes.count) genres"
    }

    private func pick(_ node: ConstellationGraph.Node) {
        if node.kind == .item {
            openItem(store.graph.items[node.itemIndex].userMediaItemId)
        } else {
            store.filter(byGenre: node.name)
            dismiss()
        }
    }
}

private struct HudChip: View {
    let text: String

    var body: some View {
        Text(text)
            .font(Fonts.display(11))
            .tracking(0.9)
            .foregroundStyle(Palette.muted)
            .padding(.horizontal, 14)
            .padding(.vertical, 8)
            .background(Color(red: 24 / 255, green: 24 / 255, blue: 24 / 255).opacity(0.86), in: Capsule())
            .overlay(Capsule().stroke(Palette.line, lineWidth: 1))
            .shadow(color: .black.opacity(0.4), radius: 10, y: 6)
    }
}

// Genre hue as a Color, and the per-type fallback gradient the map uses.
extension ConstellationGraph.Node {
    var color: Color { Color(red: hue.0 / 255, green: hue.1 / 255, blue: hue.2 / 255) }
}

extension Color {
    init(hex: UInt32) {
        self.init(red: Double((hex >> 16) & 0xff) / 255, green: Double((hex >> 8) & 0xff) / 255,
                  blue: Double(hex & 0xff) / 255)
    }
}

// The drawing and the pointer state machine: tap, drag a node, pan, pinch.
private struct ConstellationCanvas: View {
    let store: LibraryStore
    let isActive: Bool
    let pick: (ConstellationGraph.Node) -> Void

    @State private var dragNode: ConstellationGraph.Node?
    @State private var panning = false
    @State private var moved = false
    @State private var last: CGPoint?
    @State private var pinching = false
    @State private var pinchZoom = 0.0
    @State private var pinchWorld: (x: Double, y: Double) = (0, 0)

    private var graph: ConstellationGraph { store.graph }

    var body: some View {
        TimelineView(.animation(minimumInterval: 1 / 60, paused: !isActive)) { timeline in
            Canvas(rendersAsynchronously: false) { context, size in
                draw(&context, size: size, time: timeline.date.timeIntervalSinceReferenceDate)
            }
        }
        .contentShape(Rectangle())
        .gesture(drag)
        .simultaneousGesture(pinch)
    }

    private var drag: some Gesture {
        DragGesture(minimumDistance: 0, coordinateSpace: .local)
            .onChanged { value in
                if pinching { return }
                if last == nil {
                    moved = false
                    last = value.startLocation
                    if let node = graph.node(at: value.startLocation) {
                        dragNode = node
                        graph.dragging = node
                    } else {
                        panning = true
                    }
                }
                let delta = CGPoint(x: value.location.x - last!.x, y: value.location.y - last!.y)
                if abs(delta.x) + abs(delta.y) > 3 { moved = true }
                if let node = dragNode {
                    let w = graph.toWorld(value.location)
                    node.x = w.x
                    node.y = w.y
                    node.vx = 0
                    node.vy = 0
                    graph.alpha = max(graph.alpha, 0.3)
                } else if panning {
                    graph.camera.x += delta.x
                    graph.camera.y += delta.y
                }
                last = value.location
            }
            .onEnded { value in
                if !pinching, !moved, let node = graph.node(at: value.location) { pick(node) }
                reset()
            }
    }

    private var pinch: some Gesture {
        MagnifyGesture()
            .onChanged { value in
                let anchor = CGPoint(x: value.startAnchor.x * graph.viewport.width,
                                     y: value.startAnchor.y * graph.viewport.height)
                if !pinching {
                    pinching = true
                    pinchZoom = graph.camera.z
                    pinchWorld = graph.toWorld(anchor)
                    graph.dragging = nil
                    dragNode = nil
                    panning = false
                }
                graph.camera.z = min(max(pinchZoom * value.magnification, 0.26), 3)
                graph.camera.x = anchor.x - graph.viewport.width / 2 - pinchWorld.x * graph.camera.z
                graph.camera.y = anchor.y - graph.viewport.height * 0.46 - pinchWorld.y * graph.camera.z
            }
            .onEnded { _ in
                pinching = false
                reset()
            }
    }

    private func reset() {
        dragNode = nil
        graph.dragging = nil
        panning = false
        last = nil
    }

    private func draw(_ context: inout GraphicsContext, size: CGSize, time: TimeInterval) {
        graph.viewport = size
        if store.pendingFit {
            graph.fitView()
            store.pendingFit = false
        }
        graph.step()

        let width = size.width, height = size.height
        context.fill(Path(CGRect(origin: .zero, size: size)), with: .radialGradient(
            Gradient(colors: [Color(hex: 0x161616), Color(hex: 0x0a0a0a)]),
            center: CGPoint(x: width / 2, y: height * 0.44), startRadius: 60, endRadius: max(width, height) * 0.85))

        let camera = graph.camera
        var world = context
        world.translateBy(x: width / 2 + camera.x, y: height * 0.46 + camera.y)
        world.scaleBy(x: camera.z, y: camera.z)

        for node in graph.nodes {
            if node === graph.dragging {
                node.dx = node.x
                node.dy = node.y
                continue
            }
            let amplitude = node.kind == .item ? 3.4 : 1.6
            node.dx = node.x + sin(time * 0.5 + node.phase) * amplitude
            node.dy = node.y + cos(time * 0.42 + node.phase * 1.3) * amplitude
        }

        for link in graph.links {
            let ax = link.source.dx, ay = link.source.dy, bx = link.target.dx, by = link.target.dy
            let mx = (ax + bx) / 2, my = (ay + by) / 2
            let vx = bx - ax, vy = by - ay
            let length = max(hypot(vx, vy), 1)
            let bow = length * (link.twin ? 0.2 : 0.05) * (link.source.itemIndex % 2 == 1 ? 1 : -1)
            var path = Path()
            path.move(to: CGPoint(x: ax, y: ay))
            path.addQuadCurve(to: CGPoint(x: bx, y: by),
                              control: CGPoint(x: mx - vy / length * bow, y: my + vx / length * bow))
            let stroke: Color = link.twin
                ? Color(red: 242 / 255, green: 239 / 255, blue: 230 / 255).opacity(0.11)
                : link.target.color.opacity(0.3)
            world.stroke(path, with: .color(stroke), lineWidth: 1)
        }

        // World-space viewport with a cover's margin: decode and draw only what is on screen.
        let margin = ConstellationGraph.coverHeight
        let halfW = width / 2, halfH = height * 0.46
        let vx0 = (-halfW - camera.x) / camera.z - margin, vx1 = (width - halfW - camera.x) / camera.z + margin
        let vy0 = (-halfH - camera.y) / camera.z - margin, vy1 = (height - halfH - camera.y) / camera.z + margin
        let coverT = min(max((camera.z - ConstellationGraph.fadeLow) / (ConstellationGraph.fadeHigh - ConstellationGraph.fadeLow), 0), 1)

        for node in graph.nodes where node.kind == .item {
            guard node.x > vx0, node.x < vx1, node.y > vy0, node.y < vy1 else { continue }
            let item = graph.items[node.itemIndex]

            if coverT < 1 {
                var dot = world
                dot.opacity = 1 - coverT
                dot.fill(Path(ellipseIn: CGRect(x: node.dx - node.radius, y: node.dy - node.radius,
                                                width: node.radius * 2, height: node.radius * 2)),
                         with: .color(node.color))
            }
            if coverT > 0 {
                drawCover(&world, node: node, item: item, opacity: coverT)
            }
        }

        for node in graph.nodes where node.kind == .genre {
            let rect = CGRect(x: node.dx - node.radius, y: node.dy - node.radius, width: node.radius * 2, height: node.radius * 2)
            world.drawLayer { layer in
                layer.addFilter(.shadow(color: node.color.opacity(0.7), radius: 11))
                layer.fill(Path(ellipseIn: rect), with: .radialGradient(
                    Gradient(colors: [node.color.opacity(0.97), node.color.opacity(0.74)]),
                    center: CGPoint(x: node.dx - node.radius * 0.3, y: node.dy - node.radius * 0.3),
                    startRadius: node.radius * 0.2, endRadius: node.radius))
            }
            let fontSize = min(max(node.radius * 0.5, 10), 18).rounded(.down)
            var label = world
            label.addFilter(.shadow(color: Color(red: 6 / 255, green: 6 / 255, blue: 6 / 255).opacity(0.92), radius: 1.5))
            label.draw(Text(ConstellationGraph.capitalised(node.name))
                .font(Fonts.title(fontSize))
                .foregroundStyle(Palette.ink), at: CGPoint(x: node.dx, y: node.dy))
        }
    }

    private func coverRect(_ node: ConstellationGraph.Node) -> CGRect {
        CGRect(x: node.dx - ConstellationGraph.coverWidth / 2, y: node.dy - ConstellationGraph.coverHeight / 2,
               width: ConstellationGraph.coverWidth, height: ConstellationGraph.coverHeight)
    }

    private func drawCover(_ context: inout GraphicsContext, node: ConstellationGraph.Node, item: LibraryItem, opacity: Double) {
        let rect = coverRect(node)
        let image = item.imageUrl.flatMap { ImageCache.shared.cached($0) }
        if image == nil, let url = item.imageUrl, !requested.contains(url) {
            requested.insert(url)
            Task { _ = await ImageCache.shared.image(url) }
        }

        context.drawLayer { layer in
            layer.opacity = opacity
            layer.clip(to: Path(roundedRect: rect, cornerRadius: 3))
            if let image {
                // Crop to fill, like CSS background-size: cover.
                let scale = max(rect.width / image.size.width, rect.height / image.size.height)
                let drawn = CGSize(width: image.size.width * scale, height: image.size.height * scale)
                layer.draw(layer.resolve(Image(uiImage: image)),
                           in: CGRect(x: rect.midX - drawn.width / 2, y: rect.midY - drawn.height / 2,
                                      width: drawn.width, height: drawn.height))
            } else {
                layer.fill(Path(rect), with: .linearGradient(
                    Gradient(colors: gradientColors(item.mediaType)),
                    startPoint: rect.origin, endPoint: CGPoint(x: rect.maxX, y: rect.maxY)))
            }
        }
        // Hairline in the genre hue so the colour coding survives the zoom-in.
        var outline = context
        outline.opacity = opacity * 0.9
        outline.stroke(Path(roundedRect: rect, cornerRadius: 3), with: .color(node.color.opacity(0.85)), lineWidth: 1)
    }

    private func gradientColors(_ type: MediaType) -> [Color] {
        switch type {
        case .book: [Color(hex: 0xa9812f), Color(hex: 0x7a5320)]
        case .game: [Color(hex: 0x4a6d63), Color(hex: 0x2b4746)]
        case .movie: [Color(hex: 0x3b4a6b), Color(hex: 0x26304a)]
        case .show: [Color(hex: 0x8c3f33), Color(hex: 0x5e2620)]
        }
    }
}

// Which covers have been asked for, so the draw loop requests each once.
@MainActor private var requested = Set<String>()

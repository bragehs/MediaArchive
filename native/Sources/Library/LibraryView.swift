import SwiftUI

// The Library is the constellation: a black canvas with the genre map, a
// search bar over it, a HUD, and a bottom panel for the selected hub.
struct LibraryView: View {
    let isActive: Bool

    @State private var store = LibraryStore()

    private static let ground = Color(red: 0x0a / 255, green: 0x0a / 255, blue: 0x0a / 255)

    var body: some View {
        ZStack(alignment: .top) {
            Self.ground.ignoresSafeArea()

            ConstellationCanvas(store: store, isActive: isActive)
                .ignoresSafeArea(edges: .bottom)

            if let error = store.error {
                Notice(text: error).padding(20).padding(.top, 60)
            } else if !store.loaded {
                Aside("Loading…").padding(.top, 80)
            }

            LibrarySearch(store: store)
                .padding(.horizontal, 20)
                .padding(.top, 12)

            hud
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .bottomTrailing)
                .padding(14)

            if store.selection != nil {
                Color.black.opacity(0.45)
                    .ignoresSafeArea()
                    .onTapGesture { store.select(nil) }
                    .transition(.opacity)
            }
        }
        .overlay(alignment: .bottom) {
            if let node = store.selection {
                GenrePanel(store: store, node: node)
                    .transition(.move(edge: .bottom))
            }
        }
        .animation(.spring(duration: 0.4, bounce: 0.15), value: store.selection?.id)
        .task { await store.load() }
        .task(id: store.query) {
            try? await Task.sleep(for: .milliseconds(170))
            await store.search()
        }
    }

    private var hud: some View {
        VStack(alignment: .trailing, spacing: 10) {
            if store.selection != nil {
                Button { store.select(nil) } label: {
                    HudChip(text: "Show all", color: Palette.ink, border: Palette.ac)
                }
                .buttonStyle(.plain)
            }
            if store.loaded {
                HudChip(text: store.countLabel, color: Palette.muted, border: Palette.line)
            }
        }
    }
}

private struct HudChip: View {
    let text: String
    let color: Color
    let border: Color

    var body: some View {
        Text(text)
            .font(Fonts.display(11))
            .tracking(0.9)
            .foregroundStyle(color)
            .padding(.horizontal, 14)
            .padding(.vertical, 8)
            .background(Color(red: 24 / 255, green: 24 / 255, blue: 24 / 255).opacity(0.86), in: Capsule())
            .overlay(Capsule().stroke(border, lineWidth: 1))
            .shadow(color: .black.opacity(0.4), radius: 10, y: 6)
    }
}

// Genre hue as a Color, and the per-type fallback gradient the map uses.
extension ConstellationGraph.Node {
    var color: Color { Color(red: hue.0 / 255, green: hue.1 / 255, blue: hue.2 / 255) }
}

enum ConstellationPalette {
    static func gradient(_ type: MediaType) -> LinearGradient {
        let pair: (Color, Color) = switch type {
        case .book: (Color(hex: 0xa9812f), Color(hex: 0x7a5320))
        case .game: (Color(hex: 0x4a6d63), Color(hex: 0x2b4746))
        case .movie: (Color(hex: 0x3b4a6b), Color(hex: 0x26304a))
        case .show: (Color(hex: 0x8c3f33), Color(hex: 0x5e2620))
        }
        return LinearGradient(colors: [pair.0, pair.1], startPoint: .topLeading, endPoint: .bottomTrailing)
    }
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
                    graph.cameraTarget = nil
                }
                last = value.location
            }
            .onEnded { value in
                if !pinching, !moved {
                    if let node = graph.node(at: value.location) {
                        if node.kind == .item {
                            openItem(graph.items[node.itemIndex].userMediaItemId)
                        } else {
                            store.select(node)
                        }
                    } else {
                        store.select(nil)
                    }
                }
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
                graph.cameraTarget = nil
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
        graph.glide()

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

        let dimming = graph.dimming
        for link in graph.links {
            let on = graph.isActive(link.source.id) && graph.isActive(link.target.id)
            let dim = dimming && !on
            let ax = link.source.dx, ay = link.source.dy, bx = link.target.dx, by = link.target.dy
            let mx = (ax + bx) / 2, my = (ay + by) / 2
            let vx = bx - ax, vy = by - ay
            let length = max(hypot(vx, vy), 1)
            let bow = length * (link.twin ? 0.2 : 0.05) * (link.source.itemIndex % 2 == 1 ? 1 : -1)
            var path = Path()
            path.move(to: CGPoint(x: ax, y: ay))
            path.addQuadCurve(to: CGPoint(x: bx, y: by),
                              control: CGPoint(x: mx - vy / length * bow, y: my + vx / length * bow))
            let stroke: Color = dim
                ? Color(red: 150 / 255, green: 160 / 255, blue: 140 / 255).opacity(0.045)
                : (link.twin ? Color(red: 242 / 255, green: 239 / 255, blue: 230 / 255).opacity(0.11)
                             : link.target.color.opacity(0.3))
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
            let on = graph.isActive(node.id)
            let base = dimming && !on ? 0.14 : 1.0
            let item = graph.items[node.itemIndex]

            if coverT < 1 {
                var dot = world
                dot.opacity = base * (1 - coverT)
                dot.fill(Path(ellipseIn: CGRect(x: node.dx - node.radius, y: node.dy - node.radius,
                                                width: node.radius * 2, height: node.radius * 2)),
                         with: .color(node.color))
            }
            if coverT > 0 {
                drawCover(&world, node: node, item: item, opacity: base * coverT)
            }
            if node === graph.selected {
                var ring = world
                ring.opacity = base
                let path = coverT > 0.5
                    ? Path(roundedRect: coverRect(node), cornerRadius: 3)
                    : Path(ellipseIn: CGRect(x: node.dx - node.radius, y: node.dy - node.radius,
                                             width: node.radius * 2, height: node.radius * 2))
                ring.stroke(path, with: .color(Palette.ink), lineWidth: 2)
            }
        }

        for node in graph.nodes where node.kind == .genre {
            let on = graph.isActive(node.id)
            let dim = dimming && !on
            let rect = CGRect(x: node.dx - node.radius, y: node.dy - node.radius, width: node.radius * 2, height: node.radius * 2)
            world.drawLayer { layer in
                layer.opacity = dim ? 0.16 : 1
                if !dim { layer.addFilter(.shadow(color: node.color.opacity(0.7), radius: 11)) }
                layer.fill(Path(ellipseIn: rect), with: .radialGradient(
                    Gradient(colors: [node.color.opacity(0.97), node.color.opacity(0.74)]),
                    center: CGPoint(x: node.dx - node.radius * 0.3, y: node.dy - node.radius * 0.3),
                    startRadius: node.radius * 0.2, endRadius: node.radius))
            }
            if node === graph.selected {
                world.stroke(Path(ellipseIn: rect), with: .color(Palette.ink), lineWidth: 2.5)
            }
            if node.radius > 14 || !dimming || on {
                let fontSize = min(max(node.radius * 0.5, 10), 18).rounded(.down)
                var label = world
                label.opacity = dim ? 0.3 : 1
                label.addFilter(.shadow(color: Color(red: 6 / 255, green: 6 / 255, blue: 6 / 255).opacity(0.92), radius: 1.5))
                label.draw(Text(ConstellationGraph.capitalised(node.name))
                    .font(Fonts.title(fontSize))
                    .foregroundStyle(Palette.ink), at: CGPoint(x: node.dx, y: node.dy))
            }
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

// Search over the whole archive; the map only draws what was finished, so an
// off-map hit says so rather than looking like a dead end.
private struct LibrarySearch: View {
    @Bindable var store: LibraryStore
    @Environment(\.lexicon) private var lexicon
    @FocusState private var focused: Bool

    var body: some View {
        VStack(spacing: 8) {
            HStack(spacing: 9) {
                Text("⌕").font(.system(size: 14, weight: .bold)).foregroundStyle(Palette.dim)
                TextField("Search the archive…", text: $store.query)
                    .font(Fonts.display(13.5))
                    .foregroundStyle(Palette.ink)
                    .textInputAutocapitalization(.never)
                    .autocorrectionDisabled()
                    .focused($focused)
                    .onChange(of: focused) { if focused, !store.query.isEmpty { store.resultsOpen = true } }
                if !store.query.isEmpty {
                    Button { store.clearSearch(); focused = true } label: {
                        Text("✕")
                            .font(.system(size: 14))
                            .foregroundStyle(Palette.muted)
                            .frame(width: 26, height: 26)
                            .background(Color(red: 242 / 255, green: 239 / 255, blue: 230 / 255).opacity(0.1), in: Circle())
                    }
                    .buttonStyle(.plain)
                }
            }
            .padding(.horizontal, 14)
            .frame(height: 40)
            .background(Color(red: 24 / 255, green: 24 / 255, blue: 24 / 255).opacity(0.86), in: Capsule())
            .overlay(Capsule().stroke(Palette.line, lineWidth: 1))
            .shadow(color: .black.opacity(0.5), radius: 17, y: 10)

            if store.resultsOpen, store.searched, !store.query.isEmpty {
                results
            }
        }
    }

    private var results: some View {
        ScrollView {
            VStack(spacing: 0) {
                if !store.genreHits.isEmpty {
                    resultHead("Genres")
                    ForEach(store.genreHits, id: \.id) { node in
                        Button { pick(node) } label: {
                            HStack(spacing: 11) {
                                Circle().fill(node.color).frame(width: 11, height: 11)
                                    .shadow(color: node.color, radius: 4)
                                Text(ConstellationGraph.capitalised(node.name)).font(Fonts.title(14)).foregroundStyle(Palette.ink).lineLimit(1)
                                Spacer()
                                Eyebrow("\(node.count) works", size: 10, tracking: 0.06, bold: false)
                            }
                            .padding(.horizontal, 15)
                            .padding(.vertical, 11)
                        }
                        .buttonStyle(.plain)
                        .overlay(alignment: .bottom) { HairlineRule(color: Palette.line2) }
                    }
                }
                if !store.itemHits.isEmpty {
                    resultHead("Titles")
                    ForEach(store.itemHits.prefix(40), id: \.userMediaItemId) { item in
                        Button { focused = false; store.resultsOpen = false; openItem(item.userMediaItemId) } label: {
                            itemRow(item)
                        }
                        .buttonStyle(.plain)
                        .overlay(alignment: .bottom) { HairlineRule(color: Palette.line2) }
                    }
                }
                if store.genreHits.isEmpty && store.itemHits.isEmpty {
                    Text("No matches in the archive.")
                        .font(Fonts.title(14))
                        .foregroundStyle(Palette.dim)
                        .padding(15)
                }
            }
        }
        .frame(maxHeight: UIScreen.main.bounds.height * 0.46)
        .background(Color(red: 16 / 255, green: 16 / 255, blue: 16 / 255).opacity(0.95), in: RoundedRectangle(cornerRadius: 16))
        .overlay(RoundedRectangle(cornerRadius: 16).stroke(Palette.line, lineWidth: 1))
    }

    private func resultHead(_ text: String) -> some View {
        Eyebrow(text, size: 10, color: Palette.ac, tracking: 0.14)
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.horizontal, 15)
            .padding(.top, 9)
            .padding(.bottom, 5)
    }

    private func itemRow(_ item: LibraryItem) -> some View {
        let off = item.status == .completed ? nil : "\(lexicon.glyph(item.status)) \(lexicon.label(item.status))"
        return HStack(spacing: 11) {
            CoverImage(url: item.imageUrl, title: "", fallbackSize: 0)
                .frame(width: 36, height: 54)
                .clipShape(RoundedRectangle(cornerRadius: 5))
                .overlay(RoundedRectangle(cornerRadius: 5).stroke(Palette.line2, lineWidth: 1))
                .opacity(off == nil ? 1 : 0.55)
            VStack(alignment: .leading, spacing: 2) {
                Text(item.title).font(Fonts.title(14)).foregroundStyle(Palette.ink).lineLimit(1)
                Text([item.creator, off].compactMap { $0 }.joined(separator: " · "))
                    .font(Fonts.display(10.5))
                    .foregroundStyle(off == nil ? Palette.dim : Palette.ac2)
                    .lineLimit(1)
            }
            Spacer()
            Eyebrow(lexicon.label(item.mediaType), size: 10, tracking: 0.06, bold: false)
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 9)
    }

    private func pick(_ node: ConstellationGraph.Node) {
        focused = false
        store.resultsOpen = false
        store.focus(node)
    }
}

// The bottom sheet for a genre hub: what it threads into, and every work in it.
private struct GenrePanel: View {
    let store: LibraryStore
    let node: ConstellationGraph.Node

    var body: some View {
        let members = store.members(of: node)
        let related = store.related(to: node)

        VStack(spacing: 0) {
            Capsule().fill(Palette.line).frame(width: 40, height: 4).padding(.top, 10).padding(.bottom, 6)

            HStack(alignment: .top, spacing: 13) {
                Circle().fill(node.color).frame(width: 15, height: 15)
                    .shadow(color: node.color, radius: 6)
                    .padding(.top, 6)
                VStack(alignment: .leading, spacing: 2) {
                    Eyebrow("Genre · \(members.count) works", size: 10, tracking: 0.16, bold: false)
                    Text(ConstellationGraph.capitalised(node.name)).font(Fonts.title(22)).foregroundStyle(Palette.ink)
                }
                Spacer()
            }
            .padding(.horizontal, 20)
            .padding(.top, 6)
            .padding(.bottom, 14)
            .overlay(alignment: .bottom) { HairlineRule(color: Palette.line2) }

            ScrollView {
                VStack(alignment: .leading, spacing: 10) {
                    if !related.isEmpty {
                        groupHead("Threads into")
                        FlowLayout(spacing: 7) {
                            ForEach(related, id: \.genre) { entry in
                                Button {
                                    if let hub = store.graph.genreNodes[entry.genre] { store.focus(hub) }
                                } label: {
                                    HStack(spacing: 7) {
                                        Circle().fill(store.graph.genreNodes[entry.genre]?.color ?? Palette.dim).frame(width: 8, height: 8)
                                        Text("\(ConstellationGraph.capitalised(entry.genre)) · \(entry.count)")
                                            .font(Fonts.display(12))
                                            .foregroundStyle(Palette.muted)
                                    }
                                    .padding(.horizontal, 12)
                                    .padding(.vertical, 6)
                                    .overlay(Capsule().stroke(Palette.line, lineWidth: 1))
                                }
                                .buttonStyle(.plain)
                            }
                        }
                        .padding(.bottom, 4)
                        groupHead("\(members.count) works")
                    }

                    LazyVGrid(columns: Array(repeating: GridItem(.flexible(), spacing: 8), count: 5), spacing: 8) {
                        ForEach(members, id: \.userMediaItemId) { item in
                            Button { openItem(item.userMediaItemId) } label: {
                                VStack(alignment: .leading, spacing: 4) {
                                    poster(item)
                                    Text(item.title)
                                        .font(Fonts.title(9))
                                        .foregroundStyle(Palette.ink)
                                        .multilineTextAlignment(.leading)
                                        .lineLimit(2)
                                        .frame(maxWidth: .infinity, alignment: .leading)
                                }
                            }
                            .buttonStyle(.plain)
                        }
                    }
                }
                .padding(.horizontal, 18)
                .padding(.top, 16)
                .padding(.bottom, 26)
            }
        }
        .frame(maxWidth: .infinity)
        .frame(height: UIScreen.main.bounds.height * 0.62)
        .background(
            LinearGradient(colors: [Color(hex: 0x1a1a1a), Color(hex: 0x0d0d0d)], startPoint: .top, endPoint: .bottom),
            in: UnevenRoundedRectangle(topLeadingRadius: 22, topTrailingRadius: 22))
        .overlay(alignment: .top) {
            UnevenRoundedRectangle(topLeadingRadius: 22, topTrailingRadius: 22).stroke(Palette.line, lineWidth: 1)
        }
        .shadow(color: .black.opacity(0.6), radius: 25, y: -16)
    }

    private func groupHead(_ text: String) -> some View {
        Text(text.uppercased())
            .font(Fonts.title(11))
            .tracking(1.5)
            .foregroundStyle(Palette.muted)
            .padding(.horizontal, 4)
            .padding(.top, 6)
    }

    private func poster(_ item: LibraryItem) -> some View {
        ZStack(alignment: .bottomLeading) {
            if let url = item.imageUrl {
                CoverImage(url: url, title: "", fallbackSize: 0)
            } else {
                ConstellationPalette.gradient(item.mediaType)
                Text(item.title)
                    .font(Fonts.serif(9))
                    .foregroundStyle(Palette.ink)
                    .shadow(color: .black.opacity(0.6), radius: 1.5, y: 1)
                    .padding(5)
            }
        }
        .aspectRatio(2 / 3, contentMode: .fit)
        .clipShape(RoundedRectangle(cornerRadius: 6))
        .overlay(RoundedRectangle(cornerRadius: 6).stroke(Palette.line2, lineWidth: 1))
        .overlay(alignment: .topTrailing) {
            if let rating = item.rating {
                Text("★ \(stars(Double(rating)))")
                    .font(Fonts.display(8, bold: true))
                    .foregroundStyle(Palette.ac2)
                    .padding(.horizontal, 4)
                    .padding(.vertical, 2)
                    .background(Color.black.opacity(0.66), in: RoundedRectangle(cornerRadius: 5))
                    .padding(4)
            }
        }
        .shadow(color: .black.opacity(0.45), radius: 8, y: 6)
    }
}

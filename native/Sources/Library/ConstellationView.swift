import SwiftUI

// The map: the archive as genre territories. It picks a genre and hands you
// back to the wall — the list of works in a genre is the wall's job.
// Nothing animates, so there is no frame loop to pause.
struct ConstellationView: View {
    private let store = LibraryStore.shared
    @Environment(\.dismiss) private var dismiss

    @State private var selected: Int?
    @State private var covers = 0
    @State private var last: CGPoint?
    @State private var moved = false
    @State private var pinching = false
    @State private var pinchZoom = 1.0
    @State private var pinchWorld = CGPoint.zero

    private static let ground = Color(red: 0x0a / 255, green: 0x0a / 255, blue: 0x0a / 255)

    var body: some View {
        GeometryReader { proxy in
            ZStack(alignment: .top) {
                Self.ground.ignoresSafeArea()

                Canvas(rendersAsynchronously: false) { context, size in
                    draw(&context, size: size)
                }
                .id(covers)
                .contentShape(Rectangle())
                .gesture(drag(proxy.size))
                .simultaneousGesture(pinch(proxy.size))
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
                    HudChip(text: hud)
                        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .bottomTrailing)
                        .padding(14)
                }
            }
            .onChange(of: proxy.size) { fit(proxy.size) }
            .onChange(of: store.loaded) { fit(proxy.size) }
            .onAppear { fit(proxy.size) }
        }
        .task { await store.load() }
        .task(id: store.loaded) { await warmCovers() }
    }

    private var map: ConstellationMap { store.map }

    private var hud: String {
        if let selected {
            let count = map.items[selected].genres.count
            return "\(count) \(plural(count, "genre")) hold this work"
        }
        return "\(map.items.count) works · \(map.territories.count) genres"
    }

    private func fit(_ size: CGSize) {
        guard store.loaded, !store.fitted, size.width > 1 else { return }
        store.camera = MapCamera.fitting(map.bounds, in: size)
        store.fitted = true
    }

    // Nothing redraws on a timer, so a cover arriving has to say so.
    private func warmCovers() async {
        for url in Set(map.items.compactMap(\.imageUrl)) where ImageCache.shared.cached(url) == nil {
            _ = await ImageCache.shared.image(url)
            covers += 1
        }
    }

    private func tap(_ point: CGPoint, in size: CGSize) {
        switch map.hit(world: store.camera.world(point, in: size)) {
        case .genre(let genre):
            store.filter(byGenre: genre)
            dismiss()
        case .cover(let index):
            // One tap rings this work's copies in every other genre, the next opens it.
            if selected == index {
                openItem(map.items[index].userMediaItemId)
            } else {
                selected = index
            }
        case nil:
            selected = nil
        }
    }

    private func drag(_ size: CGSize) -> some Gesture {
        DragGesture(minimumDistance: 0)
            .onChanged { value in
                guard !pinching else { return }
                if last == nil { last = value.startLocation; moved = false }
                let delta = CGPoint(x: value.location.x - last!.x, y: value.location.y - last!.y)
                if abs(delta.x) + abs(delta.y) > 3 { moved = true }
                store.camera.x += delta.x
                store.camera.y += delta.y
                last = value.location
            }
            .onEnded { value in
                if !pinching, !moved { tap(value.location, in: size) }
                last = nil
            }
    }

    private func pinch(_ size: CGSize) -> some Gesture {
        MagnifyGesture()
            .onChanged { value in
                let anchor = CGPoint(x: value.startAnchor.x * size.width, y: value.startAnchor.y * size.height)
                if !pinching {
                    pinching = true
                    pinchZoom = store.camera.z
                    pinchWorld = store.camera.world(anchor, in: size)
                }
                store.camera.z = min(max(pinchZoom * value.magnification, 0.1), 3)
                store.camera.x = anchor.x - size.width / 2 - pinchWorld.x * store.camera.z
                store.camera.y = anchor.y - size.height / 2 - pinchWorld.y * store.camera.z
            }
            .onEnded { _ in
                pinching = false
                last = nil
            }
    }

    private func draw(_ context: inout GraphicsContext, size: CGSize) {
        context.fill(Path(CGRect(origin: .zero, size: size)), with: .color(Self.ground))

        let origin = store.camera.screen(size)
        var world = context
        world.translateBy(x: origin.x, y: origin.y)
        world.scaleBy(x: store.camera.z, y: store.camera.z)

        let art = ConstellationMap.coverWidth * store.camera.z >= ConstellationMap.artWidth
        // Strokes are drawn in world space, so they need dividing back to hairlines.
        let hairline = 1 / store.camera.z
        let visible = CGRect(x: -origin.x / store.camera.z, y: -origin.y / store.camera.z,
                             width: size.width / store.camera.z, height: size.height / store.camera.z)
            .insetBy(dx: -ConstellationMap.coverHeight, dy: -ConstellationMap.coverHeight)
        for territory in map.territories {
            let colour = Color(red: territory.hue.0 / 255, green: territory.hue.1 / 255, blue: territory.hue.2 / 255)
            guard visible.intersects(CGRect(x: territory.x - territory.radius, y: territory.y - territory.radius,
                                            width: territory.radius * 2, height: territory.radius * 2)) else { continue }

            let hub = CGRect(x: territory.x - territory.hubRadius, y: territory.y - territory.hubRadius,
                             width: territory.hubRadius * 2, height: territory.hubRadius * 2)
            world.fill(Path(ellipseIn: hub), with: .radialGradient(
                Gradient(colors: [colour.opacity(0.95), colour.opacity(0.55)]),
                center: CGPoint(x: territory.x, y: territory.y),
                startRadius: 0, endRadius: territory.hubRadius))

            for cover in territory.covers {
                let rect = CGRect(x: territory.x + cover.x - ConstellationMap.coverWidth / 2,
                                  y: territory.y + cover.y - ConstellationMap.coverHeight / 2,
                                  width: ConstellationMap.coverWidth, height: ConstellationMap.coverHeight)
                guard visible.intersects(rect) else { continue }
                let lit = selected == nil || selected == cover.itemIndex
                var tile = world
                tile.opacity = lit ? 1 : 0.28
                if art {
                    drawArt(&tile, rect: rect, item: map.items[cover.itemIndex], hue: colour, hairline: hairline)
                } else {
                    tile.fill(Path(roundedRect: rect, cornerRadius: 2), with: .color(colour.opacity(0.85)))
                }
                if selected == cover.itemIndex {
                    tile.stroke(Path(roundedRect: rect, cornerRadius: 2), with: .color(Palette.ink), lineWidth: hairline * 2)
                }
            }

            // Zoomed into one territory its label is off-screen, so the orb
            // carries the name itself once there is room for it.
            let onOrb = territory.hubRadius * store.camera.z > 34
            var label = world
            label.opacity = selected == nil ? 1 : 0.4
            label.draw(Text(territory.genre.capitalized)
                .font(Fonts.title(onOrb ? territory.hubRadius * 0.34 : territory.labelSize))
                .foregroundStyle(onOrb ? Palette.bg : Palette.ink),
                       at: CGPoint(x: territory.x, y: territory.y + (onOrb ? 0 : territory.labelY)))
        }
    }

    private func drawArt(_ context: inout GraphicsContext, rect: CGRect, item: LibraryItem, hue: Color, hairline: Double) {
        context.drawLayer { layer in
            layer.clip(to: Path(roundedRect: rect, cornerRadius: 2))
            if let url = item.imageUrl, let image = ImageCache.shared.cached(url) {
                // Crop to fill, like CSS background-size: cover.
                let scale = max(rect.width / image.size.width, rect.height / image.size.height)
                let drawn = CGSize(width: image.size.width * scale, height: image.size.height * scale)
                layer.draw(layer.resolve(Image(uiImage: image)),
                           in: CGRect(x: rect.midX - drawn.width / 2, y: rect.midY - drawn.height / 2,
                                      width: drawn.width, height: drawn.height))
            } else {
                layer.fill(Path(rect), with: .color(Palette.accent(item.mediaType).opacity(0.8)))
            }
        }
        context.stroke(Path(roundedRect: rect, cornerRadius: 2), with: .color(hue.opacity(0.8)), lineWidth: hairline)
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
    }
}

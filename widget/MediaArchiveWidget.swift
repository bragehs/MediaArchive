// In-progress widget. Renders the snapshot the app writes into the shared
// App Group container; it never touches the database or the network.
// Tapping a row deep-links into the app's log dialog for that item.

import WidgetKit
import SwiftUI

let appGroupId = "group.no.norapps.mediaarchive"

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
}

struct SnapshotItem: Decodable, Identifiable {
    let id: Int            // UserMediaItemId — the app's routing key
    let title: String
    let kind: String       // "Book" | "Game" | "Film" | "Show"
    let progressLabel: String
    let percent: Double?   // nil when the work has no known length
    let cover: String?     // filename under widget/covers/ in the container
}

struct Snapshot: Decodable {
    let items: [SnapshotItem]
}

func containerURL() -> URL? {
    FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: appGroupId)
}

func loadItems() -> [SnapshotItem] {
    guard let container = containerURL(),
          let data = try? Data(contentsOf: container.appendingPathComponent("widget/snapshot.json")),
          let snapshot = try? JSONDecoder().decode(Snapshot.self, from: data)
    else { return [] }
    return snapshot.items
}

func coverImage(_ name: String?) -> UIImage? {
    guard let name, let container = containerURL() else { return nil }
    return UIImage(contentsOfFile: container.appendingPathComponent("widget/covers/\(name)").path)
}

extension SnapshotItem {
    // Matches the app's per-type accents (wwwroot/app.css --book/--game/--movie/--show).
    var accent: Color {
        switch kind {
        case "Book": return Palette.book
        case "Game": return Palette.game
        case "Film": return Palette.movie
        default:     return Palette.show
        }
    }

    static let samples = [
        SnapshotItem(id: 1, title: "The Name of the Wind", kind: "Book",
                     progressLabel: "312 pages", percent: 47, cover: nil),
        SnapshotItem(id: 2, title: "Hollow Knight", kind: "Game",
                     progressLabel: "18 hours", percent: 60, cover: nil),
        SnapshotItem(id: 3, title: "Severance", kind: "Show",
                     progressLabel: "6 episodes", percent: 33, cover: nil),
        SnapshotItem(id: 4, title: "Blade Runner", kind: "Film",
                     progressLabel: "45 minutes", percent: 38, cover: nil),
    ]
}

struct InProgressEntry: TimelineEntry {
    let date: Date
    let items: [SnapshotItem]
}

struct Provider: TimelineProvider {
    func placeholder(in context: Context) -> InProgressEntry {
        InProgressEntry(date: .now, items: SnapshotItem.samples)
    }

    func getSnapshot(in context: Context, completion: @escaping (InProgressEntry) -> Void) {
        let items = context.isPreview ? SnapshotItem.samples : loadItems()
        completion(InProgressEntry(date: .now, items: items))
    }

    // .never: the app pushes a reload whenever it writes a new snapshot,
    // and nothing can change while the app isn't running.
    func getTimeline(in context: Context, completion: @escaping (Timeline<InProgressEntry>) -> Void) {
        completion(Timeline(entries: [InProgressEntry(date: .now, items: loadItems())], policy: .never))
    }
}

struct ItemCell: View {
    let item: SnapshotItem

    var body: some View {
        Link(destination: URL(string: "mediaarchive://log/\(item.id)")!) {
            VStack(spacing: 4) {
                cover
                Text(item.title)
                    .font(.caption2.weight(.semibold))
                    .foregroundStyle(.white)
                    .lineLimit(1)
                Text(percentText)
                    .font(.caption2.weight(.bold))
                    .foregroundStyle(item.accent)
            }
            .frame(width: 62)
        }
    }

    private var percentText: String {
        if let p = item.percent { return "\(Int(p.rounded()))%" }
        return "–"
    }

    @ViewBuilder private var cover: some View {
        if let ui = coverImage(item.cover) {
            Image(uiImage: ui)
                .resizable()
                .aspectRatio(contentMode: .fill)
                .frame(width: 58, height: 84)
                .clipShape(RoundedRectangle(cornerRadius: 6))
        } else {
            RoundedRectangle(cornerRadius: 6)
                .fill(item.accent.opacity(0.22))
                .frame(width: 58, height: 84)
                .overlay(
                    Text(String(item.title.prefix(1)))
                        .font(.title3.weight(.bold))
                        .foregroundStyle(item.accent)
                )
        }
    }
}

struct InProgressView: View {
    var entry: InProgressEntry

    var body: some View {
        Group {
            if entry.items.isEmpty {
                Text("Nothing in progress")
                    .font(.caption)
                    .foregroundStyle(.white.opacity(0.45))
            } else {
                // One centered shelf of up to 4 covers; a small "+N" marks
                // anything the row can't fit (least recent first to go).
                HStack(alignment: .center, spacing: 10) {
                    ForEach(entry.items.prefix(4)) { item in
                        ItemCell(item: item)
                    }
                    if entry.items.count > 4 {
                        Text("+\(entry.items.count - 4)")
                            .font(.caption.weight(.bold))
                            .foregroundStyle(.white.opacity(0.5))
                    }
                }
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .containerBackground(Palette.bg, for: .widget)
    }
}

struct InProgressWidget: Widget {
    var body: some WidgetConfiguration {
        StaticConfiguration(kind: "InProgressWidget", provider: Provider()) { entry in
            InProgressView(entry: entry)
        }
        .configurationDisplayName("In progress")
        .description("What you're reading, playing and watching right now.")
        .supportedFamilies([.systemMedium])
    }
}

@main
struct MediaArchiveWidgets: WidgetBundle {
    var body: some Widget {
        InProgressWidget()
        SessionLiveActivity()
    }
}

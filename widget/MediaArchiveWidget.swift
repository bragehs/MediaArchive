// In-progress widget. Renders the snapshot the app writes into the shared
// App Group container; it never touches the database or the network.
// Tapping a row deep-links into the app's log dialog for that item.

import WidgetKit
import SwiftUI

let appGroupId = "group.no.norapps.mediaarchive"

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
        case "Book": return Color(red: 0x8B / 255, green: 0xBE / 255, blue: 0x5A / 255)
        case "Game": return Color(red: 0xD6 / 255, green: 0xA7 / 255, blue: 0x4A / 255)
        case "Film": return Color(red: 0xA9 / 255, green: 0xC6 / 255, blue: 0x8E / 255)
        default:     return Color(red: 0x8A / 255, green: 0x7F / 255, blue: 0x6A / 255)
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
            HStack(spacing: 8) {
                cover
                VStack(alignment: .leading, spacing: 3) {
                    Text(item.title)
                        .font(.caption.weight(.semibold))
                        .foregroundStyle(.white)
                        .lineLimit(1)
                    Text(progressText)
                        .font(.caption2)
                        .foregroundStyle(.white.opacity(0.55))
                        .lineLimit(1)
                    bar
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        }
    }

    private var progressText: String {
        if let p = item.percent {
            return "\(item.progressLabel) · \(Int(p.rounded()))%"
        }
        return item.progressLabel
    }

    @ViewBuilder private var cover: some View {
        if let ui = coverImage(item.cover) {
            Image(uiImage: ui)
                .resizable()
                .aspectRatio(contentMode: .fill)
                .frame(width: 30, height: 44)
                .clipShape(RoundedRectangle(cornerRadius: 4))
        } else {
            RoundedRectangle(cornerRadius: 4)
                .fill(item.accent.opacity(0.22))
                .frame(width: 30, height: 44)
                .overlay(
                    Text(String(item.title.prefix(1)))
                        .font(.caption.weight(.bold))
                        .foregroundStyle(item.accent)
                )
        }
    }

    private var bar: some View {
        GeometryReader { geo in
            ZStack(alignment: .leading) {
                Capsule().fill(.white.opacity(0.12))
                if let p = item.percent {
                    Capsule()
                        .fill(item.accent)
                        .frame(width: max(3, geo.size.width * min(p, 100) / 100))
                }
            }
        }
        .frame(height: 3)
    }
}

struct InProgressView: View {
    var entry: InProgressEntry

    private let columns = [
        GridItem(.flexible(), spacing: 14),
        GridItem(.flexible(), spacing: 14),
    ]

    var body: some View {
        Group {
            if entry.items.isEmpty {
                Text("Nothing in progress")
                    .font(.caption)
                    .foregroundStyle(.white.opacity(0.45))
            } else {
                LazyVGrid(columns: columns, spacing: 10) {
                    ForEach(entry.items.prefix(4)) { item in
                        ItemCell(item: item)
                    }
                }
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .containerBackground(Color(red: 0x0A / 255, green: 0x0A / 255, blue: 0x0A / 255), for: .widget)
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
    }
}

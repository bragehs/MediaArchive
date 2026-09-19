import SwiftUI

// The open interval: this week's effort, what's open ranked by progress, the
// on-deck rail, and the last thing closed.
struct HomeView: View {
    @State private var store = HomeStore()
    @State private var confetti = false

    private let onDeckCap = 8

    var body: some View {
        ScrollView {
            switch store.page {
            case .loading:
                Aside("Loading…").padding(.vertical, 10)
            case .failed(let message):
                Notice(text: message)
            case .loaded(let page):
                content(page)
            }
        }
        .page()
        .task { await store.load() }
        .sheet(item: $store.logTarget) { target in
            LogProgressSheet(entryId: target.openEntryId, title: target.title, mediaType: target.mediaType) { finished in
                store.logTarget = nil
                confetti = finished
                Task { await store.load() }
            }
        }
        .confetti($confetti)
    }

    @ViewBuilder
    private func content(_ page: HomePage) -> some View {
        VStack(alignment: .leading, spacing: 0) {
            WeekStrip(week: page.weekly)

            SectionHead("Open now", right: "\(page.openNow.count) open")
            if let error = store.error {
                Notice(text: error).padding(.vertical, 6)
            }
            if page.openNow.isEmpty {
                Aside("Nothing in progress right now.").padding(.vertical, 10)
            } else {
                ForEach(page.openNow) { item in
                    OpenNowRow(item: item, sessionLabel: store.sessionLabel(item), busy: store.saving,
                               onLog: { store.logTarget = item },
                               onSession: { Task { await store.toggleSession(item) } })
                }
            }

            SectionHead("On deck")
            if page.onDeck.isEmpty {
                Aside("Nothing lined up.").padding(.vertical, 10)
            } else {
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: 11) {
                        ForEach(page.onDeck.prefix(onDeckCap)) { card in
                            Button { openItem(card.userMediaItemId) } label: {
                                CoverTile(url: card.imageUrl, title: card.title, width: 82)
                            }
                            .buttonStyle(.plain)
                        }
                        if page.onDeck.count > onDeckCap {
                            Text("+\(page.onDeck.count - onDeckCap)")
                                .font(Fonts.display(13, bold: true))
                                .foregroundStyle(Palette.ac)
                                .padding(.horizontal, 6)
                        }
                    }
                    .padding(.bottom, 6)
                }
            }

            if let closed = page.justClosed {
                JustClosedRow(item: closed)
            }
        }
    }
}

private struct WeekStrip: View {
    let week: WeeklyActivity

    var body: some View {
        HStack(spacing: 0) {
            ForEach(Array(week.buckets.enumerated()), id: \.offset) { index, bucket in
                VStack(alignment: .leading, spacing: 0) {
                    HStack(alignment: .firstTextBaseline, spacing: 3) {
                        Text(value(bucket))
                            .font(Fonts.title(26))
                            .foregroundStyle(Palette.ac)
                        Eyebrow(unit(bucket.unit), size: 10, tracking: 0.1, bold: false)
                    }
                    Eyebrow(bucket.bucket.rawValue, size: 8.5, tracking: 0.12, bold: false)
                        .padding(.top, 7)
                    Aside("\(bucket.itemsTouched) \(plural(bucket.itemsTouched, "item"))", size: 11, color: Palette.muted)
                        .padding(.top, 2)
                }
                .padding(.vertical, 12)
                .padding(.leading, index == 0 ? 0 : 16)
                .padding(.trailing, index == week.buckets.count - 1 ? 0 : 16)
                .frame(maxWidth: .infinity, alignment: .leading)
                .overlay(alignment: .trailing) {
                    if index < week.buckets.count - 1 {
                        Rectangle().fill(Palette.line).frame(width: 1)
                    }
                }
            }
        }
        .overlay(alignment: .bottomTrailing) {
            Eyebrow("This week", size: 8, tracking: 0.1, bold: false).padding(.bottom, 12)
        }
        .overlay(alignment: .top) { Rectangle().fill(Palette.ink).frame(height: 2) }
        .overlay(alignment: .bottom) { Rectangle().fill(Palette.line).frame(height: 1) }
        .padding(.top, 2)
    }

    private func value(_ bucket: WeeklyBucketStat) -> String {
        bucket.unit == "pages" ? String(Int(bucket.value)) : trimmed(bucket.value)
    }

    private func unit(_ unit: String) -> String {
        switch unit {
        case "pages": "P"
        case "hours", "h": "H"
        case "minutes": "M"
        default: unit.prefix(1).uppercased()
        }
    }
}

private struct OpenNowRow: View {
    let item: OpenNowItem
    let sessionLabel: String?
    let busy: Bool
    let onLog: () -> Void
    let onSession: () -> Void

    var body: some View {
        HStack(alignment: .top, spacing: 13) {
            Button { openItem(item.userMediaItemId) } label: {
                CoverTile(url: item.imageUrl, title: item.title, width: 62)
            }
            .buttonStyle(.plain)

            VStack(alignment: .leading, spacing: 0) {
                Button { openItem(item.userMediaItemId) } label: {
                    Text(item.title)
                        .font(Fonts.title(16))
                        .foregroundStyle(Palette.ink)
                        .multilineTextAlignment(.leading)
                }
                .buttonStyle(.plain)

                Eyebrow("touched \(ago(item.daysSinceTouched))" + (item.progress.map { " · \(Int($0.rounded()))%" } ?? ""),
                        size: 9, color: Palette.muted, tracking: 0.08, bold: false)
                    .padding(.top, 7)

                if let progress = item.progress {
                    ProgressBar(percent: progress).padding(.top, 9)
                }

                HStack(spacing: 4) {
                    (Text("open ").italic() + Text("\(item.daysOpen)").foregroundStyle(Palette.muted) + Text(" \(plural(item.daysOpen, "day"))").italic())
                        .font(Fonts.serif(11, italic: true))
                        .foregroundStyle(Palette.dim)
                    Button { onLog() } label: {
                        Eyebrow("Log →", size: 9, color: Palette.ac, tracking: 0.1)
                    }
                    .buttonStyle(.plain)
                    .padding(.leading, 4)
                    if let sessionLabel {
                        Button { onSession() } label: {
                            Eyebrow(sessionLabel, size: 9, color: Palette.ac2, tracking: 0.1)
                        }
                        .buttonStyle(.plain)
                        .disabledLook(busy)
                        .padding(.leading, 8)
                    }
                }
                .padding(.top, 8)
            }
        }
        .padding(.vertical, 14)
        .overlay(alignment: .bottom) { HairlineRule() }
    }
}

private struct JustClosedRow: View {
    let item: JustClosedItem

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(alignment: .firstTextBaseline, spacing: 9) {
                Button { openItem(item.userMediaItemId) } label: {
                    Text(item.title).font(Fonts.title(15)).foregroundStyle(Palette.ink)
                }
                .buttonStyle(.plain)
                if let rating = item.rating {
                    StarsInline(tenScale: rating)
                }
            }
            Eyebrow("\(item.creator) · closed \(ago(item.daysSinceClosed))", size: 8.5, tracking: 0.08, bold: false)
        }
        .padding(.top, 13)
        .frame(maxWidth: .infinity, alignment: .leading)
        .overlay(alignment: .top) { Rectangle().fill(Palette.ink).frame(height: 2) }
        .padding(.top, 18)
    }
}

@MainActor
func openItem(_ userMediaItemId: Int) {
    Router.shared.push(.item(userMediaItemId))
}

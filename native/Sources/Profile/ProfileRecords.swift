import SwiftUI

// One pane per medium: its totals in its own unit, how it reached you, its
// progression, and the extremes that only make sense for that medium.
struct RecordsPane: View {
    let snapshot: ProfileSnapshot
    let store: ProfileStore

    @Environment(\.lexicon) private var lexicon

    var body: some View {
        HStack(spacing: 6) {
            ForEach(snapshot.panels, id: \.mediaType) { panel in
                let on = store.currentPanel?.mediaType == panel.mediaType
                Button { store.recordsTab = panel.mediaType } label: {
                    Eyebrow(lexicon.label(panel.mediaType) + "s", size: 10.5,
                            color: on ? Palette.onAc : Palette.dim, tracking: 0.08, bold: false)
                        .padding(.horizontal, 11)
                        .padding(.vertical, 5)
                        .background(on ? Palette.ac : .clear, in: RoundedRectangle(cornerRadius: 10))
                        .overlay(RoundedRectangle(cornerRadius: 10).stroke(on ? Palette.ac : Palette.line, lineWidth: 1))
                }
                .buttonStyle(.plain)
            }
        }
        .padding(.bottom, 14)

        if let panel = store.currentPanel {
            stats(panel)
            reach(panel)
            progression(panel)
            records(panel)
        }
    }

    @ViewBuilder
    private func stats(_ panel: TypePanel) -> some View {
        if !panel.stats.isEmpty {
            HStack(spacing: 0) {
                ForEach(Array(panel.stats.enumerated()), id: \.offset) { index, stat in
                    VStack(alignment: .leading, spacing: 5) {
                        Text(stat.value).font(Fonts.title(21)).foregroundStyle(Palette.ac)
                        Eyebrow(stat.label, size: 8, tracking: 0.12, bold: false)
                    }
                    .padding(.top, 10)
                    .padding(.horizontal, index == 0 ? 0 : 14)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .overlay(alignment: .trailing) {
                        if index < panel.stats.count - 1 { Rectangle().fill(Palette.line2).frame(width: 1) }
                    }
                }
            }
            .overlay(alignment: .top) { HairlineRule(color: Palette.line2) }
            .padding(.bottom, 16)
        }
    }

    // Print against audiobook, console against PC — the same shape read four ways.
    @ViewBuilder
    private func reach(_ panel: TypePanel) -> some View {
        if panel.contexts.count > 1 {
            Eyebrow("How it reached you", size: 8.5, tracking: 0.12, bold: false)
                .padding(.bottom, 8)
            SplitBar(slices: slices(panel), height: 8)
            Text(panel.contexts.map { share in
                "\(share.context.map { lexicon.label($0).lowercased() } ?? "unrecorded") \(share.passes)"
            }.joined(separator: " · "))
                .font(Fonts.serif(11.5, italic: true))
                .foregroundStyle(Palette.muted)
                .padding(.top, 7)
                .padding(.bottom, 18)
        }
    }

    // The medium's own accent, stepping down; an unrecorded pass is grey rather
    // than absent, because it is a gap to fill.
    private func slices(_ panel: TypePanel) -> [BarSlice] {
        panel.contexts.enumerated().map { index, share in
            BarSlice(id: share.context?.rawValue ?? "unrecorded",
                     color: share.context == nil
                        ? Palette.dim.opacity(0.3)
                        : Palette.accent(panel.mediaType).opacity(max(0.3, 1 - Double(index) * 0.34)),
                     value: Double(share.passes))
        }
    }

    @ViewBuilder
    private func progression(_ panel: TypePanel) -> some View {
        if panel.weekly.contains(where: { $0.value > 0 }) || panel.yearly.contains(where: { $0.value > 0 }) {
            HStack(spacing: 5) {
                Spacer()
                scopeButton(String(DateOnly.today.year), on: !store.allTime) { store.allTime = false }
                scopeButton("All time", on: store.allTime) { store.allTime = true }
            }
            .padding(.bottom, 10)

            if store.allTime {
                let step = max(1, Int((Double(panel.yearly.count) / 8).rounded(.up)))
                let first = panel.yearly.first?.year ?? 0
                BarChart(values: panel.yearly.map(\.value),
                         labels: panel.yearly.map { ($0.year - first) % step == 0 ? String(String($0.year).suffix(2)) : nil })
                Aside("\(panel.unit) / year · all time", size: 11).padding(.top, 18).padding(.bottom, 6)
            } else {
                BarChart(values: panel.weekly.map(\.value),
                         labels: panel.weekly.map { $0.weekStart.day <= 7 ? String($0.weekStart.formatted("MMM").prefix(1)) : nil })
                Aside("\(panel.unit) / week · \(String(DateOnly.today.year))", size: 11).padding(.top, 18).padding(.bottom, 6)
            }
        }
    }

    @ViewBuilder
    private func records(_ panel: TypePanel) -> some View {
        VStack(spacing: 0) {
            ForEach(Array(panel.records.enumerated()), id: \.offset) { _, record in
                Button { openItem(record.userMediaItemId) } label: {
                    RecordRow(label: record.label, value: record.value, title: record.title)
                }
                .buttonStyle(.plain)
            }
            if let busiest = snapshot.busiestMonth {
                RecordRow(label: "Busiest month", value: "\(busiest.logs) logs",
                          title: "\(monthName(busiest.month)) \(String(busiest.year)) · everything")
            }
        }
    }

    private func scopeButton(_ title: String, on: Bool, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Eyebrow(title, size: 9, color: on ? Palette.ink : Palette.dim, tracking: 0.08, bold: false)
                .padding(.horizontal, 9)
                .padding(.vertical, 3)
                .overlay(RoundedRectangle(cornerRadius: 10).stroke(on ? Palette.ac : Palette.line2, lineWidth: 1))
        }
        .buttonStyle(.plain)
    }

    private func monthName(_ month: Int) -> String {
        DateOnly(year: 2000, month: month, day: 1).formatted("MMMM")
    }
}

private struct RecordRow: View {
    let label: String
    let value: String
    let title: String

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: 10) {
            Eyebrow(label, size: 8.5, tracking: 0.1, bold: false).frame(width: 104, alignment: .leading)
            Text(value).font(Fonts.title(14)).foregroundStyle(Palette.ac).lineLimit(1)
            Aside(title, size: 12, color: Palette.muted)
                .lineLimit(1)
                .frame(maxWidth: .infinity, alignment: .trailing)
        }
        .padding(.vertical, 10)
        .overlay(alignment: .bottom) { HairlineRule(color: Palette.line2) }
    }
}

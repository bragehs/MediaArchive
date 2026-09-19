import SwiftUI

// The one figure that crosses all four media: minutes, rendered with a leading
// `≈` whenever any of it was derived from Length rather than logged.
struct TimeSpentHero: View {
    let snapshot: ProfileSnapshot

    @Environment(\.lexicon) private var lexicon

    private var spent: TimeSpent { snapshot.timeSpent }
    private var totalMinutes: Double { spent.actualMinutes + spent.estimatedMinutes }
    private var derived: Bool { spent.estimatedMinutes > 0 }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(alignment: .firstTextBaseline, spacing: 4) {
                if derived {
                    Text("≈").font(Fonts.title(25)).foregroundStyle(Palette.ac.opacity(0.6))
                }
                Text(hours(totalMinutes)).font(Fonts.title(38)).foregroundStyle(Palette.ac)
                Eyebrow("hours", size: 10, tracking: 0.1, bold: false)
            }

            if derived {
                Aside("≈\(hours(spent.estimatedMinutes)) h of it estimated from length",
                      size: 11, color: Palette.muted)
                    .padding(.top, 5)
            }

            if !mix.isEmpty {
                SplitBar(slices: mix.map {
                    BarSlice(id: $0.type.rawValue, color: Palette.accent($0.type), value: $0.minutes)
                })
                .padding(.top, 14)

                HStack(spacing: 0) {
                    ForEach(mix) { slice in
                        VStack(alignment: .leading, spacing: 6) {
                            HStack(spacing: 6) {
                                RoundedRectangle(cornerRadius: 2)
                                    .fill(Palette.accent(slice.type))
                                    .frame(width: 8, height: 8)
                                Text("\(hours(slice.minutes)) h")
                                    .font(Fonts.title(16))
                                    .foregroundStyle(Palette.ink)
                            }
                            Eyebrow(lexicon.label(slice.type) + "s", size: 8, tracking: 0.12, bold: false)
                        }
                        .frame(maxWidth: .infinity, alignment: .leading)
                    }
                }
                .padding(.top, 11)
            }

            if columns.count > 1 {
                YearMix(columns: columns).padding(.top, 18)
            }

            facts.padding(.top, 18)
        }
        .padding(.vertical, 16)
        .frame(maxWidth: .infinity, alignment: .leading)
        .overlay(alignment: .top) { Rectangle().fill(Palette.ink).frame(height: 2) }
        .overlay(alignment: .bottom) { HairlineRule() }
        .padding(.top, 2)
    }

    private var facts: some View {
        HStack(spacing: 0) {
            statCell(String(snapshot.itemsLogged), "logged", first: true)
            rule
            statCell(String(snapshot.finished), "finished")
            if let rating = snapshot.avgRating {
                rule
                statCell("★ \(stars(rating))", "of \(snapshot.ratedCount) rated", accent: Palette.ac2)
            }
        }
        .overlay(alignment: .top) { HairlineRule(color: Palette.line2) }
    }

    private func statCell(_ value: String, _ label: String,
                          first: Bool = false, accent: Color = Palette.ac) -> some View {
        VStack(alignment: .leading, spacing: 5) {
            Text(value).font(Fonts.title(21)).foregroundStyle(accent)
            Eyebrow(label, size: 8, tracking: 0.12, bold: false)
        }
        .padding(.top, 11)
        .padding(.leading, first ? 0 : 14)
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var rule: some View {
        Rectangle().fill(Palette.line2).frame(width: 1).padding(.top, 11)
    }

    private func hours(_ minutes: Double) -> String {
        let value = minutes / 60
        return value >= 10 ? grouped(value.rounded()) : trimmed(value)
    }

    private var mix: [MediumSlice] {
        Dictionary(grouping: spent.buckets, by: \.mediaType)
            .map { MediumSlice(type: $0.key, minutes: $0.value.reduce(0) { $0 + $1.minutes }) }
            .filter { $0.minutes > 0 }
            .sorted { $0.minutes > $1.minutes }
    }

    // Only years you actually logged something in. Carrying the empty ones between
    // them turned a decade of nothing into most of the chart.
    private var columns: [YearColumn] {
        Dictionary(grouping: spent.buckets, by: \.year)
            .map { year, buckets in
                YearColumn(year: year, slices: buckets
                    .map { MediumSlice(type: $0.mediaType, minutes: $0.minutes) }
                    .sorted { $0.minutes > $1.minutes })
            }
            .filter { $0.total > 0 }
            .sorted { $0.year < $1.year }
    }
}

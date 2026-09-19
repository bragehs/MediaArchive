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

            Eyebrow("across \(spent.items) \(plural(spent.items, "item"))", size: 8.5, tracking: 0.12, bold: false)
                .padding(.top, 9)

            if !mix.isEmpty {
                SplitBar(slices: mix.map {
                    BarSlice(id: $0.type.rawValue, color: Palette.accent($0.type), value: $0.minutes)
                })
                .padding(.top, 14)

                Text(mix.map { "\(lexicon.label($0.type).lowercased())s \(hours($0.minutes)) h" }
                        .joined(separator: " · "))
                    .font(Fonts.serif(11.5, italic: true))
                    .foregroundStyle(Palette.muted)
                    .padding(.top, 7)
            }

            if let caveat {
                Aside(caveat, size: 11, color: Palette.muted).padding(.top, 3)
            }

            if columns.count > 1 {
                YearMix(columns: columns).padding(.top, 16)
            }

            facts.padding(.top, 14)
        }
        .padding(.vertical, 16)
        .frame(maxWidth: .infinity, alignment: .leading)
        .overlay(alignment: .top) { Rectangle().fill(Palette.ink).frame(height: 2) }
        .overlay(alignment: .bottom) { HairlineRule() }
        .padding(.top, 2)
    }

    // Says what the figure is made of: a derived share is not a measured one, and
    // an item that couldn't convert is missing from the total, not zero in it.
    private var caveat: String? {
        var parts: [String] = []
        if derived { parts.append("≈\(hours(spent.estimatedMinutes)) h of it estimated from length") }
        if spent.withoutLength > 0 {
            parts.append("\(spent.withoutLength) \(plural(spent.withoutLength, "item")) left out for want of a length")
        }
        return parts.isEmpty ? nil : parts.joined(separator: " · ")
    }

    private var facts: Text {
        var line = Text("\(snapshot.itemsLogged)").fontWeight(.bold).foregroundStyle(Palette.ink)
            + Text(" logged · ").italic()
            + Text("\(snapshot.finished)").fontWeight(.bold).foregroundStyle(Palette.ink)
            + Text(" finished").italic()
        if let rating = snapshot.avgRating {
            line = line + Text(" · ").italic()
                + Text("★ \(stars(rating))").fontWeight(.bold).foregroundStyle(Palette.ac2)
                + Text(" of \(snapshot.ratedCount) rated").italic()
        }
        return line.font(Fonts.serif(11.5, italic: true)).foregroundStyle(Palette.muted)
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

    // Empty years are kept: a decade you logged nothing in is a fact about you,
    // and dropping it would make a long gap look like a short one.
    private var columns: [YearColumn] {
        let years = spent.buckets.map(\.year)
        guard let first = years.min(), let last = years.max() else { return [] }
        let byYear = Dictionary(grouping: spent.buckets, by: \.year)
        return (first...last).map { year in
            YearColumn(year: year, slices: (byYear[year] ?? [])
                .map { MediumSlice(type: $0.mediaType, minutes: $0.minutes) }
                .sorted { $0.minutes > $1.minutes })
        }
    }
}

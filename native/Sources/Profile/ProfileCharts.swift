import SwiftUI

struct BarSlice: Identifiable {
    let id: String
    let color: Color
    let value: Double
}

// One bar cut into proportional segments: the archive's minutes by medium, or a
// medium's passes by the context they reached you through.
struct SplitBar: View {
    let slices: [BarSlice]
    var height: CGFloat = 9

    var body: some View {
        let total = max(1, slices.reduce(0) { $0 + $1.value })
        GeometryReader { geometry in
            let track = max(0, geometry.size.width - CGFloat(max(0, slices.count - 1)))
            HStack(spacing: 1) {
                ForEach(slices) { slice in
                    slice.color.frame(width: max(2, track * slice.value / total))
                }
            }
        }
        .frame(height: height)
        .clipShape(Capsule())
    }
}

// One bar per bucket, normalised to the tallest; an optional label under each.
struct BarChart: View {
    let values: [Double]
    let labels: [String?]

    var body: some View {
        let peak = max(1, values.max() ?? 1)
        HStack(alignment: .bottom, spacing: 2) {
            ForEach(Array(values.enumerated()), id: \.offset) { index, value in
                VStack(spacing: 0) {
                    Spacer(minLength: 0)
                    UnevenRoundedRectangle(topLeadingRadius: 2, topTrailingRadius: 2)
                        .fill(Palette.ac.opacity(0.85))
                        .frame(height: max(1, 74 * value / peak))
                }
                .frame(maxWidth: .infinity)
                .overlay(alignment: .bottomLeading) {
                    if let label = labels[index] {
                        Text(label).font(Fonts.display(8)).foregroundStyle(Palette.dim).offset(y: 14)
                    }
                }
            }
        }
        .frame(height: 74)
        .overlay(alignment: .bottom) { HairlineRule(color: Palette.line2) }
    }
}

struct MediumSlice: Identifiable {
    let type: MediaType
    let minutes: Double
    var id: MediaType { type }
}

struct YearColumn: Identifiable {
    let year: Int
    let slices: [MediumSlice]
    var id: Int { year }
    var total: Double { slices.reduce(0) { $0 + $1.minutes } }
}

// The archive's minutes on a time axis, stacked by medium — how the mix shifted.
struct YearMix: View {
    let columns: [YearColumn]

    var body: some View {
        let peak = max(1, columns.map(\.total).max() ?? 1)
        let step = max(1, Int((Double(columns.count) / 6).rounded(.up)))
        VStack(alignment: .leading, spacing: 6) {
            HStack(alignment: .bottom, spacing: 3) {
                ForEach(columns) { column in
                    VStack(spacing: 1) {
                        Spacer(minLength: 0)
                        ForEach(column.slices) { slice in
                            Palette.accent(slice.type)
                                .frame(height: max(1, 56 * slice.minutes / peak))
                        }
                    }
                    .frame(maxWidth: .infinity)
                    .clipShape(UnevenRoundedRectangle(topLeadingRadius: 2, topTrailingRadius: 2))
                }
            }
            .frame(height: 56)
            .overlay(alignment: .bottom) { HairlineRule(color: Palette.line2) }

            HStack(spacing: 3) {
                ForEach(Array(columns.enumerated()), id: \.element.id) { index, column in
                    // Anchored to the newest year, not the oldest: the last column is
                    // the one being read, and it has to be the one that is labelled.
                    Text((columns.count - 1 - index) % step == 0
                         ? String(String(column.year).suffix(2)) : "")
                        .font(Fonts.display(8))
                        .foregroundStyle(Palette.dim)
                        .frame(maxWidth: .infinity)
                }
            }
        }
    }
}

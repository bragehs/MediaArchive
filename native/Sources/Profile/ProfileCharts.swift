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
                    // Capped, then centred in an even cell: with the empty years
                    // dropped a handful of columns would otherwise stretch into slabs.
                    .frame(maxWidth: 46)
                    .clipShape(UnevenRoundedRectangle(topLeadingRadius: 2, topTrailingRadius: 2))
                    .frame(maxWidth: .infinity)
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

// One item, one bar: a value against the row set's peak, with an optional
// reference tick — your median, or this item's own estimate.
struct ItemBarRow: View {
    let title: String
    let fraction: Double
    var marker: Double? = nil
    let value: String
    var trailing: String? = nil

    var body: some View {
        HStack(spacing: 9) {
            Text(title)
                .font(Fonts.display(11.5))
                .foregroundStyle(Palette.muted)
                .lineLimit(1)
                .frame(maxWidth: .infinity, alignment: .leading)

            GeometryReader { geometry in
                ZStack(alignment: .leading) {
                    Capsule().fill(Palette.well)
                    Capsule()
                        .fill(Palette.ac.opacity(0.8))
                        .frame(width: max(2, geometry.size.width * clamped(fraction)))
                    if let marker {
                        Rectangle()
                            .fill(Palette.ac2)
                            .frame(width: 1.5)
                            .offset(x: geometry.size.width * clamped(marker))
                    }
                }
            }
            .frame(width: 80, height: 7)

            Text(value)
                .font(Fonts.title(12))
                .foregroundStyle(Palette.ink)
                .frame(width: 40, alignment: .trailing)

            // Always drawn, even when empty: a missing slot would shift the row.
            Text(trailing ?? "")
                .font(Fonts.serif(11, italic: true))
                .foregroundStyle(Palette.dim)
                .frame(width: 54, alignment: .trailing)
        }
        .padding(.vertical, 7)
    }

    private func clamped(_ value: Double) -> Double { min(max(value, 0), 1) }
}

// A chart's explanation, out of the way until asked for.
struct InfoDot: View {
    let text: String

    @State private var open = false

    var body: some View {
        Button { open = true } label: {
            Text("?")
                .font(Fonts.display(9, bold: true))
                .foregroundStyle(Palette.dim)
                .frame(width: 15, height: 15)
                .overlay(Circle().stroke(Palette.line, lineWidth: 1))
        }
        .buttonStyle(.plain)
        .popover(isPresented: $open) {
            Text(text)
                .font(Fonts.serif(12.5, italic: true))
                .foregroundStyle(Palette.muted)
                // A fixed width plus vertical fixedSize, or the popover clips the
                // copy to one line's worth instead of growing to fit it.
                .fixedSize(horizontal: false, vertical: true)
                .padding(14)
                .frame(width: 236)
                .presentationCompactAdaptation(.popover)
                .presentationBackground(Palette.panel)
        }
    }
}

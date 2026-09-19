import SwiftUI

@MainActor
@Observable
final class DiaryStore {
    var years: Loadable<[Int]> = .loading
    var year: Int?
    var data: Loadable<DiaryYear> = .loading

    func load() async {
        do {
            let index = try await api.diary()
            years = .loaded(index.years)
            if let current = index.current {
                year = current.year
                data = .loaded(current)
            }
        } catch {
            years = .failed(error.localizedDescription)
        }
    }

    func show(_ year: Int) async {
        self.year = year
        data = .loading
        do {
            data = .loaded(try await api.diaryYear(YearArgs(year: year)))
        } catch {
            data = .failed(error.localizedDescription)
        }
    }
}

// A year of months, each a bar against the busiest month and a rail of what
// was touched, drilling into the month's day-by-day journal.
struct DiaryView: View {
    @State private var store = DiaryStore()

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                switch store.years {
                case .loading:
                    Aside("Loading…").padding(.vertical, 10)
                case .failed(let message):
                    Notice(text: message)
                case .loaded(let years) where years.isEmpty:
                    Aside("Nothing logged yet.").padding(.vertical, 10)
                case .loaded(let years):
                    if years.count > 1 {
                        ScrollView(.horizontal, showsIndicators: false) {
                            HStack(spacing: 6) {
                                ForEach(years, id: \.self) { year in
                                    Button { Task { await store.show(year) } } label: {
                                        Text(String(year))
                                            .font(Fonts.display(11))
                                            .tracking(0.9)
                                            .foregroundStyle(year == store.year ? Palette.onAc : Palette.dim)
                                            .padding(.horizontal, 11)
                                            .padding(.vertical, 5)
                                            .background(year == store.year ? Palette.ac : .clear, in: RoundedRectangle(cornerRadius: 10))
                                            .overlay(RoundedRectangle(cornerRadius: 10).stroke(year == store.year ? Palette.ac : Palette.line, lineWidth: 1))
                                    }
                                    .buttonStyle(.plain)
                                }
                            }
                            .padding(.vertical, 4)
                        }
                    }
                    yearContent
                }
            }
        }
        .page()
        .task { await store.load() }
    }

    @ViewBuilder
    private var yearContent: some View {
        switch store.data {
        case .loading:
            Aside("Loading…").padding(.vertical, 10)
        case .failed(let message):
            Notice(text: message)
        case .loaded(let data) where data.months.isEmpty:
            Aside("Nothing logged in \(String(data.year)).").padding(.vertical, 10)
        case .loaded(let data):
            let busiest = data.months.map(\.logCount).max() ?? 0
            ForEach(Array(data.months.enumerated()), id: \.element.month) { index, month in
                MonthBlock(month: month, share: busiest == 0 ? 0 : Double(month.logCount) / Double(busiest)) {
                    Router.shared.push(.diaryMonth(year: data.year, month: month.month))
                }
                .overlay(alignment: .bottom) {
                    if index < data.months.count - 1 { HairlineRule() }
                }
            }
        }
    }
}

private struct MonthBlock: View {
    let month: DiaryMonthSummary
    let share: Double
    let onOpen: () -> Void

    var body: some View {
        Button(action: onOpen) {
            VStack(alignment: .leading, spacing: 0) {
                HStack(alignment: .firstTextBaseline, spacing: 10) {
                    Text(month.name).font(Fonts.title(20)).foregroundStyle(Palette.ink)
                    Spacer()
                    (Text("\(month.finishedCount)").fontWeight(.bold).foregroundStyle(Palette.ink)
                        + Text(" finished · ").italic()
                        + Text("\(month.logCount)").fontWeight(.bold).foregroundStyle(Palette.ink)
                        + Text(" \(plural(month.logCount, "log"))").italic())
                        .font(Fonts.serif(11.5, italic: true))
                        .foregroundStyle(Palette.muted)
                }

                GeometryReader { geometry in
                    ZStack(alignment: .leading) {
                        Capsule().fill(Palette.line2)
                        Capsule().fill(Palette.ac).frame(width: max(3, geometry.size.width * share))
                    }
                }
                .frame(height: 3)
                .padding(.top, 12)
                .padding(.bottom, 14)

                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: 8) {
                        ForEach(Array(month.touched.enumerated()), id: \.offset) { _, touch in
                            CoverImage(url: touch.imageUrl, title: touch.title, fallbackPadding: 3, fallbackSize: 6.5)
                                .frame(width: 40, height: 60)
                                .clipShape(RoundedRectangle(cornerRadius: 5))
                                .overlay(RoundedRectangle(cornerRadius: 5).stroke(Palette.line2, lineWidth: 1))
                                .overlay(alignment: .bottomTrailing) {
                                    KindGlyph(kind: touch.kind).padding(2)
                                }
                        }
                    }
                    .padding(.bottom, 2)
                }
            }
            .padding(.vertical, 16)
        }
        .buttonStyle(.plain)
    }
}

// The small badge on a cover: ✓ finished, ○ started, ▸ resumed, ✕ dropped, ▐▐ logged.
struct KindGlyph: View {
    let kind: DiaryEventKind

    var body: some View {
        Text(glyph)
            .font(.system(size: kind == .progress ? 7 : 9, weight: .bold))
            .foregroundStyle(color)
            .frame(minWidth: 13, minHeight: 13)
            .padding(.horizontal, 2)
            .background(Color.black.opacity(0.85), in: RoundedRectangle(cornerRadius: 4))
    }

    private var glyph: String {
        switch kind {
        case .finished: "✓"
        case .dropped: "✕"
        case .started: "○"
        case .resumed: "▸"
        case .progress: "▐▐"
        }
    }

    var color: Color {
        switch kind {
        case .finished: Palette.ac
        case .started, .resumed: Palette.sage
        case .progress: Palette.ac2
        case .dropped: Palette.dim
        }
    }
}

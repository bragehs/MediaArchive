import SwiftUI

@MainActor
@Observable
final class ActivityStore {
    var calendar: Loadable<ActivityCalendar> = .loading
    var day: ActivityDay?

    func load() async {
        do {
            calendar = .loaded(try await api.activity())
        } catch {
            calendar = .failed(error.localizedDescription)
        }
    }
}

extension ActivityDay: Identifiable {
    var id: DateOnly { date }
}

// Every month from the first pass to today, opened at the bottom so the past is
// up; a cover on each day something was logged or sat with, and a day opens as a sheet.
struct ActivityView: View {
    @State private var store = ActivityStore()
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                Crumb("Now", trail: "Activity") { dismiss() }

                switch store.calendar {
                case .loading:
                    Aside("Loading…").padding(.vertical, 10)
                case .failed(let message):
                    Notice(text: message)
                case .loaded(let calendar):
                    LazyVStack(alignment: .leading, spacing: 0) {
                        ForEach(calendar.months, id: \.self) { month in
                            MonthGrid(month: month) { store.day = $0 }
                        }
                    }
                }
            }
        }
        .defaultScrollAnchor(.bottom)
        .page()
        .task { await store.load() }
        .sheet(item: $store.day) { day in
            ActivityDayView(day: day)
                .presentationDetents([.medium, .large])
                .presentationBackground(Palette.bg)
        }
    }
}

private struct MonthGrid: View {
    let month: ActivityMonth
    let open: (ActivityDay) -> Void

    private let columns = Array(repeating: GridItem(.flexible(), spacing: 6), count: 7)
    private let letters = ["M", "T", "W", "T", "F", "S", "S"]

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(alignment: .firstTextBaseline, spacing: 10) {
                Text("\(month.name) \(String(month.year))").font(Fonts.title(20)).foregroundStyle(Palette.ink)
                Spacer()
                Aside(summary, size: 11.5, color: Palette.muted)
            }
            .padding(.bottom, 8)
            .overlay(alignment: .bottom) { HairlineRule(height: 2) }

            LazyVGrid(columns: columns, spacing: 6) {
                ForEach(0..<7, id: \.self) { index in
                    Eyebrow(letters[index], size: 7.5, tracking: 0, bold: false)
                }
                ForEach(0..<leadingBlanks, id: \.self) { _ in Color.clear.aspectRatio(2 / 3, contentMode: .fit) }
                ForEach(1...daysInMonth, id: \.self) { number in
                    if let day = days[number] {
                        Button { open(day) } label: { DayCell(day: day) }.buttonStyle(.plain)
                    } else {
                        EmptyDay(number: number, today: first.adding(days: number - 1) == .today)
                    }
                }
            }
            .padding(.top, 10)
        }
        .padding(.vertical, 16)
    }

    private var summary: String {
        var parts = ["\(month.logs) \(plural(month.logs, "log"))"]
        if month.finished > 0 { parts.append("\(month.finished) finished") }
        if month.minutesSat > 0 { parts.append("\(duration(month.minutesSat)) sat") }
        return parts.joined(separator: " · ")
    }

    private var first: DateOnly { DateOnly(year: month.year, month: month.month, day: 1) }

    private var daysInMonth: Int {
        Calendar.current.range(of: .day, in: .month, for: first.date)?.count ?? 30
    }

    // Monday-first: Sunday is 1 in Calendar's weekday numbering.
    private var leadingBlanks: Int {
        (Calendar.current.component(.weekday, from: first.date) + 5) % 7
    }

    private var days: [Int: ActivityDay] {
        Dictionary(uniqueKeysWithValues: month.days.map { ($0.date.day, $0) })
    }
}

private struct DayCell: View {
    let day: ActivityDay

    var body: some View {
        Color.clear
            .aspectRatio(2 / 3, contentMode: .fit)
            .overlay { CoverImage(url: day.imageUrl, title: day.title, fallbackPadding: 3, fallbackSize: 6) }
            .clipShape(RoundedRectangle(cornerRadius: 5))
            .opacity(day.loudest == .sat ? 0.55 : 1)
            .overlay(RoundedRectangle(cornerRadius: 5).stroke(finished ? Palette.ac2 : Palette.line2, lineWidth: finished ? 1.5 : 1))
            .overlay(alignment: .topLeading) { DayNumber(number: day.date.day, today: day.date == .today).padding(2) }
            .overlay(alignment: .bottomTrailing) {
                if day.loudest != .sat { KindGlyph(kind: day.loudest).padding(2) }
            }
            .overlay(alignment: .topTrailing) {
                if day.items > 1 {
                    Text("+\(day.items - 1)")
                        .font(.system(size: 7, weight: .bold))
                        .foregroundStyle(Palette.ink)
                        .padding(.horizontal, 3).padding(.vertical, 1)
                        .background(Color.black.opacity(0.85), in: RoundedRectangle(cornerRadius: 3))
                        .padding(2)
                }
            }
    }

    private var finished: Bool { day.loudest == .finished }
}

private struct EmptyDay: View {
    let number: Int
    let today: Bool

    var body: some View {
        Color.clear
            .aspectRatio(2 / 3, contentMode: .fit)
            .overlay(RoundedRectangle(cornerRadius: 5).stroke(today ? Palette.ink : Palette.line, lineWidth: today ? 1 : 0.5))
            .overlay(alignment: .topLeading) { DayNumber(number: number, today: today).padding(2) }
    }
}

private struct DayNumber: View {
    let number: Int
    let today: Bool

    var body: some View {
        Text(String(number))
            .font(.system(size: 8, weight: today ? .bold : .medium))
            .foregroundStyle(today ? Palette.ac : Palette.dim)
            .padding(.horizontal, 3).padding(.vertical, 1)
            .background(Color.black.opacity(0.75), in: RoundedRectangle(cornerRadius: 3))
    }
}

// The small badge on a cover: ✓ finished, ○ started, ▸ resumed, ✕ dropped, ▐▐ logged.
struct KindGlyph: View {
    let kind: ActivityKind

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
        case .sat: "◷"
        }
    }

    var color: Color {
        switch kind {
        case .finished: Palette.ac
        case .started, .resumed: Palette.sage
        case .progress, .sat: Palette.ac2
        case .dropped: Palette.dim
        }
    }
}

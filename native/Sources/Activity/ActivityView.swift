import SwiftUI

@MainActor
@Observable
final class ActivityStore {
    enum Zoom { case week, month }

    var calendar: Loadable<ActivityCalendar> = .loading
    var day: ActivityDay?
    var zoom: Zoom = .week
    var weekStart = DateOnly.today.monday
    var direction = 1

    func load() async {
        do {
            calendar = .loaded(try await api.activity())
        } catch {
            calendar = .failed(error.localizedDescription)
        }
    }

    var days: [DateOnly: ActivityDay] {
        Dictionary(uniqueKeysWithValues: (calendar.value?.months ?? []).flatMap(\.days).map { ($0.date, $0) })
    }

    var week: [DateOnly] { (0..<7).map { weekStart.adding(days: $0) } }

    var isCurrentWeek: Bool { weekStart >= DateOnly.today.monday }

    func step(_ weeks: Int) {
        direction = weeks
        weekStart = weekStart.adding(days: 7 * weeks)
    }

    func open(weekOf date: DateOnly) {
        weekStart = date.monday
        zoom = .week
    }
}

extension ActivityDay: Identifiable {
    var id: DateOnly { date }
}

extension DateOnly {
    // Monday-first: Sunday is 1 in Calendar's weekday numbering.
    var monday: DateOnly {
        adding(days: -((Calendar.current.component(.weekday, from: date) + 5) % 7))
    }
}

struct ActivityView: View {
    @State private var store = ActivityStore()
    @Environment(\.dismiss) private var dismiss

    // The week lives below full size and the month above it, so both views always zoom the same way.
    var body: some View {
        ZStack {
            switch store.zoom {
            case .week:
                weekPage.transition(.scale(scale: 0.94).combined(with: .opacity))
            case .month:
                monthPage.transition(.scale(scale: 1.06).combined(with: .opacity))
            }
        }
        .animation(.smooth(duration: 0.35), value: store.zoom)
        .task { await store.load() }
        .sheet(item: $store.day) { day in
            ActivityDayView(day: day)
                .presentationDetents([.medium, .large])
                .presentationBackground(Palette.bg)
        }
    }

    private var weekPage: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                header(zoomLabel: "Month") { store.zoom = .month }
                content {
                    WeekHeader(store: store)
                    ZStack(alignment: .top) {
                        VStack(spacing: 0) {
                            ForEach(store.week, id: \.self) { date in
                                WeekDayRow(date: date, day: store.days[date]) { store.day = $0 }
                            }
                        }
                        .id(store.weekStart)
                        .transition(.push(from: store.direction > 0 ? .trailing : .leading))
                    }
                    .clipped()
                    .animation(.smooth(duration: 0.3), value: store.weekStart)
                }
            }
        }
        .page()
    }

    private var monthPage: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                header(zoomLabel: "Week") { store.open(weekOf: .today) }
                content {
                    LazyVStack(alignment: .leading, spacing: 0) {
                        ForEach(store.calendar.value?.months ?? [], id: \.self) { month in
                            MonthGrid(month: month) { store.open(weekOf: $0) }
                        }
                    }
                }
            }
        }
        .defaultScrollAnchor(.bottom)
        .page()
    }

    private func header(zoomLabel: String, zoom: @escaping () -> Void) -> some View {
        HStack(alignment: .center) {
            Crumb("Now", trail: "Activity") { dismiss() }
            Spacer()
            Button(action: zoom) {
                Eyebrow(zoomLabel, size: 10, color: Palette.ac, tracking: 0.1)
                    .padding(.horizontal, 10)
                    .padding(.vertical, 6)
                    .overlay(Capsule().stroke(Palette.line, lineWidth: 1))
            }
            .buttonStyle(.plain)
        }
    }

    @ViewBuilder
    private func content<Content: View>(@ViewBuilder _ loaded: () -> Content) -> some View {
        switch store.calendar {
        case .loading:
            Aside("Loading…").padding(.vertical, 10)
        case .failed(let message):
            Notice(text: message)
        case .loaded:
            loaded()
        }
    }
}

private struct WeekHeader: View {
    let store: ActivityStore

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(alignment: .firstTextBaseline, spacing: 12) {
                arrow("‹", enabled: true) { store.step(-1) }
                Text(range)
                    .font(Fonts.title(20))
                    .foregroundStyle(Palette.ink)
                    .contentTransition(.numericText(countsDown: store.direction < 0))
                    .animation(.smooth(duration: 0.3), value: store.weekStart)
                arrow("›", enabled: !store.isCurrentWeek) { store.step(1) }
                Spacer()
            }
            Aside(summary, size: 11.5, color: Palette.muted)
        }
        .padding(.bottom, 8)
        .frame(maxWidth: .infinity, alignment: .leading)
        .overlay(alignment: .bottom) { HairlineRule(height: 2) }
        .padding(.top, 4)
    }

    private func arrow(_ glyph: String, enabled: Bool, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Text(glyph)
                .font(.system(size: 22, weight: .medium))
                .foregroundStyle(enabled ? Palette.ac : Palette.line)
                .frame(width: 28, height: 28)
        }
        .buttonStyle(.plain)
        .disabled(!enabled)
    }

    private var range: String {
        let end = store.weekStart.adding(days: 6)
        return "\(store.weekStart.formatted("d MMM")) – \(end.formatted("d MMM"))"
    }

    private var summary: String {
        let days = store.week.compactMap { store.days[$0] }
        let logs = days.reduce(0) { $0 + $1.events.count { $0.kind != .sat } + $1.runs.reduce(0) { $0 + $1.logs } }
        let minutes = days.reduce(0) { $0 + $1.minutesSat }
        var parts = ["\(days.count) \(plural(days.count, "day"))", "\(logs) \(plural(logs, "log"))"]
        if minutes > 0 { parts.append("\(duration(minutes)) sat") }
        return parts.joined(separator: " · ")
    }
}

private struct WeekDayRow: View {
    let date: DateOnly
    let day: ActivityDay?
    let open: (ActivityDay) -> Void

    var body: some View {
        Button { if let day { open(day) } } label: {
            HStack(alignment: .top, spacing: 14) {
                VStack(alignment: .leading, spacing: 2) {
                    Eyebrow(date.formatted("EEE"), size: 8.5, color: today ? Palette.ac : Palette.dim, tracking: 0.12, bold: false)
                    Text(String(date.day))
                        .font(Fonts.title(20))
                        .foregroundStyle(today ? Palette.ac : (day == nil ? Palette.dim : Palette.ink))
                    if let minutes = day?.minutesSat, minutes > 0 {
                        Aside(duration(minutes), size: 10.5, color: Palette.muted).padding(.top, 2)
                    }
                }
                .frame(width: 44, alignment: .leading)

                if let day {
                    FlowLayout(spacing: 6) {
                        ForEach(DayItem.of(day)) { item in
                            DayCover(item: item)
                        }
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                } else {
                    Spacer()
                }
            }
            .padding(.vertical, day == nil ? 8 : 12)
            .frame(maxWidth: .infinity, alignment: .leading)
            .contentShape(Rectangle())
            .overlay(alignment: .bottom) { HairlineRule() }
        }
        .buttonStyle(.plain)
        .disabled(day == nil)
    }

    private var today: Bool { date == .today }
}

struct DayItem: Identifiable {
    let id: Int
    let title: String
    let imageUrl: String?
    let kind: ActivityKind

    static func of(_ day: ActivityDay) -> [DayItem] {
        let touches = day.events.map { DayItem(id: $0.userMediaItemId, title: $0.title, imageUrl: $0.imageUrl, kind: $0.kind) }
            + day.runs.map { DayItem(id: $0.userMediaItemId, title: $0.title, imageUrl: $0.imageUrl, kind: $0.logs > 0 ? .progress : .sat) }
        var loudest: [Int: DayItem] = [:]
        for touch in touches where loudest[touch.id].map({ rank(touch.kind) < rank($0.kind) }) ?? true {
            loudest[touch.id] = touch
        }
        return loudest.values.sorted { (rank($0.kind), $0.title) < (rank($1.kind), $1.title) }
    }

    private static func rank(_ kind: ActivityKind) -> Int {
        switch kind {
        case .finished: 0
        case .started: 1
        case .resumed: 2
        case .progress: 3
        case .sat: 4
        case .dropped: 5
        }
    }
}

private struct DayCover: View {
    let item: DayItem

    var body: some View {
        CoverImage(url: item.imageUrl, title: item.title, fallbackPadding: 3, fallbackSize: 6)
            .frame(width: 44, height: 66)
            .clipShape(RoundedRectangle(cornerRadius: 5))
            .opacity(item.kind == .sat ? 0.55 : 1)
            .overlay(RoundedRectangle(cornerRadius: 5).stroke(item.kind == .finished ? Palette.ac2 : Palette.line2,
                                                              lineWidth: item.kind == .finished ? 1.5 : 1))
            .overlay(alignment: .bottomTrailing) { KindGlyph(kind: item.kind).padding(2) }
    }
}

private struct MonthGrid: View {
    let month: ActivityMonth
    let open: (DateOnly) -> Void

    private let columns = Array(repeating: GridItem(.flexible(), spacing: 6), count: 7)
    private let letters = ["M", "T", "W", "T", "F", "S", "S"]

    // One identity space: letters, blanks and days numbered separately collide, and a lazy grid drops duplicates.
    private enum Cell: Hashable { case letter(Int), blank(Int), day(Int) }

    private var cells: [Cell] {
        (0..<7).map(Cell.letter) + (0..<leadingBlanks).map(Cell.blank) + (1...daysInMonth).map(Cell.day)
    }

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
                ForEach(cells, id: \.self) { cell in
                    switch cell {
                    case .letter(let index):
                        Eyebrow(letters[index], size: 7.5, tracking: 0, bold: false)
                    case .blank:
                        Color.clear.aspectRatio(2 / 3, contentMode: .fit)
                    case .day(let number):
                        let date = first.adding(days: number - 1)
                        Button { open(date) } label: {
                            if let day = days[number] {
                                DayCell(day: day)
                            } else {
                                EmptyDay(number: number, today: date == .today)
                            }
                        }
                        .buttonStyle(.plain)
                        .disabled(date > .today)
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

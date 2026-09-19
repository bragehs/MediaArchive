import SwiftUI

// One month, day by day: milestones with a cover, the quieter ticks below.
struct DiaryMonthView: View {
    let year: Int
    let month: Int

    @State private var data: Loadable<DiaryMonthDetail> = .loading
    @Environment(\.lexicon) private var lexicon
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                Button { dismiss() } label: {
                    (Text("‹ Diary / ") + Text(data.value?.name ?? "").foregroundStyle(Palette.ac))
                        .font(Fonts.display(9.5))
                        .tracking(1.1)
                        .textCase(.uppercase)
                        .foregroundStyle(Palette.dim)
                }
                .buttonStyle(.plain)
                .padding(.top, 6)
                .padding(.bottom, 12)

                switch data {
                case .loading:
                    Aside("Loading…").padding(.vertical, 10)
                case .failed(let message):
                    Notice(text: message)
                case .loaded(let detail) where detail.days.isEmpty:
                    Aside("Nothing logged in \(detail.name) \(String(year)).").padding(.vertical, 10)
                case .loaded(let detail):
                    content(detail)
                }
            }
        }
        .page()
        .task {
            do {
                data = .loaded(try await api.diaryMonth(MonthArgs(year: year, month: month)))
            } catch {
                data = .failed(error.localizedDescription)
            }
        }
    }

    @ViewBuilder
    private func content(_ detail: DiaryMonthDetail) -> some View {
        HStack(alignment: .firstTextBaseline, spacing: 10) {
            Text(detail.name).font(Fonts.title(22)).foregroundStyle(Palette.ink)
            Spacer()
            (Text("\(detail.logCount)").fontWeight(.bold).foregroundStyle(Palette.ink)
                + Text(" logs · ").italic()
                + Text("\(detail.finishedCount)").fontWeight(.bold).foregroundStyle(Palette.ink)
                + Text(" finished").italic())
                .font(Fonts.serif(11.5, italic: true))
                .foregroundStyle(Palette.muted)
        }
        .padding(.bottom, 8)
        .overlay(alignment: .bottom) { HairlineRule(height: 2) }

        ForEach(detail.days, id: \.date) { day in
            Eyebrow(day.date.formatted("EEE d"), size: 9, tracking: 0.16, bold: false)
                .padding(.top, 22)
                .padding(.bottom, 4)

            ForEach(Array(day.events.enumerated()), id: \.offset) { _, event in
                if event.isMilestone {
                    milestone(event)
                } else {
                    tick(title: event.title, type: event.mediaType, detail: progress(event),
                         note: event.note, said: true, id: event.userMediaItemId)
                }
            }

            ForEach(Array(day.runs.enumerated()), id: \.offset) { _, run in
                tick(title: run.title + (run.count > 1 ? " — logged \(run.count)×" : ""),
                     type: run.mediaType,
                     detail: "+\(trimmed(run.effortDelta)) \(lexicon.unit(run.mediaType))",
                     note: nil, said: false, id: run.userMediaItemId)
            }
        }
    }

    private func milestone(_ event: DiaryEvent) -> some View {
        Button { openItem(event.userMediaItemId) } label: {
            HStack(alignment: .top, spacing: 12) {
                CoverImage(url: event.imageUrl, title: event.title, fallbackPadding: 4, fallbackSize: 7.5)
                    .frame(width: 46, height: 69)
                    .clipShape(RoundedRectangle(cornerRadius: 6))
                    .overlay(RoundedRectangle(cornerRadius: 6).stroke(Palette.line2, lineWidth: 1))
                VStack(alignment: .leading, spacing: 0) {
                    HStack(alignment: .firstTextBaseline, spacing: 6) {
                        Text(event.title).font(Fonts.title(15)).foregroundStyle(Palette.ink)
                        if let rating = event.rating {
                            Text("★ \(stars(Double(rating)))").font(Fonts.display(11)).foregroundStyle(Palette.ac2)
                        }
                    }
                    Eyebrow(kindLine(event), size: 8.5, color: KindGlyph(kind: event.kind).color, tracking: 0.11, bold: false)
                        .padding(.top, 5)
                    if let note = event.note, !note.trimmingCharacters(in: .whitespaces).isEmpty {
                        Text(note)
                            .font(Fonts.serif(13.5))
                            .lineSpacing(3)
                            .foregroundStyle(Palette.muted)
                            .padding(.leading, 9)
                            .overlay(alignment: .leading) { Rectangle().fill(Palette.line).frame(width: 2) }
                            .padding(.top, 7)
                    }
                }
                Spacer(minLength: 0)
            }
            .padding(.vertical, 13)
            .overlay(alignment: .top) { HairlineRule(height: 2) }
        }
        .buttonStyle(.plain)
    }

    private func kindLine(_ event: DiaryEvent) -> String {
        var text = lexicon.label(event.kind)
        if event.isReread { text += " · ↻ reread" }
        if let context = event.context { text += " · " + lexicon.label(context).lowercased() }
        return text
    }

    private func tick(title: String, type: MediaType, detail: String, note: String?, said: Bool, id: Int) -> some View {
        Button { openItem(id) } label: {
            VStack(alignment: .leading, spacing: 4) {
                HStack(alignment: .firstTextBaseline, spacing: 8) {
                    Circle().fill(Palette.accent(type)).frame(width: 6, height: 6).offset(y: -1)
                    Text(title)
                        .font(Fonts.display(11.5))
                        .foregroundStyle(said ? Palette.ink : Palette.muted)
                        .lineLimit(1)
                    Spacer(minLength: 6)
                    Aside(detail, size: 11)
                }
                if let note, !note.isEmpty {
                    Text(note)
                        .font(Fonts.serif(12.5))
                        .foregroundStyle(Palette.muted)
                        .padding(.leading, 14)
                }
            }
            .padding(.vertical, 7)
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .buttonStyle(.plain)
    }

    private func progress(_ event: DiaryEvent) -> String {
        let unit = lexicon.unit(event.mediaType)
        var parts: [String] = []
        if let delta = event.effortDelta, delta > 0 { parts.append("+\(trimmed(delta)) \(unit)") }
        if let at = event.effortAtTime {
            parts.append("→ \(trimmed(at))" + (event.length.map { "/\($0)" } ?? ""))
        }
        return parts.joined(separator: " ")
    }
}

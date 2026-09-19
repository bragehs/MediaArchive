import SwiftUI

@MainActor
@Observable
final class ProfileStore {
    var snapshot: Loadable<ProfileSnapshot> = .loading
    var recordsTab: MediaType = .book
    var allTime = false

    func load() async {
        if snapshot.value == nil { snapshot = .loading }
        do {
            snapshot = .loaded(try await api.profile())
        } catch {
            snapshot = .failed(error.localizedDescription)
        }
    }

    var currentPanel: TypePanel? {
        guard let panels = snapshot.value?.panels else { return nil }
        return panels.first { $0.mediaType == recordsTab } ?? panels.first
    }
}

// The taste dashboard: the identity strip, the hall of fame, universes, the
// canon of creators, and one records pane per medium.
struct ProfileView: View {
    @State private var store = ProfileStore()
    @Environment(\.lexicon) private var lexicon

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                switch store.snapshot {
                case .loading:
                    Aside("Loading…").padding(.vertical, 10)
                case .failed(let message):
                    Notice(text: message)
                case .loaded(let snapshot) where snapshot.itemsLogged == 0:
                    Aside("Nothing logged yet — the profile fills in as you add things.").padding(.vertical, 10)
                case .loaded(let snapshot):
                    content(snapshot)
                }
            }
        }
        .page()
        .task { await store.load() }
    }

    @ViewBuilder
    private func content(_ snapshot: ProfileSnapshot) -> some View {
        IdentityStrip(snapshot: snapshot)

        if !snapshot.hallOfFame.isEmpty {
            SectionHead("Hall of fame")
            LazyVGrid(columns: Array(repeating: GridItem(.flexible(), spacing: 9), count: 4), spacing: 9) {
                ForEach(snapshot.hallOfFame, id: \.userMediaItemId) { fame in
                    Button { openItem(fame.userMediaItemId) } label: {
                        CoverTile(url: fame.imageUrl, title: fame.title, radius: 7)
                            .overlay(alignment: .bottomTrailing) {
                                Text(fame.isFavorite ? "♥" : "★ \(stars(Double(fame.rating)))")
                                    .font(Fonts.display(9, bold: true))
                                    .foregroundStyle(Palette.ac2)
                                    .padding(.horizontal, 5)
                                    .padding(.vertical, 2)
                                    .background(Color.black.opacity(0.82), in: RoundedRectangle(cornerRadius: 5))
                                    .padding(4)
                            }
                    }
                    .buttonStyle(.plain)
                }
            }
        }

        ForEach(snapshot.universes, id: \.name) { universe in
            SectionHead(universe.name, right: "\(universe.works) works" + (universe.avgRating.map { " · ★ \(stars($0))" } ?? ""))
            Aside(universe.effort.map { "\(trimmed($0.value)) \($0.unit)" }.joined(separator: " · "), size: 12.5, color: Palette.muted)
                .padding(.top, -2)
                .padding(.bottom, 12)
            FlowLayout(spacing: 8) {
                ForEach(universe.covers, id: \.userMediaItemId) { cover in
                    Button { openItem(cover.userMediaItemId) } label: {
                        CoverImage(url: cover.imageUrl, title: cover.title, fallbackPadding: 4, fallbackSize: 7)
                            .frame(width: 48, height: 72)
                            .opacity(cover.status == .interested ? 0.45 : 1)
                            .clipShape(RoundedRectangle(cornerRadius: 6))
                            .overlay(RoundedRectangle(cornerRadius: 6).stroke(Palette.line2, lineWidth: 1))
                            .overlay(alignment: .bottomTrailing) { StatusGlyph(status: cover.status).padding(2) }
                    }
                    .buttonStyle(.plain)
                }
            }
        }

        if !snapshot.canon.isEmpty {
            SectionHead("The canon")
            // The vault's one sanctioned text list: five rows in view, the rest scroll within.
            ScrollView {
                VStack(spacing: 0) {
                    ForEach(Array(snapshot.canon.enumerated()), id: \.element.name) { index, creator in
                        CreatorRow(creator: creator)
                            .overlay(alignment: .bottom) {
                                if index < snapshot.canon.count - 1 { HairlineRule(color: Palette.line2) }
                            }
                    }
                }
            }
            .frame(maxHeight: 201)
            .scrollBounceBehavior(.basedOnSize)
        }

        SectionHead("Records")
        if !snapshot.panels.isEmpty {
            records(snapshot)
        }
    }

    @ViewBuilder
    private func records(_ snapshot: ProfileSnapshot) -> some View {
        HStack(spacing: 6) {
            ForEach(snapshot.panels, id: \.mediaType) { panel in
                let on = store.currentPanel?.mediaType == panel.mediaType
                Button { store.recordsTab = panel.mediaType } label: {
                    Eyebrow(lexicon.label(panel.mediaType) + "s", size: 10.5, color: on ? Palette.onAc : Palette.dim, tracking: 0.08, bold: false)
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

private struct IdentityStrip: View {
    let snapshot: ProfileSnapshot

    var body: some View {
        let cells: [(String, String?, String, String?)] = [
            (String(snapshot.itemsLogged), nil, "Items logged", nil),
            snapshot.avgRating.map { (stars($0), "/5", "Avg rating", "across \(snapshot.ratedCount) rated") }
                ?? ("—", nil, "Avg rating", "nothing rated yet"),
            (String(snapshot.finished), nil, "Finished", "of \(snapshot.itemsLogged) logged"),
            (String(snapshot.genreCount), nil, "Genres", nil),
        ]
        LazyVGrid(columns: [GridItem(.flexible(), spacing: 0), GridItem(.flexible(), spacing: 0)], spacing: 0) {
            ForEach(Array(cells.enumerated()), id: \.offset) { index, cell in
                VStack(alignment: .leading, spacing: 0) {
                    HStack(alignment: .firstTextBaseline, spacing: 3) {
                        Text(cell.0).font(Fonts.title(30)).foregroundStyle(cell.0 == "—" ? Palette.dim : Palette.ac)
                        if let unit = cell.1 { Eyebrow(unit, size: 10, tracking: 0.1, bold: false) }
                    }
                    Eyebrow(cell.2, size: 8.5, tracking: 0.12, bold: false).padding(.top, 8)
                    if let note = cell.3 {
                        Aside(note, size: 11, color: Palette.muted).padding(.top, 2)
                    }
                }
                .padding(.vertical, 14)
                .padding(.leading, index % 2 == 0 ? 0 : 16)
                .padding(.trailing, index % 2 == 0 ? 16 : 0)
                .frame(maxWidth: .infinity, alignment: .leading)
                .overlay(alignment: .trailing) {
                    if index % 2 == 0 { Rectangle().fill(Palette.line).frame(width: 1) }
                }
                .overlay(alignment: .bottom) {
                    if index < 2 { HairlineRule(color: Palette.line2) }
                }
            }
        }
        .overlay(alignment: .top) { Rectangle().fill(Palette.ink).frame(height: 2) }
        .overlay(alignment: .bottom) { HairlineRule() }
        .padding(.top, 2)
    }
}

private struct CreatorRow: View {
    let creator: CreatorLine
    @Environment(\.lexicon) private var lexicon

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: 10) {
            Text(creator.name).font(Fonts.title(13.5)).foregroundStyle(Palette.ink).lineLimit(1)
            (Text("\(creator.works)").fontWeight(.bold).foregroundStyle(Palette.muted)
                + Text(" \(creator.works == 1 ? "work" : "works") · ").italic()
                + Text(creator.types.map { lexicon.label($0).lowercased() + (creator.works == 1 ? "" : "s") }.joined(separator: " · ")).italic())
                .font(Fonts.serif(11.5, italic: true))
                .foregroundStyle(Palette.dim)
                .lineLimit(1)
                .frame(maxWidth: .infinity, alignment: .leading)
            if let rating = creator.avgRating {
                Text("★ \(stars(rating))").font(Fonts.display(11, bold: true)).foregroundStyle(Palette.ac2)
            }
        }
        .padding(.vertical, 9)
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

// One bar per bucket, normalised to the tallest; an optional label under each.
private struct BarChart: View {
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

struct StatusGlyph: View {
    let status: MediaStatus
    @Environment(\.lexicon) private var lexicon

    var body: some View {
        Text(lexicon.glyph(status))
            .font(.system(size: status == .inProgress ? 6 : 8, weight: .bold))
            .foregroundStyle(color)
            .frame(minWidth: 13, minHeight: 13)
            .padding(.horizontal, 2)
            .background(Color.black.opacity(0.82), in: RoundedRectangle(cornerRadius: 4))
    }

    private var color: Color {
        switch status {
        case .completed: Palette.ac
        case .inProgress: Palette.ac2
        case .interested: Palette.sage
        case .dropped: Palette.dim
        }
    }
}

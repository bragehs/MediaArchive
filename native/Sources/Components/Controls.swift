import SwiftUI

// The `.btn` — accent block, uppercase display text.
struct PrimaryButtonStyle: ButtonStyle {
    var fullWidth = false

    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(Fonts.display(10.5, bold: true))
            .tracking(1.5)
            .textCase(.uppercase)
            .foregroundStyle(Palette.onAc)
            .padding(.horizontal, 18)
            .padding(.vertical, 12)
            .frame(maxWidth: fullWidth ? .infinity : nil)
            .background(Palette.ac, in: RoundedRectangle(cornerRadius: 10))
            .opacity(configuration.isPressed ? 0.8 : 1)
    }
}

// The `.btn.ghost` — outlined, muted.
struct GhostButtonStyle: ButtonStyle {
    var fullWidth = false

    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(Fonts.display(10.5, bold: true))
            .tracking(1.5)
            .textCase(.uppercase)
            .foregroundStyle(Palette.muted)
            .padding(.horizontal, 18)
            .padding(.vertical, 12)
            .frame(maxWidth: fullWidth ? .infinity : nil)
            .overlay(RoundedRectangle(cornerRadius: 10).stroke(Palette.line, lineWidth: 1))
            .opacity(configuration.isPressed ? 0.7 : 1)
    }
}

// The underlined hint link (`.hintbtn` / `.sopen`).
struct HintButtonStyle: ButtonStyle {
    var color: Color = Palette.muted

    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(Fonts.display(11.5))
            .foregroundStyle(color)
            .underline(pattern: .dot)
            .opacity(configuration.isPressed ? 0.7 : 1)
    }
}

extension View {
    // Faded and inert while a form can't be submitted.
    func disabledLook(_ disabled: Bool) -> some View {
        self.disabled(disabled).opacity(disabled ? 0.4 : 1)
    }
}

// The pill segmented control (`.xseg` / `.eseg`).
struct SegmentedPills<Option: Hashable>: View {
    let options: [Option]
    @Binding var selection: Option
    let label: (Option) -> String

    var body: some View {
        HStack(spacing: 0) {
            ForEach(options, id: \.self) { option in
                Button {
                    selection = option
                } label: {
                    Eyebrow(label(option), size: 10, color: selection == option ? Palette.onAc : Palette.dim, tracking: 0.1)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 9)
                        .background(selection == option ? Palette.ac : .clear, in: RoundedRectangle(cornerRadius: 8))
                }
                .buttonStyle(.plain)
            }
        }
        .padding(3)
        .background(Color.black.opacity(0.2), in: RoundedRectangle(cornerRadius: 11))
        .overlay(RoundedRectangle(cornerRadius: 11).stroke(Palette.line, lineWidth: 1))
    }
}

// The welded segment bar (`.segs` / `.seg`).
struct SegmentBar<Option: Hashable>: View {
    let options: [Option]
    @Binding var selection: Option
    let label: (Option) -> String

    var body: some View {
        HStack(spacing: 0) {
            ForEach(Array(options.enumerated()), id: \.element) { index, option in
                Button {
                    selection = option
                } label: {
                    Eyebrow(label(option), size: 9.5, color: selection == option ? Palette.onAc : Palette.muted, tracking: 0.08)
                        .padding(.horizontal, 11)
                        .padding(.vertical, 7)
                        .background(selection == option ? Palette.ac : .clear)
                        .overlay {
                            Rectangle().stroke(selection == option ? Palette.ac : Palette.line, lineWidth: 1)
                        }
                }
                .buttonStyle(.plain)
                .padding(.leading, index == 0 ? 0 : -1)
            }
        }
    }
}

enum NoticeKind { case bad, good }

struct Notice: View {
    let text: String
    var kind: NoticeKind = .bad
    var trailing: AnyView? = nil

    var body: some View {
        HStack(spacing: 10) {
            Text(text)
                .font(Fonts.display(12))
                .foregroundStyle(kind == .bad ? Palette.badInk : Palette.muted)
                .frame(maxWidth: .infinity, alignment: .leading)
            if let trailing { trailing }
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 10)
        .background(kind == .bad ? Palette.bad.opacity(0.15) : Palette.panel, in: RoundedRectangle(cornerRadius: 9))
        .overlay(RoundedRectangle(cornerRadius: 9).stroke(kind == .bad ? Palette.bad : Palette.line, lineWidth: 1))
        .overlay(alignment: .leading) {
            if kind == .good {
                RoundedRectangle(cornerRadius: 9).fill(Palette.ac).frame(width: 3).padding(.vertical, 1)
            }
        }
    }
}

// `.fl` / `.il2`: the small uppercase field label, with its optional/required tail.
struct FieldLabel: View {
    let text: String
    var optional = false
    var required = false

    init(_ text: String, optional: Bool = false, required: Bool = false) {
        self.text = text
        self.optional = optional
        self.required = required
    }

    var body: some View {
        HStack(spacing: 4) {
            Eyebrow(text, size: 9.5, color: Palette.dim)
            if optional {
                Text("optional").font(Fonts.serif(10, italic: true)).foregroundStyle(Palette.dim.opacity(0.8))
            }
            if required {
                Eyebrow("required", size: 9.5, color: Palette.ac)
            }
        }
        .padding(.bottom, 6)
    }
}

extension View {
    // The `.in` / `.iin` text control frame.
    func field() -> some View {
        self
            .font(Fonts.display(13))
            .foregroundStyle(Palette.ink)
            .padding(.horizontal, 12)
            .padding(.vertical, 10)
            .background(Palette.well, in: RoundedRectangle(cornerRadius: 9))
            .overlay(RoundedRectangle(cornerRadius: 9).stroke(Palette.line, lineWidth: 1))
    }
}

struct TextArea: View {
    @Binding var text: String
    var placeholder = ""

    var body: some View {
        TextField(placeholder, text: $text, axis: .vertical)
            .lineLimit(3...8)
            .font(Fonts.serif(14, italic: true))
            .foregroundStyle(Palette.ink)
            .padding(.horizontal, 12)
            .padding(.vertical, 10)
            .background(Palette.well, in: RoundedRectangle(cornerRadius: 9))
            .overlay(RoundedRectangle(cornerRadius: 9).stroke(Palette.line, lineWidth: 1))
    }
}

// A whole number or nothing — the empty field is null, never zero.
struct NumberField: View {
    @Binding var value: Int?
    var placeholder = ""

    var body: some View {
        TextField(placeholder, text: Binding(
            get: { value.map(String.init) ?? "" },
            set: { value = Int($0.filter(\.isNumber)) }))
            .keyboardType(.numberPad)
            .field()
    }
}

struct DecimalField: View {
    @Binding var value: Double?
    var placeholder = ""

    @State private var text = ""

    var body: some View {
        TextField(placeholder, text: $text)
            .keyboardType(.decimalPad)
            .field()
            .onAppear { text = value.map(trimmed) ?? "" }
            .onChange(of: text) { value = Double(text.replacingOccurrences(of: ",", with: ".")) }
            .onChange(of: value) {
                if Double(text.replacingOccurrences(of: ",", with: ".")) != value { text = value.map(trimmed) ?? "" }
            }
    }
}

struct DateField: View {
    @Binding var date: DateOnly?

    var body: some View {
        DatePicker("", selection: Binding(
            get: { date?.date ?? Date() },
            set: { date = DateOnly($0) }), displayedComponents: .date)
            .labelsHidden()
            .datePickerStyle(.compact)
            .tint(Palette.ac)
            .colorScheme(.dark)
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.horizontal, 6)
            .padding(.vertical, 4)
            .background(Palette.well, in: RoundedRectangle(cornerRadius: 9))
            .overlay(RoundedRectangle(cornerRadius: 9).stroke(Palette.line, lineWidth: 1))
    }
}

// A `<select>`: the current choice in a field frame, options in a menu.
struct MenuField<Option: Hashable>: View {
    @Binding var selection: Option?
    let options: [Option]
    let label: (Option) -> String
    var empty = "—"

    var body: some View {
        Menu {
            Button(empty) { selection = nil }
            ForEach(options, id: \.self) { option in
                Button(label(option)) { selection = option }
            }
        } label: {
            HStack {
                Text(selection.map(label) ?? empty)
                    .foregroundStyle(selection == nil ? Palette.dim : Palette.ink)
                Spacer()
                Text("▾").foregroundStyle(Palette.dim)
            }
            .field()
        }
    }
}

// Wraps its children onto as many rows as they need.
struct FlowLayout: Layout {
    var spacing: CGFloat = 6

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) -> CGSize {
        let rows = arrange(proposal: proposal, subviews: subviews)
        let width = proposal.width ?? rows.map(\.width).max() ?? 0
        let height = rows.reduce(0) { $0 + $1.height } + spacing * CGFloat(max(0, rows.count - 1))
        return CGSize(width: width, height: height)
    }

    func placeSubviews(in bounds: CGRect, proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) {
        var y = bounds.minY
        for row in arrange(proposal: proposal, subviews: subviews) {
            var x = bounds.minX
            for index in row.indices {
                let size = subviews[index].sizeThatFits(.unspecified)
                subviews[index].place(at: CGPoint(x: x, y: y), proposal: .unspecified)
                x += size.width + spacing
            }
            y += row.height + spacing
        }
    }

    private struct Row {
        var indices: [Int] = []
        var width: CGFloat = 0
        var height: CGFloat = 0
    }

    private func arrange(proposal: ProposedViewSize, subviews: Subviews) -> [Row] {
        let maxWidth = proposal.width ?? .infinity
        var rows: [Row] = [Row()]
        for (index, subview) in subviews.enumerated() {
            let size = subview.sizeThatFits(.unspecified)
            var row = rows[rows.count - 1]
            let extra = row.indices.isEmpty ? 0 : spacing
            if !row.indices.isEmpty && row.width + extra + size.width > maxWidth {
                rows.append(Row())
                row = rows[rows.count - 1]
            }
            row.indices.append(index)
            row.width += (row.indices.count == 1 ? 0 : spacing) + size.width
            row.height = max(row.height, size.height)
            rows[rows.count - 1] = row
        }
        return rows
    }
}

// The `.vp-chip`: accent pill with an × to remove.
struct Chip: View {
    let text: String
    var onRemove: (() -> Void)? = nil

    var body: some View {
        HStack(spacing: 7) {
            Eyebrow(text, size: 9.5, color: Palette.onAc, tracking: 0.08)
            if let onRemove {
                Button { onRemove() } label: {
                    Text("×").font(.system(size: 13)).foregroundStyle(Palette.onAc.opacity(0.7))
                }
                .buttonStyle(.plain)
            }
        }
        .padding(.horizontal, 9)
        .padding(.vertical, 4)
        .background(Palette.ac, in: RoundedRectangle(cornerRadius: 10))
    }
}

struct ProgressBar: View {
    let percent: Double
    var height: CGFloat = 4

    var body: some View {
        GeometryReader { geometry in
            ZStack(alignment: .leading) {
                Capsule().fill(Color.black.opacity(0.3))
                Capsule().fill(Palette.ac)
                    .frame(width: geometry.size.width * CGFloat(min(max(percent, 0), 100)) / 100)
            }
        }
        .frame(height: height)
    }
}

struct HairlineRule: View {
    var color: Color = Palette.line
    var height: CGFloat = 1

    var body: some View {
        Rectangle().fill(color).frame(height: height)
    }
}

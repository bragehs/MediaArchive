import SwiftUI

// Pick-existing-or-create over a controlled vocabulary. A typeahead, not a list
// to browse — the whole vocabulary is noise until you type.
struct VocabularyPicker: View {
    @Binding var selected: [String]
    let suggestions: [String]
    var max: Int? = nil
    var placeholder = ""

    @State private var draft = ""

    private var matches: [String] {
        let query = draft.trimmingCharacters(in: .whitespaces)
        guard !query.isEmpty else { return [] }
        var seen = Set<String>()
        return suggestions
            .filter { candidate in
                !selected.contains { $0.caseInsensitiveCompare(candidate) == .orderedSame }
                    && candidate.range(of: query, options: .caseInsensitive) != nil
            }
            .filter { seen.insert($0.lowercased()).inserted }
            .prefix(12)
            .map { $0 }
    }

    private var canCreate: Bool {
        let query = draft.trimmingCharacters(in: .whitespaces)
        return !query.isEmpty && !matches.contains { $0.caseInsensitiveCompare(query) == .orderedSame }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 7) {
            if selected.isEmpty {
                Aside("none", size: 11.5)
                    .frame(minHeight: 22)
            } else {
                FlowLayout(spacing: 6) {
                    ForEach(selected, id: \.self) { value in
                        Chip(text: value) { remove(value) }
                    }
                }
            }

            TextField(placeholder, text: $draft)
                .textInputAutocapitalization(.never)
                .autocorrectionDisabled()
                .submitLabel(.done)
                .onSubmit {
                    let query = draft.trimmingCharacters(in: .whitespaces)
                    // Prefer an exact known match over creating a near-duplicate.
                    let exact = matches.first { $0.caseInsensitiveCompare(query) == .orderedSame }
                    commit(exact ?? query)
                }
                .field()

            if !matches.isEmpty {
                FlowLayout(spacing: 6) {
                    ForEach(matches, id: \.self) { match in
                        Button { commit(match) } label: {
                            Eyebrow(match, size: 9.5, color: Palette.muted, tracking: 0.08)
                                .padding(.horizontal, 9)
                                .padding(.vertical, 4)
                                .overlay(RoundedRectangle(cornerRadius: 10).stroke(Palette.line, lineWidth: 1))
                        }
                        .buttonStyle(.plain)
                    }
                }
            }

            if canCreate {
                Button { commit(draft) } label: {
                    Text("+ create “\(draft.trimmingCharacters(in: .whitespaces))”")
                        .font(Fonts.display(9.5, bold: true))
                        .foregroundStyle(Palette.sage)
                        .padding(.horizontal, 9)
                        .padding(.vertical, 4)
                        .overlay(RoundedRectangle(cornerRadius: 10).stroke(Palette.sage, lineWidth: 1))
                }
                .buttonStyle(.plain)
            }
        }
    }

    private func commit(_ value: String) {
        let trimmed = value.trimmingCharacters(in: .whitespaces)
        guard !trimmed.isEmpty,
              !selected.contains(where: { $0.caseInsensitiveCompare(trimmed) == .orderedSame })
        else { return }

        var next = selected
        // Max = 1 makes it single-valued: a new pick replaces the old.
        if let max, next.count >= max {
            next.removeFirst(next.count - max + 1)
        }
        next.append(trimmed)
        selected = next
        draft = ""
    }

    private func remove(_ value: String) {
        selected.removeAll { $0 == value }
    }
}

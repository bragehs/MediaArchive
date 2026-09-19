import Foundation

// Label lookups over the table C# serves once at launch (UiHelpers is the source).
extension Lexicon {
    func type(_ type: MediaType) -> TypeEntry {
        types.first { $0.value == type } ?? TypeEntry(value: type, label: type.rawValue, unit: "",
            runtimeLabel: "", contexts: [])
    }

    func label(_ type: MediaType) -> String { self.type(type).label }

    func unit(_ type: MediaType) -> String { self.type(type).unit }

    func label(_ status: MediaStatus) -> String {
        statuses.first { $0.value == status }?.label ?? status.rawValue
    }

    func glyph(_ status: MediaStatus) -> String {
        statuses.first { $0.value == status }?.glyph ?? "•"
    }

    func label(_ context: ConsumptionContext) -> String {
        contexts.first { $0.value == context }?.label ?? context.rawValue
    }

    func label(_ source: DiscoverySource) -> String {
        discovery.first { $0.value == source }?.label ?? source.rawValue
    }

    func label(_ kind: DiaryEventKind) -> String {
        kinds.first { $0.value == kind }?.label ?? kind.rawValue
    }
}

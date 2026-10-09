import Foundation

enum Loadable<Value> {
    case loading
    case loaded(Value)
    case failed(String)

    var value: Value? {
        if case .loaded(let value) = self { return value }
        return nil
    }

    var isLoading: Bool {
        if case .loading = self { return true }
        return false
    }
}

func plural(_ count: Int, _ one: String, _ many: String? = nil) -> String {
    count == 1 ? one : (many ?? one + "s")
}

func trimmed(_ value: Double) -> String {
    let rounded = (value * 10).rounded() / 10
    return rounded == rounded.rounded() ? String(Int(rounded)) : String(format: "%.1f", rounded)
}

func grouped(_ value: Double) -> String {
    let formatter = NumberFormatter()
    formatter.numberStyle = .decimal
    formatter.maximumFractionDigits = 0
    formatter.locale = Locale(identifier: "en_US")
    return formatter.string(from: NSNumber(value: value)) ?? String(Int(value))
}

func duration(_ minutes: Int) -> String {
    minutes < 60 ? "\(minutes) min" : "\(trimmed(Double(minutes) / 60)) h"
}

func stars(_ tenScale: Double) -> String { trimmed(tenScale / 2) }

func ago(_ days: Int) -> String {
    days <= 0 ? "today" : days == 1 ? "yesterday" : "\(days) days ago"
}

func age(_ days: Int) -> String {
    switch days {
    case ..<14: ago(days)
    case ..<60: "\(days / 7) weeks ago"
    case ..<730: "\(days / 30) months ago"
    default: "\(days / 365) years ago"
    }
}

// Mirror of Book.PagesFromHours: the audiobook forms convert before they send.
func pagesFromHours(_ hours: Double?, _ audioHours: Double?, _ pageCount: Int?) -> Int? {
    guard let hours, let audioHours, audioHours > 0, let pageCount, pageCount > 0 else { return nil }
    return Int((hours / audioHours * Double(pageCount)).rounded())
}

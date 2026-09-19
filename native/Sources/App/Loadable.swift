import Foundation

// The state every page owner exposes to its view.
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

// "0.#" — one decimal, dropped when it is zero.
func trimmed(_ value: Double) -> String {
    let rounded = (value * 10).rounded() / 10
    return rounded == rounded.rounded() ? String(Int(rounded)) : String(format: "%.1f", rounded)
}

// "#,0" — grouped, no decimals.
func grouped(_ value: Double) -> String {
    let formatter = NumberFormatter()
    formatter.numberStyle = .decimal
    formatter.maximumFractionDigits = 0
    formatter.locale = Locale(identifier: "en_US")
    return formatter.string(from: NSNumber(value: value)) ?? String(Int(value))
}

// Ratings live on /10 in the model; the app speaks stars.
func stars(_ tenScale: Double) -> String { trimmed(tenScale / 2) }

func ago(_ days: Int) -> String {
    days <= 0 ? "today" : days == 1 ? "yesterday" : "\(days) days ago"
}

// Mirrors Book.PagesFromHours: hours only convert when both lengths are known.
func pagesFromHours(_ hours: Double?, _ audioHours: Double?, _ pageCount: Int?) -> Int? {
    guard let hours, let audioHours, audioHours > 0, let pageCount, pageCount > 0 else { return nil }
    return Int((hours / audioHours * Double(pageCount)).rounded())
}

import Foundation

// System.Text.Json writes camelCase and ISO-8601 timestamps with seven
// fractional digits and, for SQLite-read values, no zone. Both ends here.
enum JSON {
    static let decoder: JSONDecoder = {
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .custom { decoder in
            let raw = try decoder.singleValueContainer().decode(String.self)
            guard let date = parseTimestamp(raw) else {
                throw DecodingError.dataCorrupted(.init(codingPath: decoder.codingPath,
                    debugDescription: "Not a timestamp: \(raw)"))
            }
            return date
        }
        return decoder
    }()

    static let encoder: JSONEncoder = {
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .custom { date, encoder in
            var container = encoder.singleValueContainer()
            try container.encode(iso.string(from: date))
        }
        return encoder
    }()

    private static let iso: ISO8601DateFormatter = {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        return formatter
    }()

    private static let isoWhole: ISO8601DateFormatter = {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime]
        return formatter
    }()

    private static func parseTimestamp(_ raw: String) -> Date? {
        var text = raw
        // Foundation parses at most three fractional digits.
        if let dot = text.firstIndex(of: ".") {
            let tail = text[text.index(after: dot)...]
            let digits = tail.prefix { $0.isNumber }
            let rest = tail.dropFirst(digits.count)
            text = String(text[..<dot]) + "." + String(digits.prefix(3)) + String(rest)
        }
        if !text.hasSuffix("Z"), !text.contains("+"), text.lastIndex(of: "-").map({ text.distance(from: text.startIndex, to: $0) }) ?? 0 < 10 {
            text += "Z"
        }
        return text.contains(".") ? iso.date(from: text) : isoWhole.date(from: text)
    }
}

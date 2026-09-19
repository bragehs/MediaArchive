import SwiftUI
import UIKit

// Decoded covers, keyed by URL, shared by every surface — an item filed under
// three genres is one texture. file:// (the cache) and https:// (not yet cached).
final class ImageCache: @unchecked Sendable {
    static let shared = ImageCache()

    private let cache = NSCache<NSString, UIImage>()
    private let lock = NSLock()
    private var inflight: [String: Task<UIImage?, Never>] = [:]

    func cached(_ url: String) -> UIImage? {
        cache.object(forKey: url as NSString)
    }

    func image(_ url: String) async -> UIImage? {
        if let hit = cached(url) { return hit }

        let task = lock.withLock { () -> Task<UIImage?, Never> in
            if let running = inflight[url] { return running }
            let started = Task.detached(priority: .utility) { await Self.load(url) }
            inflight[url] = started
            return started
        }

        let image = await task.value
        lock.withLock { inflight[url] = nil }
        if let image { cache.setObject(image, forKey: url as NSString) }
        return image
    }

    private static func load(_ url: String) async -> UIImage? {
        guard let target = URL(string: url) else { return nil }
        let data: Data?
        if target.isFileURL {
            data = try? Data(contentsOf: target)
        } else {
            data = try? await URLSession.shared.data(from: target).0
        }
        guard let data, let image = UIImage(data: data) else { return nil }
        return await image.byPreparingForDisplay() ?? image
    }
}

// Real art when there is any, the typographic tile when there isn't. The
// parent sets the frame; this fills and clips.
struct CoverImage: View {
    let url: String?
    let title: String
    var fallbackPadding: CGFloat = 6
    var fallbackSize: CGFloat = 8.5

    @State private var image: UIImage?

    init(url: String?, title: String, fallbackPadding: CGFloat = 6, fallbackSize: CGFloat = 8.5) {
        self.url = url
        self.title = title
        self.fallbackPadding = fallbackPadding
        self.fallbackSize = fallbackSize
        _image = State(initialValue: url.flatMap { ImageCache.shared.cached($0) })
    }

    var body: some View {
        Rectangle()
            .fill(Palette.coverFallback)
            .overlay {
                if let image {
                    Image(uiImage: image)
                        .resizable()
                        .scaledToFill()
                } else {
                    Text(title)
                        .font(Fonts.display(fallbackSize, bold: true))
                        .foregroundStyle(Palette.ink)
                        .shadow(color: .black.opacity(0.5), radius: 2, y: 1)
                        .lineLimit(5)
                        .multilineTextAlignment(.leading)
                        .padding(fallbackPadding)
                        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .bottomLeading)
                }
            }
            .clipped()
            .task(id: url) {
                guard let url else { image = nil; return }
                if let hit = ImageCache.shared.cached(url) { image = hit; return }
                image = await ImageCache.shared.image(url)
            }
    }
}

// The bordered 2:3 tile every rail and grid uses.
struct CoverTile: View {
    let url: String?
    let title: String
    var width: CGFloat? = nil
    var radius: CGFloat = 8
    var fallbackPadding: CGFloat = 6
    var fallbackSize: CGFloat = 8.5

    var body: some View {
        // A fixed width fixes the height too; otherwise the ratio derives the
        // height from whatever width the container proposes (a grid cell).
        CoverImage(url: url, title: title, fallbackPadding: fallbackPadding, fallbackSize: fallbackSize)
            .frame(width: width, height: width.map { $0 * 1.5 })
            .aspectRatio(width == nil ? 2 / 3 : nil, contentMode: .fit)
            .clipShape(RoundedRectangle(cornerRadius: radius))
            .overlay(RoundedRectangle(cornerRadius: radius).stroke(Palette.line, lineWidth: 1))
    }
}

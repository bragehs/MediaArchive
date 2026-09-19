import Foundation

enum BackendError: LocalizedError {
    case detached
    case remote(String)
    case empty

    var errorDescription: String? {
        switch self {
        case .detached: "The app's data layer is not attached."
        case .remote(let message): message
        case .empty: "Not found."
        }
    }
}

struct NoArgs: Encodable {}

// The Swift end of the boundary. Every request is three NSStrings into one
// exported C# selector; the reply comes back through MANativeApp.complete with
// the request id, so callers see plain async/await and never the bridge.
final class Backend: @unchecked Sendable {
    static let shared = Backend()

    private typealias Invoke = @convention(c) (AnyObject, Selector, NSString, NSString, NSString) -> Void

    private let selector = NSSelectorFromString("call:args:requestId:")
    private let lock = NSLock()
    private var target: NSObject?
    private var invoke: Invoke?
    private var pending: [String: CheckedContinuation<String, Error>] = [:]

    func attach(_ backend: NSObject) {
        guard backend.responds(to: selector) else { return }
        lock.withLock {
            target = backend
            invoke = unsafeBitCast(backend.method(for: selector), to: Invoke.self)
        }
    }

    func call<Args: Encodable, Reply: Decodable>(_ route: String, _ args: Args) async throws -> Reply {
        let reply = try await send(route, args)
        // A route that found nothing returns a bare null; the contract types are non-optional.
        if reply == "null" { throw BackendError.empty }
        return try JSON.decoder.decode(Reply.self, from: Data(reply.utf8))
    }

    func perform<Args: Encodable>(_ route: String, _ args: Args) async throws {
        _ = try await send(route, args)
    }

    private func send<Args: Encodable>(_ route: String, _ args: Args) async throws -> String {
        let (target, invoke) = lock.withLock { (self.target, self.invoke) }
        guard let target, let invoke else { throw BackendError.detached }

        let data = try JSON.encoder.encode(args)
        let payload = String(decoding: data, as: UTF8.self)
        let id = UUID().uuidString

        return try await withCheckedThrowingContinuation { continuation in
            lock.withLock { pending[id] = continuation }
            invoke(target, selector, route as NSString, payload as NSString, id as NSString)
        }
    }

    func complete(id: String, json: String?, error: String?) {
        guard let continuation = lock.withLock({ pending.removeValue(forKey: id) }) else { return }
        if let error {
            continuation.resume(throwing: BackendError.remote(error))
        } else {
            continuation.resume(returning: json ?? "null")
        }
    }
}

let api = Api(backend: .shared)

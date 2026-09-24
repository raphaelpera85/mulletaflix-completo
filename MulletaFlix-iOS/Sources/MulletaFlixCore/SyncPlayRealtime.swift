import Foundation

public enum SyncPlayRealtimeEvent: Equatable, Sendable {
    case groupUpdate(SyncPlayGroupUpdate)
    case command(SyncPlayCommand)
    case unknown(type: String)
}

public struct SyncPlayGroupUpdate: Decodable, Equatable, Sendable {
    public let type: String?
    public let groupId: String?
    public let groupName: String?
    public let state: String?

    public init(type: String? = nil, groupId: String? = nil, groupName: String? = nil, state: String? = nil) {
        self.type = type
        self.groupId = groupId
        self.groupName = groupName
        self.state = state
    }

    private enum CodingKeys: String, CodingKey {
        case type = "Type"
        case groupId = "GroupId"
        case groupName = "GroupName"
        case state = "State"
    }
}

public struct SyncPlayCommand: Decodable, Equatable, Sendable {
    public let name: String?
    public let positionTicks: Int64?

    public init(name: String? = nil, positionTicks: Int64? = nil) {
        self.name = name
        self.positionTicks = positionTicks
    }

    private enum CodingKeys: String, CodingKey {
        case name = "Command"
        case positionTicks = "PositionTicks"
    }
}

public enum SyncPlayRealtimeDecoder {
    private struct Envelope: Decodable {
        let messageType: String
        let data: Payload?

        private enum CodingKeys: String, CodingKey {
            case messageType = "MessageType"
            case data = "Data"
        }
    }

    private enum Payload: Decodable {
        case string(String)
        case object(Data)

        init(from decoder: Decoder) throws {
            if let value = try? decoder.singleValueContainer().decode(String.self) {
                self = .string(value)
                return
            }
            self = .object(try JSONEncoder().encode(AnyJSONValue(from: decoder)))
        }
    }

    private enum AnyJSONValue: Encodable {
        case object([String: AnyJSONValue])
        case array([AnyJSONValue])
        case string(String)
        case integer(Int64)
        case number(Double)
        case bool(Bool)
        case null

        init(from decoder: Decoder) throws {
            let container = try decoder.singleValueContainer()
            if container.decodeNil() { self = .null }
            else if let value = try? container.decode(Bool.self) { self = .bool(value) }
            else if let value = try? container.decode(Int64.self) { self = .integer(value) }
            else if let value = try? container.decode(Double.self) { self = .number(value) }
            else if let value = try? container.decode(String.self) { self = .string(value) }
            else if let value = try? container.decode([String: AnyJSONValue].self) { self = .object(value) }
            else { self = .array(try container.decode([AnyJSONValue].self)) }
        }

        func encode(to encoder: Encoder) throws {
            switch self {
            case .object(let value): try value.encode(to: encoder)
            case .array(let value): try value.encode(to: encoder)
            case .string(let value): try value.encode(to: encoder)
            case .integer(let value): try value.encode(to: encoder)
            case .number(let value): try value.encode(to: encoder)
            case .bool(let value): try value.encode(to: encoder)
            case .null: var container = encoder.singleValueContainer(); try container.encodeNil()
            }
        }
    }

    public static func decode(_ data: Data) throws -> SyncPlayRealtimeEvent {
        let envelope = try JSONDecoder().decode(Envelope.self, from: data)
        guard let payload = envelope.data else { return .unknown(type: envelope.messageType) }
        let payloadData: Data
        switch payload {
        case .string(let value): payloadData = Data(value.utf8)
        case .object(let value): payloadData = value
        }
        switch envelope.messageType {
        case "SyncPlayGroupUpdate":
            return .groupUpdate(try JSONDecoder().decode(SyncPlayGroupUpdate.self, from: payloadData))
        case "SyncPlayCommand":
            return .command(try JSONDecoder().decode(SyncPlayCommand.self, from: payloadData))
        default:
            return .unknown(type: envelope.messageType)
        }
    }
}

public final class SyncPlayRealtimeClient: @unchecked Sendable {
    private let session: URLSession
    private var task: URLSessionWebSocketTask?
    private var receiveTask: Task<Void, Never>?

    public init(session: URLSession = .shared) {
        self.session = session
    }

    public static func webSocketURL(serverURL: URL, accessToken: String?, deviceID: String = "ios-unknown") -> URL? {
        guard var components = URLComponents(url: serverURL, resolvingAgainstBaseURL: false) else { return nil }
        components.scheme = components.scheme == "https" ? "wss" : "ws"
        let basePath = components.path.isEmpty ? "" : "/" + components.path.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        components.path = basePath + "/socket"
        components.queryItems = [
            URLQueryItem(name: "api_key", value: accessToken),
            URLQueryItem(name: "deviceId", value: deviceID),
            URLQueryItem(name: "deviceName", value: "MulletaFlix iOS"),
            URLQueryItem(name: "appVersion", value: "0.1.0")
        ].filter { $0.value != nil }
        return components.url
    }

    public func connect(serverURL: URL, accessToken: String?, deviceID: String = "ios-unknown") -> AsyncStream<SyncPlayRealtimeEvent> {
        disconnect()
        guard let url = Self.webSocketURL(serverURL: serverURL, accessToken: accessToken, deviceID: deviceID) else {
            return AsyncStream { continuation in continuation.finish() }
        }
        let stream = AsyncStream<SyncPlayRealtimeEvent> { continuation in
            self.receiveTask = Task { [weak self] in
                guard let self else { return }
                var socket = self.session.webSocketTask(with: url)
                socket.resume()
                self.task = socket
                var reconnectAttempt = 0
                while !Task.isCancelled {
                    do {
                        let message = try await socket.receive()
                        reconnectAttempt = 0
                        let data: Data?
                        switch message {
                        case .data(let value): data = value
                        case .string(let value): data = Data(value.utf8)
                        @unknown default: data = nil
                        }
                        if let data, let event = try? SyncPlayRealtimeDecoder.decode(data) {
                            continuation.yield(event)
                        }
                    } catch {
                        guard reconnectAttempt < 3, !Task.isCancelled else {
                            continuation.finish()
                            break
                        }
                        reconnectAttempt += 1
                        let delay = UInt64(reconnectAttempt) * 500_000_000
                        try? await Task.sleep(nanoseconds: delay)
                        guard !Task.isCancelled else { break }
                        socket = self.session.webSocketTask(with: url)
                        socket.resume()
                        self.task = socket
                    }
                }
                self.task = nil
            }
            continuation.onTermination = { [weak self] _ in self?.disconnect() }
        }
        return stream
    }

    public func disconnect() {
        receiveTask?.cancel()
        receiveTask = nil
        task?.cancel(with: .goingAway, reason: nil)
        task = nil
    }

    deinit { disconnect() }
}

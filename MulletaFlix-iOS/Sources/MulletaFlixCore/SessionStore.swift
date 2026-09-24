import Foundation
import Security

public protocol SessionStore: Sendable {
    func load() -> UserSession?
    func save(_ session: UserSession) throws
    func clear()
}

public enum SessionStoreError: Error, Equatable {
    case encodingFailed
    case keychain(OSStatus)
}

public final class KeychainSessionStore: SessionStore, @unchecked Sendable {
    private let service: String
    private let account: String

    public init(service: String = "org.mulletaflix.ios", account: String = "session") {
        self.service = service
        self.account = account
    }

    public func load() -> UserSession? {
        var query = baseQuery
        query[kSecReturnData as String] = true
        query[kSecMatchLimit as String] = kSecMatchLimitOne
        var result: CFTypeRef?
        guard SecItemCopyMatching(query as CFDictionary, &result) == errSecSuccess,
              let data = result as? Data else { return nil }
        return try? JSONDecoder().decode(UserSession.self, from: data)
    }

    public func save(_ session: UserSession) throws {
        guard let data = try? JSONEncoder().encode(session) else { throw SessionStoreError.encodingFailed }
        let status = SecItemUpdate(baseQuery as CFDictionary, [kSecValueData as String: data] as CFDictionary)
        if status == errSecItemNotFound {
            var item = baseQuery
            item[kSecValueData as String] = data
            let addStatus = SecItemAdd(item as CFDictionary, nil)
            guard addStatus == errSecSuccess else { throw SessionStoreError.keychain(addStatus) }
        } else if status != errSecSuccess {
            throw SessionStoreError.keychain(status)
        }
    }

    public func clear() {
        SecItemDelete(baseQuery as CFDictionary)
    }

    private var baseQuery: [String: Any] {
        [kSecClass as String: kSecClassGenericPassword, kSecAttrService as String: service, kSecAttrAccount as String: account]
    }
}

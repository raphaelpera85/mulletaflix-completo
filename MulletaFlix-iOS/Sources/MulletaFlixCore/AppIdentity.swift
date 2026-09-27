import Foundation

/// Runtime identity shared by HTTP and realtime integrations.
///
/// Keep this value aligned with MARKETING_VERSION in the Xcode Release target.
public enum AppIdentity {
    public static let version = "1.0.0"
    public static let userAgent = "MulletaFlix-iOS/\(version)"
}

/// Canonical language values shared by settings, playback and server requests.
public enum MediaLanguage {
    public static let off = "off"
    public static let original = "original"

    public static func canonicalize(_ value: String?) -> String {
        let normalized = value?
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .lowercased()
            .replacingOccurrences(of: "_", with: "-") ?? ""
        guard !normalized.isEmpty else { return "" }

        switch normalized {
        case "off", "none", "disabled", "desativadas", "desabilitadas": return off
        case "original", "auto", "default": return original
        case "por", "pt", "pt-br", "pt-pt": return "pt"
        case "eng", "en", "en-us", "en-gb": return "en"
        case "spa", "es", "es-es", "es-mx": return "es"
        case "fra", "fre", "fr", "fr-fr": return "fr"
        case "deu", "ger", "de", "de-de": return "de"
        case "ita", "it", "it-it": return "it"
        case "jpn", "ja", "ja-jp": return "ja"
        case "kor", "ko", "ko-kr": return "ko"
        case "zho", "chi", "zh", "zh-cn", "zh-tw": return "zh"
        default: return normalized
        }
    }
}

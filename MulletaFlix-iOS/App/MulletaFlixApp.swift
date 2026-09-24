import SwiftUI
import UIKit

@MainActor
final class MulletaFlixAppDelegate: NSObject, UIApplicationDelegate {
    func application(
        _ application: UIApplication,
        handleEventsForBackgroundURLSession identifier: String,
        completionHandler: @escaping () -> Void
    ) {
        guard identifier == OfflineDownloadCoordinator.sessionIdentifier else {
            completionHandler()
            return
        }
        OfflineDownloadCoordinator.registerBackgroundCompletionHandler(completionHandler)
    }
}

@main
@MainActor
struct MulletaFlixApp: App {
    @UIApplicationDelegateAdaptor(MulletaFlixAppDelegate.self) private var appDelegate
    @State private var model = AppModel()

    var body: some Scene {
        WindowGroup {
            RootView()
                .environment(model)
                .preferredColorScheme(preferredColorScheme)
                .tint(themeTint)
        }
    }

    private var preferredColorScheme: ColorScheme? {
        switch model.themePreference {
        case .system: return nil
        case .light: return .light
        case .dark, .midnight, .ocean, .forest, .sunset, .violet: return .dark
        }
    }

    private var themeTint: Color {
        switch model.themePreference {
        case .system, .dark, .light: return .red
        case .midnight: return .indigo
        case .ocean: return .cyan
        case .forest: return .green
        case .sunset: return .orange
        case .violet: return .purple
        }
    }
}

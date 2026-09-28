import SwiftUI
import Foundation
import VisionKit

struct RootView: View {
    @Environment(AppModel.self) private var model

    var body: some View {
        Group {
            switch model.state {
            case .signedOut, .loading: LoginView()
            case .signedIn: HomeView()
            }
        }
        .onOpenURL { url in
            model.receiveDeepLink(url)
        }
    }
}

struct LoginView: View {
    @Environment(AppModel.self) private var model
    @State private var accessMode = 0
    @State private var showingRegistration = false
    @State private var showingQRScanner = false

    var body: some View {
        @Bindable var model = model
        NavigationStack {
            Form {
                Section {
                    Text("MulletaFlix")
                        .font(.largeTitle.bold())
                        .foregroundStyle(.red)
                    Text("Sua biblioteca, em qualquer lugar.")
                        .foregroundStyle(.secondary)
                }
                Section("Servidor") {
                    TextField("URL do servidor", text: $model.serverURL)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                        .keyboardType(.URL)
                    Button {
                        Task { await model.discoverServers() }
                    } label: {
                        Label(model.isDiscoveringServers ? "Procurando…" : "Encontrar na rede local", systemImage: "dot.radiowaves.left.and.right")
                    }
                    .disabled(model.isDiscoveringServers)
                    if DataScannerViewController.isSupported && DataScannerViewController.isAvailable {
                        Button {
                            showingQRScanner = true
                        } label: {
                            Label("Ler QR do servidor", systemImage: "qrcode.viewfinder")
                        }
                        .disabled(model.state == .loading)
                    }
                    Button {
                        Task { await model.verifyServer() }
                    } label: {
                        Label(model.isVerifyingServer ? "Verificando…" : "Verificar servidor", systemImage: "checkmark.shield")
                    }
                    .disabled(model.isVerifyingServer || model.state == .loading)
                    if let info = model.serverInfo {
                        Label {
                            Text(info.version.map { "\(info.displayName) • v\($0)" } ?? info.displayName)
                        } icon: {
                            Image(systemName: "checkmark.circle.fill")
                                .foregroundStyle(.green)
                        }
                        .font(.footnote)
                    }
                    if let health = model.serverHealth {
                        Label("Saúde: \(health)", systemImage: "heart.text.square.fill")
                            .font(.footnote)
                            .foregroundStyle(.secondary)
                    }
                    if let disclaimer = model.branding?.loginDisclaimer?.trimmingCharacters(in: .whitespacesAndNewlines),
                       !disclaimer.isEmpty {
                        Text(disclaimer)
                            .font(.footnote)
                            .foregroundStyle(.secondary)
                            .textSelection(.enabled)
                    }
                    if !model.discoveredServers.isEmpty {
                        Picker("Servidor encontrado", selection: $model.serverURL) {
                            Text("Selecionar manualmente").tag("")
                            ForEach(model.discoveredServers, id: \.address) { server in
                                Text(server.name).tag(server.address.absoluteString)
                            }
                        }
                    }
                }
                if !model.savedServers.isEmpty {
                    Section("Servidores salvos") {
                        ForEach(model.savedServers) { server in
                            Button {
                                Task { await model.selectSavedServer(server) }
                            } label: {
                                HStack {
                                    Image(systemName: "server.rack")
                                    VStack(alignment: .leading) {
                                        Text(server.name)
                                        Text(server.url)
                                            .font(.caption)
                                            .foregroundStyle(.secondary)
                                    }
                                    Spacer()
                                    if let version = server.version {
                                        Text("v\(version)")
                                            .font(.caption2)
                                            .foregroundStyle(.secondary)
                                    }
                                }
                            }
                            .swipeActions {
                                Button(role: .destructive) {
                                    model.removeSavedServer(server)
                                } label: {
                                    Label("Remover", systemImage: "trash")
                                }
                            }
                        }
                    }
                }
                Picker("Método de acesso", selection: $accessMode) {
                    Text("Usuário e senha").tag(0)
                    Text("Quick Connect").tag(1)
                }
                .pickerStyle(.segmented)
                .onChange(of: accessMode) { _, newValue in
                    if newValue == 0 { model.cancelQuickConnect() }
                }
                if accessMode == 0 {
                    Section("Acesso") {
                        TextField("Usuário", text: $model.username)
                            .textInputAutocapitalization(.never)
                            .autocorrectionDisabled()
                        SecureField("Senha", text: $model.password)
                    }
                    Button(model.state == .loading ? "Conectando…" : "Entrar") {
                        Task { await model.signIn() }
                    }
                    .disabled(model.state == .loading || model.username.isEmpty)
                    .buttonStyle(.borderedProminent)
                    Button("Criar conta") {
                        showingRegistration = true
                    }
                    .disabled(model.state == .loading)
                } else {
                    Section("Quick Connect") {
                        Text("Gere um código e autorize o acesso no servidor MulletaFlix.")
                            .font(.footnote)
                            .foregroundStyle(.secondary)
                        if let code = model.quickConnectCode {
                            Text(code)
                                .font(.system(size: 36, weight: .bold, design: .monospaced))
                                .frame(maxWidth: .infinity)
                                .accessibilityLabel("Código Quick Connect \(code)")
                            if let seconds = model.quickConnectSecondsRemaining {
                                Text("Expira em \(seconds / 60):\(String(format: "%02d", seconds % 60))")
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                                    .frame(maxWidth: .infinity)
                            }
                        }
                        Button(model.isQuickConnectWaiting ? "Cancelar" : "Gerar código") {
                            if model.isQuickConnectWaiting {
                                model.cancelQuickConnect()
                            } else {
                                Task { await model.startQuickConnect() }
                            }
                        }
                        .disabled(model.state == .loading && !model.isQuickConnectWaiting)
                        .buttonStyle(.borderedProminent)
                    }
                }
                if let error = model.errorMessage {
                    Text(error).foregroundStyle(.red).accessibilityLabel("Erro: \(error)")
                }
            }
            .navigationTitle("Entrar")
        }
        .sheet(isPresented: $showingRegistration) {
            RegistrationView(model: model)
        }
        .sheet(isPresented: $showingQRScanner) {
            NavigationStack {
                QRCodeScannerView { payload in
                    showingQRScanner = false
                    guard let url = ServerURLPolicy.url(fromQRPayload: payload) else {
                        model.errorMessage = "QR inválido: informe uma URL HTTP ou HTTPS."
                        return
                    }
                    model.serverURL = url.absoluteString
                    Task { await model.verifyServer() }
                }
                .ignoresSafeArea()
                .navigationTitle("Ler QR do servidor")
                .navigationBarTitleDisplayMode(.inline)
                .toolbar {
                    ToolbarItem(placement: .cancellationAction) {
                        Button("Fechar") { showingQRScanner = false }
                    }
                }
            }
        }
    }
}

private struct QRCodeScannerView: UIViewControllerRepresentable {
    let onPayload: (String) -> Void

    func makeCoordinator() -> Coordinator { Coordinator(onPayload: onPayload) }

    func makeUIViewController(context: Context) -> DataScannerViewController {
        let scanner = DataScannerViewController(
            recognizedDataTypes: [.barcode(symbologies: [.qr])],
            qualityLevel: .balanced,
            recognizesMultipleItems: false,
            isHighFrameRateTrackingEnabled: false,
            isPinchToZoomEnabled: true,
            isGuidanceEnabled: true,
            isHighlightingEnabled: true
        )
        scanner.delegate = context.coordinator
        do { try scanner.startScanning() } catch { }
        return scanner
    }

    func updateUIViewController(_ scanner: DataScannerViewController, context: Context) { }

    final class Coordinator: NSObject, DataScannerViewControllerDelegate {
        let onPayload: (String) -> Void
        private var didEmit = false

        init(onPayload: @escaping (String) -> Void) {
            self.onPayload = onPayload
        }

        func dataScanner(_ dataScanner: DataScannerViewController, didAdd addedItems: [RecognizedItem], allItems: [RecognizedItem]) {
            guard !didEmit else { return }
            for item in addedItems {
                guard case .barcode(let barcode) = item,
                      let payload = barcode.payloadStringValue,
                      !payload.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { continue }
                didEmit = true
                onPayload(payload)
                return
            }
        }
    }
}

private struct RegistrationView: View {
    let model: AppModel
    @Environment(\.dismiss) private var dismiss
    @State private var username = ""
    @State private var password = ""
    @State private var confirmation = ""
    @State private var showingSuccess = false

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    Text("Crie sua conta para acessar o MulletaFlix.")
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                    TextField("E-mail ou usuário", text: $username)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                    SecureField("Senha", text: $password)
                    SecureField("Confirmar senha", text: $confirmation)
                }
                if let error = model.registrationError {
                    Section {
                        Text(error)
                            .foregroundStyle(.red)
                            .accessibilityLabel("Erro: \(error)")
                    }
                }
                Section {
                    Button(model.isRegistering ? "Cadastrando…" : "Cadastrar") {
                        Task {
                            if await model.register(username: username, password: password, confirmation: confirmation) {
                                showingSuccess = true
                            }
                        }
                    }
                    .disabled(model.isRegistering)
                    .buttonStyle(.borderedProminent)
                }
            }
            .navigationTitle("Criar conta")
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Voltar") { dismiss() }
                        .disabled(model.isRegistering)
                }
            }
            .alert("Cadastro realizado", isPresented: $showingSuccess) {
                Button("Continuar") { dismiss() }
            } message: {
                Text("Sua conta foi criada. Agora você pode entrar com suas credenciais.")
            }
        }
    }
}

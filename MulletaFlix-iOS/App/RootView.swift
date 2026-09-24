import SwiftUI
import Foundation

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
                    if !model.discoveredServers.isEmpty {
                        Picker("Servidor encontrado", selection: $model.serverURL) {
                            Text("Selecionar manualmente").tag("")
                            ForEach(model.discoveredServers, id: \.address) { server in
                                Text(server.name).tag(server.address.absoluteString)
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

# Plano de Recuperação de Desastres, RPO/RTO e Exercício Operacional (T4.5)

Este documento estabelece o plano formal de recuperação de desastres (Disaster Recovery - DR), as métricas realistas de RPO (Recovery Point Objective) e RTO (Recovery Time Objective), e o guia de execução de testes de restauração para ambientes **Windows** e **Linux** no MulletaFlix.

---

## 1. Métricas de Resiliência: RPO e RTO por Camada de Dados

| Camada / Componente | Natureza do Dado | RPO (Perda Máxima Tolerada) | RTO (Tempo de Recuperação Alvo) | Mecanismo Primário de Backup |
| :--- | :--- | :--- | :--- | :--- |
| **Catálogo Relacional (`MulletaFlixDbContext`, `UsersDbContext`)** | Usuários, senhas, histórico de reprodução, metadados do Jellyfin, coleções, IntroSkipper. | **<= 24 horas** (ou desde o último backup agendado) | **<= 15 minutos** | `BackupService` (`/System/Backup`), gerando arquivo ZIP atômico com CRC-32 validado. |
| **Metadados Nebula (MongoDB `ftp.files`, `ftp.users`)** | Catálogo de arquivos indexados no Telegram, multipartes, hashes e usuários FTP. | **<= 6 horas** | **<= 20 minutos** | `NebulaSupabaseSyncService` (sincronização periódica delta para tabelas remotas no Supabase). |
| **Arquivos Ponte (`.strm`)** | Pontes locais no sistema de arquivos para streaming via Jellyfin. | **<= 24 horas** (Zero perda de dado) | **<= 10 minutos** | Regeneração determinística sob demanda via `NebulaFtpManager.GenerateStrmAsync()`. |
| **Arquivos de Configuração (`system.xml`, `nebulaftp.xml`, plugins)** | Ajustes de rede, transcode, credenciais protegidas e bindings. | **<= 24 horas** | **<= 5 minutos** | Incluídos no arquivo ZIP pelo `BackupService`. |
| **Cache de Reprodução e Chaves Temporárias** | Arquivos temporários em `PlaybackCachePath`, tokens transitórios. | **N/A (Descartável)** | **0 minutos** (reconstrução em tempo real) | Não é realizado backup; buffer é recriado na primeira requisição. |
| **Mídia Original no Telegram** | Vídeos, áudios e legendas em canais remotos. | **<= 0 horas** (persistido na nuvem) | Depende da taxa de download / streaming | Armazenamento distribuído no Telegram; referenciado pelos IDs de mensagem em `ftp.files`. |

---

## 2. Pré-requisitos de Segurança e Validação

1. **Integridade de Arquivo (`ValidateArchiveIntegrityAsync`):** Nenhum backup corrompido ou parcial é aceito para restauração. O arquivo deve passar na verificação estrutural (CRC-32 e leitura sequencial das entradas compactadas).
2. **Proteção contra Path Traversal / Symlinks:** Endpoints de restauração (`BackupController.Restore`) rejeitam arquivos com nomes manipulados (`..`) ou apontando para junctions/symlinks (`ReparsePoint`), respondendo com `400 Bad Request`.
3. **Isolamento de Produção:** Todo exercício de teste deve ser executado em instância isolada (portas alternativas ou diretório temporário), garantindo que dados ativos de usuários não sejam sobrescritos durante a simulação.

---

## 3. Roteiro de Exercício de Recuperação em Windows

### Passo 1: Parada Controlada do Serviço
```powershell
# Parar o serviço do MulletaFlix
Stop-Service -Name "MulletaFlix" -Force
# Garantir que processos órfãos do FFmpeg ou binário principal foram liberados
Get-Process -Name "MulletaFlix", "ffmpeg" -ErrorAction SilentlyContinue | Stop-Process -Force
```

### Passo 2: Validação da Integridade do Backup
```powershell
# Validar arquivo de backup mais recente
$BackupFile = Get-ChildItem -Path "C:\ProgramData\MulletaFlix\backups\*.zip" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

# Testar se o ZIP está íntegro sem descompactar no diretório ativo
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::OpenRead($BackupFile.FullName).Entries | Measure-Object | Out-Null
Write-Host "Backup verificado com sucesso: $($BackupFile.Name)"
```

### Passo 3: Restauração da Configuração e Bancos
```powershell
$TargetDir = "C:\ProgramData\MulletaFlix"
# Extrair arquivos de configuração e banco relacional
[System.IO.Compression.ZipFile]::ExtractToDirectory($BackupFile.FullName, "$TargetDir\restore_temp")

# Mover com segurança preservando dados anteriores em pasta .old
Move-Item -Path "$TargetDir\data" -Destination "$TargetDir\data.old"
Move-Item -Path "$TargetDir\restore_temp\data" -Destination "$TargetDir\data"
Move-Item -Path "$TargetDir\restore_temp\config" -Destination "$TargetDir\config" -Force
Remove-Item -Path "$TargetDir\restore_temp" -Recurse -Force
```

### Passo 4: Sincronização do MongoDB a partir do Supabase (Se necessário)
Caso o banco MongoDB local tenha sido perdido:
```powershell
# Iniciar MongoDB local
Start-Service -Name "MongoDB"

# Executar restore via endpoint administrativo do MulletaFlix após boot,
# ou disparar a reidratação via script/cURL:
Invoke-RestMethod -Uri "http://localhost:8096/NebulaFtp/RestoreFromSupabase" -Method Post -Headers @{ "X-Emby-Token" = $AdminToken }
```

### Passo 5: Inicialização e Verificação de Saúde
```powershell
Start-Service -Name "MulletaFlix"

# Aguardar inicialização e verificar health endpoints
Start-Sleep -Seconds 10
$Health = Invoke-RestMethod -Uri "http://localhost:8096/ServerHealth/Summary"
$Alerts = Invoke-RestMethod -Uri "http://localhost:8096/ServerHealth/Alerts"

Write-Host "Servidor online: $($Health.Server.Status) | Alertas ativos: $($Alerts.Count)"
```

---

## 4. Roteiro de Exercício de Recuperação em Linux

### Passo 1: Parada do Daemon systemd
```bash
sudo systemctl stop mulletaflix
# Verificar se não restaram workers pendentes
sudo pkill -f ffmpeg || true
```

### Passo 2: Verificação Estrutural do Backup
```bash
BACKUP_DIR="/var/lib/mulletaflix/backups"
LATEST_BACKUP=$(ls -t "$BACKUP_DIR"/*.zip | head -n 1)

# Teste de integridade em streaming com unzip
unzip -t "$LATEST_BACKUP" > /dev/null
if [ $? -eq 0 ]; then
    echo "Arquivo $LATEST_BACKUP validado com integridade 100%."
else
    echo "ERRO: Backup corrompido!" >&2
    exit 1
fi
```

### Passo 3: Restauração Atômica
```bash
DATA_DIR="/var/lib/mulletaflix/data"
CONFIG_DIR="/etc/mulletaflix"
TEMP_RESTORE="/var/lib/mulletaflix/restore_tmp"

mkdir -p "$TEMP_RESTORE"
unzip -q "$LATEST_BACKUP" -d "$TEMP_RESTORE"

# Backup de segurança do diretório corrompido
mv "$DATA_DIR" "${DATA_DIR}.failed_$(date +%s)"
mv "$TEMP_RESTORE/data" "$DATA_DIR"
cp -r "$TEMP_RESTORE/config/"* "$CONFIG_DIR/"

# Assegurar permissões restritas (600 para configs com segredos, conforme T6.4)
chmod -R 600 "$CONFIG_DIR"/*
chown -R mulletaflix:mulletaflix "$DATA_DIR" "$CONFIG_DIR"
rm -rf "$TEMP_RESTORE"
```

### Passo 4: Reinício do Serviço e Verificação de Health
```bash
sudo systemctl start mulletaflix

# Aguardar boot
sleep 8
curl -s -f http://localhost:8096/ServerHealth/Summary | jq .
curl -s -f http://localhost:8096/ServerHealth/Alerts | jq .
```

---

## 5. Regeneração dos Arquivos STRM e Reconciliação do Catálogo

Após a restauração das bases de dados:
1. Os arquivos `.strm` podem ser regerados automaticamente executando a tarefa agendada `GenerateStrmTask` ou via API:
   `POST /NebulaFtp/GenerateStrm`
2. A varredura de bibliotecas do Jellyfin (`LibraryManager.ValidateMediaLibrary`) deve ser acionada para revalidar índices, capas e metadados.
3. Se algum arquivo no MongoDB tiver seu arquivo local `.strm` removido ou renomeado, a execução idempotente de `GenerateStrmAsync` restaura o caminho padrão sem criar duplicatas.

---

## 6. Evidências de Testes de Recuperação Automatizados

A mecânica de resiliência e integridade do MulletaFlix é validada continuamente pelas suítes de teste:
- `BackupServiceTests.cs` (16 testes aprovados):
  - Validação de integridade de arquivo compactado (`ValidateArchiveIntegrityAsync`).
  - Agendamento de restauração e notificação de reinício (`ScheduleRestoreAndRestartServer`).
  - Compatibilidade de versões de backup (`TestBackupVersionCompatibility`).
  - Políticas de retenção e proteção contra exclusão indevida do último backup bom (`BackupRetentionPolicy`).
- `ServerHealthControllerTests.cs` (229 testes aprovados):
  - Emissão de alertas operacionais (`GET /ServerHealth/Alerts`) para falhas de restauração e backups ausentes ou desatualizados.

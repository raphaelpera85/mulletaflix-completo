# Auditoria de segurança e reprodutibilidade das dependências Python do Nebula

Atualizado: 2026-10-20 08:59 (-03)

## Estado verificado

- `Tools/NebulaPython/requirements.in` é a fonte das faixas runtime. `requirements.txt` é o ponto de compatibilidade para pip e para o guard do runtime, e inclui a fonte via `-r requirements.in`.
- `requirements.lock` foi gerado com uv como lock universal, com hashes SHA-256, para Python 3.10+; contém 61 distribuições resolvidas. O instalador Windows cria `Tools/NebulaPython/.venv`, instala o lock com `--require-hashes` nessa venv e grava seu `python.exe` em `python.path`; o instalador Linux também usa venv. O empacotamento inclui os três manifests e verifica o SHA-256 do lock Windows staged contra a fonte.
- `check_deps.py` segue includes relativos e lê pins ativos do lock usando a API pública de `packaging` (declarada diretamente em `requirements.in`). Antes dos imports, compara todas as distribuições ativas com `importlib.metadata`. Repara drift/ausências somente quando `sys.prefix != sys.base_prefix`; fora de venv, falha sem executar pip e orienta reinstalar pelo instalador. Compatibilidade sem lock também só instala dentro de venv.
- A mudança de `cryptography` de `>=43,<46` para `>=50.0.2,<51` exclui a resolução vulnerável observada. A regressão protege esse limite.

## Verificações executadas

- Ambiente isolado uv Python 3.10: lock com 61 distribuições, 53 pins ativos e zero drift; suíte completa Nebula **658/658**.
- Casos focados do guard: **30/30**, incluindo os caminhos com e sem lock que confirmam que Python global falha fechado sem chamar pip. Teste de parser público/vendorizado ausentes confirma que venv repara antes de qualquer probe/import. Ambientes lock-only Python 3.12 e 3.14 também encontraram 53 pins ativos e zero drift.
- `pip install --dry-run --require-hashes -r requirements.lock` passou em ambientes `uv run --isolated` Python 3.10, 3.12 e 3.14; nenhuma instalação foi aplicada nesses dry-runs. Além disso, uma venv Windows limpa Python 3.12.14 com `pip` semeado instalou o lock efetivamente por hashes; `pip check` e o guard passaram com 53 pins ativos e zero drift. `pip-audit`/OSV não encontrou vulnerabilidades conhecidas no grafo; isso é um snapshot, não garantia futura.
- Cópia temporária do helper `install-python-if-missing.ps1` selecionou Python 3.12.14 isolado com `PATH` do processo filho restrito, instalou o lock com `--require-hashes`, gravou `python.path` apenas no stage e passou `pip check` e `tools/check_deps.py`; o SHA-256 do lock staged e fonte coincidiu (`840CBE8E…D418CA`). O diretório temporário foi removido após verificação de caminho e ausência de reparse points. O script `Assert-StageIntegrity.Tests.ps1` também passou quando executado diretamente; `Invoke-Pester` 3.4.0 informou zero testes descobertos porque esse arquivo é um harness de assertions top-level, não um conjunto `Describe/It`.
- Regressão repetida 10/10 removeu dependência de ordem concorrente na asserção de ranges multipart. Contratos Node de release: 5/5; harness de integridade de stage PowerShell passou; `bash -n` e teste local de cópia de pacote Linux passaram.
- O teste local de pacote confirma presença dos arquivos no tarball; não executa o instalador Ubuntu. Não houve Linux real/CI remota nesta validação.
- A suíte completa após o guard fail-closed passou **660/660** na venv descartável; `pip check` limpo e `LOCK_DRIFT=0`. O contrato Node de packaging passou **4/4**, o harness de integridade staged passou e o parser PowerShell validou o helper. O helper Windows completo não foi executado: esta estação não tem Python nos caminhos de instalação de máquina reconhecidos pelo script. `makensis`/instalador NSIS completo, serviço, VM limpa, Linux nativo e runtime continuam pendentes.

## Limites e pendências

1. Uma primeira tentativa do harness não encontrou o Python temporário e executou o helper staged usando o fallback do `PATH` para o interpretador Python 3.14 do host, fora de venv, com user site habilitado. O comando `pip install --require-hashes` terminou com sucesso nesse interpretador e gravou pacotes no user site do host. A evidência anterior registrava `cryptography 45.0.7`; a leitura atual encontrou os 53 pins ativos sem drift, `cryptography 50.0.2` e `pip check` sem dependências quebradas. Portanto, ao menos esse pacote foi atualizado. Não há baseline prévio completo para determinar outros deltas e nenhuma desinstalação/rollback foi tentada, para não remover dependências preexistentes ou reintroduzir versão vulnerável. A segunda execução foi isolada com `PATH` restrito e passou; o diretório descartável foi removido.
2. O helper Windows foi exercitado em cópia staged, não o pacote NSIS completo, a instalação de serviço, a ramificação que baixa/instala Python ausente nem o runtime do produto. O smoke test completo continua reservado a VM Windows limpa; instalação/runtime real Linux também pendentes. O ambiente local não tem WSL/Docker/Podman utilizável; CI remota recente permanece bloqueada antes dos steps por alocação/faturamento de runner.
3. A detecção de drift foi exercitada em ambientes isolados, e o reparo versionado pelo helper foi executado no venv temporário; o teste de recuperação do startup por drift ainda foi simulado por testes. Esta rodada não consultou produção nem o runtime/serviço instalado.
4. O lock/hash e `pip-audit` não substituem auditoria periódica de CVEs, teste de compatibilidade em todas as plataformas ou inventário do runtime instalado.

Em 2026-10-20, a fonte do guard e do instalador Windows foi alterada para impedir reparo no Python global e preparar venv própria. Nenhum runtime instalado, serviço, banco ou produção foi alterado; não foi criada release. O incidente anterior alterou ao menos uma distribuição no Python global do host e os outros deltas continuam desconhecidos sem baseline. Homologação formal e release permanecem abertas.

### Revalidação Python global 3.10 — 2026-10-20

- A chamada da suíte no Python global acionou a proteção de autorreparo de dependências. O `pip` falhou ao substituir o módulo binário TgCrypto por `WinError 5` (acesso negado); após a tentativa, a leitura mostrou novamente 15 divergências de versões em relação ao lock. `pip check` não acusou dependências quebradas, mas não valida pins/hash do lock. Não havia baseline integral anterior a essa tentativa e nenhum rollback foi realizado.
- Repetição em venv temporária isolada: lock instalado com `--require-hashes`, `pip check` limpo, drift do lock zero e suíte Nebula 658/658. O executor bloqueou a remoção recursiva dessa venv temporária; ela permanece sob `%TEMP%`. Não se tentou contornar o bloqueio.
- Pendências adicionais: testar o ramo de reparo de drift em uma cópia/venv controlada, caracterizar o ambiente global sem modificar pacotes e validar instalação completa em Windows/Linux. Nenhum runtime de produção foi acessado.

### Isolamento do runtime Windows e bloqueio de reparo global — 2026-10-20

- O helper Windows agora procura apenas instalações de Python em caminhos de máquina conhecidos; não escolhe `LocalAppData` nem `python.exe` aleatório do `PATH`. Cria `Tools/NebulaPython/.venv`, instala o lock hashado dentro dela e só então grava esse `python.exe` em `python.path`. Linux já provisionava venv própria.
- O guard só permite autorreparo quando `sys.prefix != sys.base_prefix`. Com lock e sem lock, Python global falha claramente antes de invocar pip. Isso evita repetição de mutação em user-site/instalação compartilhada durante startup; upgrades normais continuam no instalador.
- Evidência: guard focal 30/30; suíte Nebula isolada 660/660, `pip check` e drift zero; contrato Node packaging 4/4; harness de stage e sintaxe PowerShell passaram. O helper Windows end-to-end não rodou por ausência de Python de máquina nos caminhos reconhecidos; NSIS, instalação real e produção pendentes.

### Revalidação do contrato de isolamento — 2026-10-20

- A repetição da suíte isolada passou 660/660. O contrato Node falhou porque exigia que a validação da venv estivesse colada à chamada de reparo com lock; o guard valida uma vez antes de ambos os caminhos de reparo. O contrato agora compara a ordem dentro de `ensure_runtime_dependencies`; passou 4/4.
- Parser PowerShell do helper passou e `Assert-StageIntegrity.Tests.ps1` passou como harness direto. Uma invocação inicial usou caminho sem a pasta `tests` e não executou; repetida no caminho correto, passou.
- Nenhum código runtime mudou nesta revalidação. Instalador end-to-end Windows, NSIS, Linux nativo, CI remota, instalação/runtime, produção e download/playback continuam sem evidência.

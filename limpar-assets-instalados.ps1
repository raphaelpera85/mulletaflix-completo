[CmdletBinding()]
param(
    # Sem este switch o script apenas lista o que seria removido (dry-run).
    [switch]$Executar,

    # Pasta do cliente web instalado. O updater extrai por cima e nao remove arquivos de
    # versoes anteriores, entao cada release deixa para tras os seus chunks com hash.
    [string]$CaminhoWeb = 'C:\Program Files\MulletaFlix\Server\MulletaFlix-web',

    # A copia de wwwroot serve /assets/** e um /index.html antigo. Nao entra por padrao:
    # trocar 53 MB por risco de quebrar uma pagina antiga em cache nao compensa.
    [switch]$IncluirWwwroot,
    [string]$CaminhoWwwroot = 'C:\Program Files\MulletaFlix\Server\wwwroot'
)

$ErrorActionPreference = 'Stop'

# Um chunk do Vite tem hash de conteudo no nome (ex.: vendor-mui-h5Z8fq6u.js). Arquivo sem hash
# (themes/, libraries/, branding/, favicons/) e carregado por URL de runtime e NUNCA e removido.
$padraoHash = '-[A-Za-z0-9_-]{8}\.[a-z0-9]+$'

function Get-Alvos([string]$raiz, [string]$corte) {
    $indexHtml = Join-Path $raiz 'index.html'
    if (-not (Test-Path -LiteralPath $indexHtml)) {
        throw "index.html nao encontrado em $raiz"
    }

    # Nunca remover algo que o index.html da release vigente referencia, mesmo que a data
    # sugira o contrario: e a unica lista positiva de que dispomos.
    $referenciados = @{}
    foreach ($m in [regex]::Matches((Get-Content -LiteralPath $indexHtml -Raw), '[A-Za-z0-9_.-]+-[A-Za-z0-9_-]{8}\.[a-z0-9]+')) {
        $referenciados[$m.Value] = $true
    }

    Get-ChildItem -LiteralPath $raiz -Recurse -File |
        Where-Object {
            $_.Name -match $padraoHash -and
            $_.LastWriteTime -lt $corte -and
            -not $referenciados.ContainsKey($_.Name)
        }
}

$raizes = @($CaminhoWeb)
if ($IncluirWwwroot) { $raizes += $CaminhoWwwroot }

$alvos = @()
foreach ($raiz in $raizes) {
    $corte = (Get-Item -LiteralPath (Join-Path $raiz 'index.html')).LastWriteTime.AddMinutes(-5)
    Write-Host ("raiz {0}  corte {1}" -f $raiz, $corte.ToString('dd/MM/yyyy HH:mm:ss')) -ForegroundColor Cyan
    $alvos += Get-Alvos -raiz $raiz -corte $corte
}

$bytes = ($alvos | Measure-Object -Property Length -Sum).Sum
Write-Host ("arquivos a remover : {0}" -f $alvos.Count)
Write-Host ("espaco a liberar   : {0:N1} MB" -f ($bytes / 1MB))

if (-not $Executar) {
    $alvos | Select-Object -First 10 | ForEach-Object { Write-Host ("   {0}  {1}" -f $_.LastWriteTime.ToString('dd/MM HH:mm'), $_.Name) }
    Write-Host "`nDry-run. Para remover de verdade, rode como Administrador com -Executar." -ForegroundColor Yellow
    return
}

$administrador = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $administrador) {
    throw 'Remover arquivos em Program Files exige uma sessao elevada (Executar como Administrador).'
}

$manifest = Join-Path $PSScriptRoot ('limpeza-assets-{0}.txt' -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
$alvos | ForEach-Object { $_.FullName } | Set-Content -LiteralPath $manifest -Encoding UTF8

$falhas = @()
foreach ($alvo in $alvos) {
    try { Remove-Item -LiteralPath $alvo.FullName -Force -ErrorAction Stop }
    catch { $falhas += $alvo.FullName }
}

Write-Host ("`nremovidos : {0} de {1}" -f ($alvos.Count - $falhas.Count), $alvos.Count) -ForegroundColor Green
if ($falhas.Count -gt 0) {
    Write-Host ("falhas    : {0}" -f $falhas.Count) -ForegroundColor Yellow
    $falhas | Select-Object -First 5 | ForEach-Object { Write-Host ("   {0}" -f $_) }
}
Write-Host ("manifesto : {0}" -f $manifest)

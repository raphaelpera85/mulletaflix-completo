[CmdletBinding()]
param(
    [string] $RoadmapPath = (Join-Path $PSScriptRoot '..\docs\roadmap-evolucao-tecnologica-servidor-e-web.md'),
    [string] $StatusPath = (Join-Path $PSScriptRoot '..\portal-site\homologacao-status.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$status = Get-Content -LiteralPath $StatusPath -Raw -Encoding UTF8 | ConvertFrom-Json
$lines = Get-Content -LiteralPath $RoadmapPath -Encoding UTF8
$features = [System.Collections.Generic.List[object]]::new()
$childCounts = @{}
$parentId = $null
$parentPrefix = 'R'
$parentArea = 'Servidor'
$fallbackIndex = 0

foreach ($line in $lines) {
    if ($line -notmatch '^\s*(?<indent> *)- \[(?<checked>[ xX])\]\s+(?<body>.+)$') {
        continue
    }

    $checked = $Matches.checked
    $body = $Matches.body.Trim()
    $featureId = $null
    $name = $null

    if ($body -match '^\*\*(?<id>[A-Z]\d+\.\d+)\s*[—-]\s*(?<name>.+?)\*\*') {
        $parentId = $Matches.id
        $parentPrefix = $parentId.Substring(0, 1)
        $parentArea = if ($parentPrefix -eq 'W') { 'Web' } else { 'Servidor' }
        if ($parentId -eq 'T9.4') { $parentArea = 'Web' }
        if ($parentId -eq 'T9.5') { $parentArea = 'Web/QA' }
        if ($parentId -eq 'T9.3') { $parentArea = 'Windows/Linux' }
        $childCounts[$parentId] = 0
        $featureId = $parentId
        $name = $Matches.name
    } elseif ($parentId) {
        $childCounts[$parentId]++
        $featureId = '{0}.{1}' -f $parentId, $childCounts[$parentId]
        if ($body -match '^\*\*(?<name>.+?)\*\*') {
            $name = $Matches.name
        } else {
            $name = $body -replace '^\*\*|\*\*$', ''
        }
    } else {
        $fallbackIndex++
        $featureId = 'ROADMAP.{0:D3}' -f $fallbackIndex
        $name = $body -replace '^\*\*|\*\*$', ''
    }

    $name = [regex]::Replace($name, '\s+', ' ').Trim(' ', '*', '.', ';')
    if ($name.Length -gt 180) {
        $name = $name.Substring(0, 177).TrimEnd() + '…'
    }

    $taskStatus = if ($checked -match '[xX]') { 'completed' } else { 'pending' }
    $features.Add([pscustomobject]@{
        id = $featureId
        name = $name
        area = $parentArea
        status = $taskStatus
        source = 'roadmap'
    })
}

if ($features.Count -eq 0) {
    throw "Nenhum item de checklist encontrado em '$RoadmapPath'."
}

$duplicateIds = $features | Group-Object id | Where-Object Count -gt 1
if ($duplicateIds) {
    throw "IDs duplicados no roadmap: $($duplicateIds.Name -join ', ')"
}

$inProgressIds = @{}
foreach ($event in $status.events) {
    if ($event.status -ne 'in_progress') {
        continue
    }

    if ($event.PSObject.Properties.Name -contains 'featureIds') {
        foreach ($id in $event.featureIds) {
            $inProgressIds[[string]$id] = $true
        }
    } elseif ($event.title -match '\b(?<id>[TW]\d+\.\d+)\b') {
        $inProgressIds[$Matches.id] = $true
    }
}

foreach ($feature in $features) {
    if ($feature.status -eq 'pending' -and $inProgressIds.ContainsKey($feature.id)) {
        $feature.status = 'in_progress'
    }
}

$completed = @($features | Where-Object status -eq 'completed').Count
$pending = @($features | Where-Object status -eq 'pending').Count
$inProgress = @($features | Where-Object status -eq 'in_progress').Count
$status.coverage.completed = $completed
$status.coverage.pending = $pending
$status.coverage | Add-Member -NotePropertyName inProgress -NotePropertyValue $inProgress -Force
$status.coverage.total = $features.Count
$status.coverage.definition = 'Checklists de funcionalidades do roadmap (cada checkbox conta como um item); tarefas com evento ativo identificado aparecem como em execução.'
$status.features = @($features)
$status.generatedAt = [DateTimeOffset]::Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz", [Globalization.CultureInfo]::InvariantCulture)

$json = $status | ConvertTo-Json -Depth 100
[System.IO.File]::WriteAllText((Resolve-Path -LiteralPath $StatusPath), $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
Write-Output ("Itens sincronizados: {0} total, {1} concluídos, {2} pendentes, {3} em execução." -f $features.Count, $completed, $pending, $inProgress)

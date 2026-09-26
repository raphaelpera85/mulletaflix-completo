<#
.SYNOPSIS
    Cruza os títulos candidatos a novela (metadados TMDb do MariaDB) com as pastas
    reais da árvore do Nebula no MongoDB, gerando um relatório de conferência.

.DESCRIPTION
    Somente leitura. Produz artifacts/novelas-candidates.tsv com:
    Folder (pasta real no Mongo), SeriesName, Genre, Language, Year, Source e Match.
#>
[CmdletBinding()]
param(
    [string]$SeriesMetadata = 'artifacts/series-metadata.tsv',
    [string]$OutputPath = 'artifacts/novelas-candidates.tsv',
    [string]$ServerDir = 'C:\Program Files\MulletaFlix\Server',
    [string]$ConnectionString = 'mongodb://localhost:27017',
    [string]$Database = 'ftp',
    [string]$Collection = 'files',
    [string]$SeriesRootPath = 'raphael/Series'
)

$ErrorActionPreference = 'Stop'

Add-Type -Path (Join-Path $ServerDir 'MongoDB.Bson.dll')
Add-Type -Path (Join-Path $ServerDir 'MongoDB.Driver.dll')

$client = [MongoDB.Driver.MongoClient]::new($ConnectionString)
$db = $client.GetDatabase($Database)
$getCollection = $db.GetType().GetMethods() |
    Where-Object { $_.Name -eq 'GetCollection' -and $_.IsGenericMethod } |
    Select-Object -First 1
$collectionArgs = @($Collection)
foreach ($parameter in $getCollection.GetParameters() | Select-Object -Skip 1) { $collectionArgs += $null }
$col = $getCollection.MakeGenericMethod([MongoDB.Bson.BsonDocument]).Invoke($db, $collectionArgs)

$findSync = $col.GetType().GetMethods() |
    Where-Object { $_.Name -eq 'FindSync' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 3 } |
    Select-Object -First 1
$filter = [MongoDB.Driver.FilterDefinition[MongoDB.Bson.BsonDocument]]::Empty
$cursor = $findSync.MakeGenericMethod([MongoDB.Bson.BsonDocument]).Invoke(
    $col, @($filter, $null, [System.Threading.CancellationToken]::None))
$documents = [System.Collections.Generic.List[MongoDB.Bson.BsonDocument]]::new()
while ($cursor.MoveNext([System.Threading.CancellationToken]::None)) {
    foreach ($doc in $cursor.Current) { $documents.Add($doc) }
}

$byId = @{}
foreach ($doc in $documents) { $byId[$doc['_id'].ToString()] = $doc }

function Get-FieldValue {
    param($Doc, [string]$Name, [string]$Default = '')
    if ($Doc.Contains($Name) -and -not $Doc[$Name].IsBsonNull) { return $Doc[$Name].ToString() }
    return $Default
}

function Resolve-VirtualPath {
    param($Doc)
    $parts = [System.Collections.Generic.List[string]]::new()
    $current = $Doc
    $guard = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    while ($null -ne $current -and $current.Contains('name') -and $guard.Add($current['_id'].ToString())) {
        $parts.Insert(0, (Get-FieldValue -Doc $current -Name 'name'))
        $parent = Get-FieldValue -Doc $current -Name 'parent'
        if ([string]::IsNullOrEmpty($parent)) { break }
        if (-not $byId.ContainsKey($parent)) {
            $legacy = $parent.Replace('\', '/').Trim('/')
            if (-not [string]::IsNullOrEmpty($legacy)) {
                $segments = $legacy.Split('/', [System.StringSplitOptions]::RemoveEmptyEntries)
                for ($i = $segments.Length - 1; $i -ge 0; $i--) { $parts.Insert(0, $segments[$i]) }
            }
            break
        }
        $current = $byId[$parent]
    }
    return ($parts -join '/')
}

# Filhos diretos da raiz de séries, indexados pelo caminho virtual completo.
$seriesChildren = @{}
foreach ($doc in $documents) {
    $path = Resolve-VirtualPath -Doc $doc
    if ($path.StartsWith("$SeriesRootPath/") -and $path.Split('/').Count -eq ($SeriesRootPath.Split('/').Count + 1)) {
        $seriesChildren[$path] = $doc
    }
}
Write-Host "Pastas de primeiro nível em '$SeriesRootPath': $($seriesChildren.Count)"

$rows = Import-Csv $SeriesMetadata -Delimiter "`t"
$soap = $rows | Where-Object { $_.Genres -match 'Soap' }

$results = [System.Collections.Generic.List[object]]::new()
$seen = @{}
foreach ($item in $soap) {
    # Path do item na biblioteca: N:\Series\<pasta>
    $folder = $item.Path -replace '^[A-Za-z]:\\+Series\\+', ''
    $folder = $folder.Trim('\', '/')
    $key = $folder.ToLowerInvariant()
    if ($seen.ContainsKey($key)) { continue }
    $seen[$key] = $true

    $path = "$SeriesRootPath/$folder"
    $node = $null
    if ($seriesChildren.ContainsKey($path)) { $node = $seriesChildren[$path] }
    $results.Add([pscustomobject]@{
        Folder     = $folder
        SeriesName = $item.Name
        Genres     = $item.Genres
        Language   = $item.OriginalLanguage
        Year       = $item.ProductionYear
        Source     = 'TMDb-Soap'
        InMongo    = [bool]$node
        MongoId    = if ($node) { $node['_id'].ToString() } else { '' }
    })
}

$results | Sort-Object Folder | Export-Csv -Path $OutputPath -Delimiter "`t" -NoTypeInformation -Encoding utf8
Write-Host "Candidatos: $($results.Count) | presentes no MongoDB: $(@($results | Where-Object InMongo).Count) | ausentes: $(@($results | Where-Object { -not $_.InMongo }).Count)"
Write-Host "Relatório: $OutputPath"

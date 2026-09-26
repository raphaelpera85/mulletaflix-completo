<#
.SYNOPSIS
    Consulta somente-leitura da árvore virtual do Nebula no MongoDB (ftp.files).

.DESCRIPTION
    Carrega os assemblies do MongoDB.Driver instalados junto ao servidor MulletaFlix
    e imprime a árvore de diretórios/arquivos para inspeção, sem alterar nada.

.EXAMPLE
    pwsh -File tools/mongo-query.ps1 -Mode Tree -Root Series
#>
[CmdletBinding()]
param(
    [ValidateSet('Stats', 'Tree', 'Search', 'Roots', 'Children', 'Parents', 'Node')]
    [string]$Mode = 'Stats',

    [string]$Root = '',

    [string]$Pattern = '',

    [int]$Limit = 400,

    [string]$ServerDir = 'C:\Program Files\MulletaFlix\Server',

    [string]$ConnectionString = 'mongodb://localhost:27017',

    [string]$Database = 'ftp',

    [string]$Collection = 'files'
)

$ErrorActionPreference = 'Stop'

Add-Type -Path (Join-Path $ServerDir 'MongoDB.Bson.dll')
Add-Type -Path (Join-Path $ServerDir 'MongoDB.Driver.dll')

$client = [MongoDB.Driver.MongoClient]::new($ConnectionString)
$db = $client.GetDatabase($Database)

# O PowerShell não resolve a sobrecarga genérica GetCollection<T> diretamente;
# a invocação é feita por reflexão fechando o tipo em BsonDocument.
$getCollection = $db.GetType().GetMethods() |
    Where-Object { $_.Name -eq 'GetCollection' -and $_.IsGenericMethod } |
    Select-Object -First 1
$collectionArgs = @($Collection)
foreach ($parameter in $getCollection.GetParameters() | Select-Object -Skip 1) { $collectionArgs += $null }
$col = $getCollection.MakeGenericMethod([MongoDB.Bson.BsonDocument]).Invoke($db, $collectionArgs)

function Get-AllDocuments {
    $docs = [System.Collections.Generic.List[MongoDB.Bson.BsonDocument]]::new()
    $filter = [MongoDB.Driver.FilterDefinition[MongoDB.Bson.BsonDocument]]::Empty
    # FindSync<TProjection> precisa de tipo genérico fechado; o PowerShell não
    # infere genéricos, então a chamada é feita por reflexão.
    $findSync = $col.GetType().GetMethods() |
        Where-Object { $_.Name -eq 'FindSync' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 3 } |
        Select-Object -First 1
    $cursor = $findSync.MakeGenericMethod([MongoDB.Bson.BsonDocument]).Invoke(
        $col,
        @($filter, $null, [System.Threading.CancellationToken]::None))
    while ($cursor.MoveNext([System.Threading.CancellationToken]::None)) {
        foreach ($doc in $cursor.Current) { $docs.Add($doc) }
    }
    return $docs
}

function Get-FieldValue {
    param($Doc, [string]$Name, [string]$Default = '')
    if ($Doc.Contains($Name) -and -not $Doc[$Name].IsBsonNull) { return $Doc[$Name].ToString() }
    return $Default
}

$documents = Get-AllDocuments

$byId = @{}
foreach ($doc in $documents) {
    if ($doc.Contains('_id')) { $byId[$doc['_id'].ToString()] = $doc }
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

switch ($Mode) {
    'Stats' {
        $dirs = @($documents | Where-Object { (Get-FieldValue -Doc $_ -Name 'type') -eq 'dir' })
        $files = @($documents | Where-Object { (Get-FieldValue -Doc $_ -Name 'type') -ne 'dir' })
        Write-Host "Documentos: $($documents.Count) (pastas: $($dirs.Count), arquivos: $($files.Count))"
        Write-Host ''
        Write-Host '--- Campos presentes nos documentos de pasta ---'
        $fieldCounts = @{}
        foreach ($dir in $dirs) {
            foreach ($element in $dir.Elements) {
                if (-not $fieldCounts.ContainsKey($element.Name)) { $fieldCounts[$element.Name] = 0 }
                $fieldCounts[$element.Name]++
            }
        }
        $fieldCounts.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object { Write-Host ("  {0,-22} {1}" -f $_.Key, $_.Value) }
        Write-Host ''
        Write-Host '--- Documentos com media_type ---'
        $documents | Where-Object { $_.Contains('media_type') } |
            Group-Object { Get-FieldValue -Doc $_ -Name 'media_type' } |
            ForEach-Object { Write-Host ("  {0,-12} {1}" -f $_.Name, $_.Count) }
    }
    'Node' {
        # Detalhe bruto de um nó pelo caminho virtual (para inspecionar parent/estrutura).
        # BsonDocument enumera BsonElement ao passar pelo pipeline, por isso o laço explícito.
        $target = $null
        foreach ($candidate in $byId.Values) {
            if ((Resolve-VirtualPath -Doc $candidate) -eq $Root) { $target = $candidate; break }
        }
        if ($null -eq $target) { throw "Nó não encontrado: $Root" }
        Write-Host $target.ToJson()
    }
    'Parents' {
        # Agrupa os documentos pelo valor bruto do campo parent para separar
        # nós modernos (ObjectId) de nós legados (caminho virtual POSIX).
        $documents |
            Group-Object { Get-FieldValue -Doc $_ -Name 'parent' } |
            Sort-Object Count -Descending |
            Select-Object -First 40 |
            ForEach-Object {
                $isObjectId = $byId.ContainsKey($_.Name)
                Write-Host ("{0,6}  {1,-12} {2}" -f $_.Count, $(if ($isObjectId) { 'ObjectId' } else { 'path/legado' }), $_.Name)
            }
    }
    'Children' {
        # Filhos diretos de um nó resolvido pelo caminho virtual informado em -Root.
        $target = $null
        foreach ($candidate in $byId.Values) {
            if ((Resolve-VirtualPath -Doc $candidate) -eq $Root) { $target = $candidate; break }
        }
        if ($null -eq $target) { throw "Nó não encontrado: $Root" }
        Write-Host "Pai: $Root (id=$($target['_id']))"
        $index = 0
        foreach ($doc in $documents) {
            if ($doc['_id'].ToString() -eq $target['_id'].ToString()) { continue }
            $parent = Get-FieldValue -Doc $doc -Name 'parent'
            $isChild = $parent -eq $target['_id'].ToString() -or $parent.Replace('\', '/').Trim('/') -eq $Root
            if (-not $isChild) { continue }
            $index++
            Write-Host ("{0,4}. {1,-58} {2,-5} media_type={3} id={4}" -f `
                $index, (Get-FieldValue -Doc $doc -Name 'name'), (Get-FieldValue -Doc $doc -Name 'type'), `
                (Get-FieldValue -Doc $doc -Name 'media_type' -Default '-'), $doc['_id'])
        }
        Write-Host "Total de filhos: $index"
    }
    'Roots' {
        # Pastas cujo pai é vazio ou cujo pai não existe mais na coleção.
        foreach ($doc in $documents) {
            if ((Get-FieldValue -Doc $doc -Name 'type') -ne 'dir') { continue }
            $parent = Get-FieldValue -Doc $doc -Name 'parent'
            if ([string]::IsNullOrEmpty($parent) -or -not $byId.ContainsKey($parent)) {
                Write-Host ("{0,-34} id={1} parent='{2}'" -f (Get-FieldValue -Doc $doc -Name 'name'), $doc['_id'], $parent)
            }
        }
    }
    'Search' {
        foreach ($doc in $documents) {
            $name = Get-FieldValue -Doc $doc -Name 'name'
            if ($name -match $Pattern) {
                Write-Host ("{0,-24} {1,-9} {2}" -f $name, (Get-FieldValue -Doc $doc -Name 'type'), (Resolve-VirtualPath -Doc $doc))
            }
        }
    }
    'Tree' {
        $items = foreach ($doc in $documents) {
            $path = Resolve-VirtualPath -Doc $doc
            if ([string]::IsNullOrEmpty($Root) -or $path -like "$Root*") {
                [pscustomobject]@{
                    Path = $path
                    Type = Get-FieldValue -Doc $doc -Name 'type'
                    Id   = $doc['_id'].ToString()
                }
            }
        }
        $items | Sort-Object Path | Select-Object -First $Limit | Format-Table -AutoSize
    }
}

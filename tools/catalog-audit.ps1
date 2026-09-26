param(
    [string]$ServerDir = 'C:\Program Files\MulletaFlix\Server',
    [string]$ConnectionString = 'mongodb://localhost:27017'
)
Add-Type -Path (Join-Path $ServerDir 'MongoDB.Bson.dll')
Add-Type -Path (Join-Path $ServerDir 'MongoDB.Driver.dll')
$client = [MongoDB.Driver.MongoClient]::new($ConnectionString)
$db = $client.GetDatabase('ftp')
$getCollection = $db.GetType().GetMethods() |
    Where-Object { $_.Name -eq 'GetCollection' -and $_.IsGenericMethod } |
    Select-Object -First 1
$collectionArgs = @('files')
foreach ($parameter in $getCollection.GetParameters() | Select-Object -Skip 1) { $collectionArgs += $null }
$col = $getCollection.MakeGenericMethod([MongoDB.Bson.BsonDocument]).Invoke($db, $collectionArgs)
$findSync = $col.GetType().GetMethods() | Where-Object { $_.Name -eq 'FindSync' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 3 } | Select-Object -First 1
$cursor = $findSync.MakeGenericMethod([MongoDB.Bson.BsonDocument]).Invoke($col, @([MongoDB.Driver.FilterDefinition[MongoDB.Bson.BsonDocument]]::Empty, $null, [System.Threading.CancellationToken]::None))
$docs = [System.Collections.Generic.List[MongoDB.Bson.BsonDocument]]::new()
while ($cursor.MoveNext([System.Threading.CancellationToken]::None)) { foreach ($d in $cursor.Current) { $docs.Add($d) } }

$byId = @{}
foreach ($d in $docs) { $byId[$d['_id'].ToString()] = $d }

function Get-Path($doc) {
    $parts = [System.Collections.Generic.List[string]]::new()
    $curr = $doc
    $guard = [System.Collections.Generic.HashSet[string]]::new()
    while ($null -ne $curr -and $guard.Add($curr['_id'].ToString())) {
        if ($curr.Contains('name')) { $parts.Add($curr['name'].ToString()) }
        if (-not $curr.Contains('parent') -or $curr['parent'].IsBsonNull) { break }
        $p = $curr['parent'].ToString()
        if ($byId.ContainsKey($p)) {
            $curr = $byId[$p]
        } else {
            $pClean = $p.Trim('/').Replace('\', '/')
            if ($pClean.Length -gt 0) {
                $segments = $pClean.Split('/')
                [System.Array]::Reverse($segments)
                foreach ($s in $segments) { $parts.Add($s) }
            }
            break
        }
    }
    $arr = $parts.ToArray()
    [System.Array]::Reverse($arr)
    return ($arr -join '/')
}

$filesWithParts = $docs | Where-Object {
    $t = if ($_.Contains('type')) { $_['type'].ToString() } else { '' }
    $hasParts = $_.Contains('parts') -and $_['parts'].IsBsonArray -and $_['parts'].AsBsonArray.Count -gt 0
    $t -ne 'dir' -and $hasParts
}

Write-Host "Total de arquivos com partes no Telegram: $($filesWithParts.Count)"

$dirsWithFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($f in $filesWithParts) {
    $p = if ($f.Contains('parent') -and -not $f['parent'].IsBsonNull) { $f['parent'].ToString() } else { '' }
    if ($byId.ContainsKey($p)) {
        $dirsWithFiles.Add($byId[$p]['_id'].ToString()) | Out-Null
    }
    if ($p.Length -gt 0) {
        $dirsWithFiles.Add($p) | Out-Null
    }
}

# Pastas que têm arquivos em Series ou Series/Series
$titlesInSeries = [System.Collections.Generic.Dictionary[string, int]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($f in $filesWithParts) {
    $path = Get-Path $f
    if ($path -like '*Series*') {
        # Extrai nome do título
        $seg = $path.Split('/')
        $idx = [System.Array]::FindIndex($seg, [System.Predicate[string]]{ param($s) $s -eq 'Series' })
        if ($idx -ge 0 -and $idx + 1 -lt $seg.Length) {
            $title = $seg[$idx + 1]
            if ($title -eq 'Series' -and $idx + 2 -lt $seg.Length) {
                $title = $seg[$idx + 2]
            }
            if (-not $titlesInSeries.ContainsKey($title)) { $titlesInSeries[$title] = 0 }
            $titlesInSeries[$title]++
        }
    }
}

Write-Host "Titulos sob Series que contem arquivos reais ($($titlesInSeries.Count)):"
foreach ($kv in $titlesInSeries.GetEnumerator() | Sort-Object Name) {
    Write-Host "  $($kv.Key) ($($kv.Value) arquivos)"
}

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

$sakuraDocs = $docs | Where-Object {
    $n = if ($_.Contains('name')) { $_['name'].ToString() } else { '' }
    $p = if ($_.Contains('parent') -and -not $_['parent'].IsBsonNull) { $_['parent'].ToString() } else { '' }
    $n -like '*Cardcaptor*' -or $p -like '*Cardcaptor*'
}

Write-Host "Total docs relacionados a Cardcaptor Sakura: $($sakuraDocs.Count)"
foreach ($d in $sakuraDocs) {
    $n = if ($d.Contains('name')) { $d['name'].ToString() } else { '' }
    $t = if ($d.Contains('type')) { $d['type'].ToString() } else { '' }
    $p = if ($d.Contains('parent') -and -not $d['parent'].IsBsonNull) { $d['parent'].ToString() } else { '' }
    $parts = if ($d.Contains('parts')) { $d['parts'].AsBsonArray.Count } else { 0 }
    Write-Host "id=$($d['_id']) type=$t parts=$parts name=$n parent=$p"
}

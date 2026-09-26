Add-Type -Path 'C:\Program Files\MulletaFlix\Server\MongoDB.Bson.dll'
Add-Type -Path 'C:\Program Files\MulletaFlix\Server\MongoDB.Driver.dll'
$client = [MongoDB.Driver.MongoClient]::new('mongodb://localhost:27017')
$db = $client.GetDatabase('ftp')
$col = $db.GetCollection[MongoDB.Bson.BsonDocument]('files')
$docs = $col.Find([MongoDB.Bson.BsonDocument]::new()).ToList()
Write-Host "Total docs: $($docs.Count)"

$seriesDocs = $docs | Where-Object {
    $name = if ($_.Contains('name') -and -not $_.GetElement('name').Value.IsBsonNull) { $_.GetElement('name').Value.AsString } else { '' }
    $p = if ($_.Contains('parent') -and -not $_.GetElement('parent').Value.IsBsonNull) { $_.GetElement('parent').Value.ToString() } else { '' }
    $p -like '*/Series*'
}
Write-Host "Docs under Series: $($seriesDocs.Count)"

$direct = $docs | Where-Object {
    $p = if ($_.Contains('parent') -and -not $_.GetElement('parent').Value.IsBsonNull) { $_.GetElement('parent').Value.ToString() } else { '' }
    $p -eq '/raphael/Series' -or $p -eq 'raphael/Series'
}
Write-Host "Direct children of Series ($($direct.Count)):"
foreach ($d in $direct) {
    $name = $d.GetElement('name').Value.AsString
    $type = if ($d.Contains('type')) { $d.GetElement('type').Value.ToString() } else { '' }
    Write-Host "  [$type] $name"
}

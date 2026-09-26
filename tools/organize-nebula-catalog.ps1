param(
    [string]$ServerDir = 'C:\Program Files\MulletaFlix\Server',
    [string]$ConnectionString = 'mongodb://localhost:27017',
    [string]$Database = 'ftp',
    [string]$Collection = 'files',
    [switch]$DryRun
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

$cursor = $findSync.MakeGenericMethod([MongoDB.Bson.BsonDocument]).Invoke($col, @([MongoDB.Driver.FilterDefinition[MongoDB.Bson.BsonDocument]]::Empty, $null, [System.Threading.CancellationToken]::None))
$documents = [System.Collections.Generic.List[MongoDB.Bson.BsonDocument]]::new()
while ($cursor.MoveNext([System.Threading.CancellationToken]::None)) {
    foreach ($d in $cursor.Current) { $documents.Add($d) }
}

Write-Host "Total de documentos carregados: $($documents.Count)"

$byId = @{}
foreach ($d in $documents) { $byId[$d['_id'].ToString()] = $d }

function Get-ParentKey($doc) {
    if ($doc.Contains('parent') -and -not $doc['parent'].IsBsonNull) {
        return $doc['parent'].ToString()
    }
    return ''
}

function Get-Name($doc) {
    if ($doc.Contains('name') -and -not $doc['name'].IsBsonNull) {
        return $doc['name'].ToString()
    }
    return ''
}

# 1. Backup antes de qualquer alteração
if (-not $DryRun) {
    $backupColName = "files_backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    Write-Host "Criando coleção de backup: $backupColName..."
    $backupArgs = @($backupColName)
    foreach ($parameter in $getCollection.GetParameters() | Select-Object -Skip 1) { $backupArgs += $null }
    $backupCol = $getCollection.MakeGenericMethod([MongoDB.Bson.BsonDocument]).Invoke($db, $backupArgs)
    $backupCol.InsertMany($documents)
    Write-Host "Backup concluído com sucesso ($($documents.Count) documentos)."
}

# Localiza as raízes principais
$seriesRoot = $documents | Where-Object { (Get-Name $_) -eq 'Series' -and ((Get-ParentKey $_) -in @('', '/', '/raphael', 'raphael')) } | Select-Object -First 1
$animacoesRoot = $documents | Where-Object { (Get-Name $_) -eq 'Animações' -and ((Get-ParentKey $_) -in @('', '/', '/raphael', 'raphael')) } | Select-Object -First 1
$novelasRoot = $documents | Where-Object { (Get-Name $_) -eq 'Novelas' -and ((Get-ParentKey $_) -in @('', '/', '/raphael', 'raphael')) } | Select-Object -First 1
$doramasRoot = $documents | Where-Object { (Get-Name $_) -eq 'Doramas' -and ((Get-ParentKey $_) -in @('', '/', '/raphael', 'raphael')) } | Select-Object -First 1

if ($null -eq $seriesRoot) { throw "Raiz Series não encontrada!" }

# Cria Doramas se não existir
if ($null -eq $doramasRoot) {
    Write-Host "Criando pasta raiz 'Doramas'..."
    $doramasId = [MongoDB.Bson.ObjectId]::GenerateNewId()
    $doramasDoc = [MongoDB.Bson.BsonDocument]::new()
    $doramasDoc['_id'] = $doramasId
    $doramasDoc['name'] = 'Doramas'
    $doramasDoc['type'] = 'dir'
    $doramasDoc['is_directory'] = $true
    $doramasDoc['status'] = 'completed'
    $doramasDoc['parent'] = $seriesRoot['parent']
    $doramasDoc['created_at'] = [System.DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $doramasDoc['modified_at'] = [System.DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    
    if (-not $DryRun) {
        $col.InsertOne($doramasDoc)
    }
    $doramasRoot = $doramasDoc
    $byId[$doramasId.ToString()] = $doramasRoot
    $documents.Add($doramasRoot)
}

Write-Host "Series Id: $($seriesRoot['_id'])"
Write-Host "Animações Id: $($animacoesRoot['_id'])"
Write-Host "Novelas Id: $($novelasRoot['_id'])"
Write-Host "Doramas Id: $($doramasRoot['_id'])"

# Arquivos que possuem partes publicadas no Telegram
$filesWithPayload = $documents | Where-Object {
    $t = if ($_.Contains('type')) { $_['type'].ToString() } else { '' }
    $t -ne 'dir' -and $_.Contains('parts') -and $_['parts'].IsBsonArray -and $_['parts'].AsBsonArray.Count -gt 0
}
Write-Host "Arquivos reais com payload: $($filesWithPayload.Count)"

# Mapeia quais pastas contêm arquivos reais (direta ou indiretamente)
$foldersWithPayload = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

function Register-Ancestors($p) {
    if ([string]::IsNullOrWhiteSpace($p)) { return }
    if ($foldersWithPayload.Add($p)) {
        if ($byId.ContainsKey($p)) {
            Register-Ancestors (Get-ParentKey $byId[$p])
        } else {
            $norm = $p.Trim('/').Replace('\', '/')
            $lastSlash = $norm.LastIndexOf('/')
            if ($lastSlash -gt 0) {
                Register-Ancestors $norm.Substring(0, $lastSlash)
                Register-Ancestors "/$($norm.Substring(0, $lastSlash))"
            }
        }
    }
}

foreach ($f in $filesWithPayload) {
    Register-Ancestors (Get-ParentKey $f)
}

# 2. Identifica filhos diretos de Series
$seriesId = $seriesRoot['_id'].ToString()
$seriesChildren = $documents | Where-Object {
    $p = Get-ParentKey $_
    $p -eq $seriesId -or $p -eq '/raphael/Series' -or $p -eq 'raphael/Series'
}

Write-Host "Total de filhos diretos sob Series: $($seriesChildren.Count)"

$ghostFoldersDeleted = 0
$movedToAnimacoes = 0
$movedToDoramas = 0
$movedToNovelas = 0
$movedToSeries = 0

# Títulos que são conhecidamente Animações
$knownAnimacoes = @(
    'Boruto - Naruto Next Generations',
    'RILAKKUMA (2026)',
    'RILAKKUMA'
)

# Títulos que são conhecidamente Doramas
$knownDoramas = @(
    'Encounter (2021) (LEG)',
    'The Guest (2025)',
    'Em Busca de Jade (2026)'
)

# Títulos que são conhecidamente Novelas
$knownNovelas = @(
    'Rebelde (2022)'
)

$BuildersFilter = [MongoDB.Driver.Builders[MongoDB.Bson.BsonDocument]]::Filter
# Função auxiliar para mover uma pasta de título e atualizar todos os seus descendentes
function Move-TitleFolder($folderDoc, $targetRootDoc, [string]$targetRootName) {
    $folderId = $folderDoc['_id']
    $folderName = Get-Name $folderDoc
    Write-Host "  -> Movendo '$folderName' para $targetRootName..."
    
    if (-not $DryRun) {
        # Atualiza parent do folderDoc para o id do targetRoot
        $setDoc = [MongoDB.Bson.BsonDocument]::new()
        $setDoc['parent'] = $targetRootDoc['_id']
        $setDoc['modified_at'] = [System.DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
        $updateFolder = [MongoDB.Bson.BsonDocument]::new('$set', $setDoc)
        $filterFolder = [MongoDB.Bson.BsonDocument]::new('_id', $folderId)
        $null = $col.UpdateOne($filterFolder, $updateFolder)
        
        # Atualiza caminhos virtuais legados dos descendentes
        $oldPrefix1 = "/raphael/Series/$folderName"
        $newPrefix1 = "/raphael/$targetRootName/$folderName"
        $oldPrefix2 = "/raphael/Series/Series/$folderName"
        
        $descendants = $documents | Where-Object {
            $p = Get-ParentKey $_
            $p.StartsWith($oldPrefix1) -or $p.StartsWith($oldPrefix2)
        }
        
        foreach ($desc in $descendants) {
            $currP = Get-ParentKey $desc
            $newP = if ($currP.StartsWith($oldPrefix2)) {
                $currP.Replace($oldPrefix2, $newPrefix1)
            } else {
                $currP.Replace($oldPrefix1, $newPrefix1)
            }
            $descSet = [MongoDB.Bson.BsonDocument]::new('parent', $newP)
            $null = $col.UpdateOne(
                [MongoDB.Bson.BsonDocument]::new('_id', $desc['_id']),
                [MongoDB.Bson.BsonDocument]::new('$set', $descSet)
            )
        }
    }
}

# 3. Processa a pasta duplicada 'Series/Series'
$seriesUnderSeries = $seriesChildren | Where-Object { (Get-Name $_) -eq 'Series' } | Select-Object -First 1
if ($null -ne $seriesUnderSeries) {
    Write-Host "Processando pasta duplicada Series/Series (id=$($seriesUnderSeries['_id']))..."
    $seriesSeriesId = $seriesUnderSeries['_id'].ToString()
    $subChildren = $documents | Where-Object {
        $p = Get-ParentKey $_
        $p -eq $seriesSeriesId -or $p -eq '/raphael/Series/Series' -or $p -eq 'raphael/Series/Series'
    }
    
    foreach ($sub in $subChildren) {
        $subName = Get-Name $sub
        if ($subName -in $knownAnimacoes) {
            Move-TitleFolder $sub $animacoesRoot 'Animações'
            $movedToAnimacoes++
        } elseif ($subName -in $knownDoramas) {
            Move-TitleFolder $sub $doramasRoot 'Doramas'
            $movedToDoramas++
        } elseif ($subName -in $knownNovelas) {
            Move-TitleFolder $sub $novelasRoot 'Novelas'
            $movedToNovelas++
        } else {
            Move-TitleFolder $sub $seriesRoot 'Series'
            $movedToSeries++
        }
    }
    
    # Exclui a pasta duplicada Series/Series
    Write-Host "Excluindo nó duplicado Series/Series..."
    if (-not $DryRun) {
        $null = $col.DeleteOne([MongoDB.Bson.BsonDocument]::new('_id', $seriesUnderSeries['_id']))
    }
}

# 4. Processa a pasta duplicada 'Series/Novelas'
$novelasUnderSeries = $seriesChildren | Where-Object { (Get-Name $_) -eq 'Novelas' } | Select-Object -First 1
if ($null -ne $novelasUnderSeries) {
    Write-Host "Processando pasta duplicada Series/Novelas (id=$($novelasUnderSeries['_id']))..."
    $novelasSeriesId = $novelasUnderSeries['_id'].ToString()
    $subNovelas = $documents | Where-Object {
        $p = Get-ParentKey $_
        $p -eq $novelasSeriesId -or $p -eq '/raphael/Series/Novelas' -or $p -eq 'raphael/Series/Novelas'
    }
    foreach ($sn in $subNovelas) {
        Move-TitleFolder $sn $novelasRoot 'Novelas'
        $movedToNovelas++
    }
    Write-Host "Excluindo nó duplicado Series/Novelas..."
    if (-not $DryRun) {
        $null = $col.DeleteOne([MongoDB.Bson.BsonDocument]::new('_id', $novelasUnderSeries['_id']))
    }
}

# 5. Processa os demais filhos diretos de Series
foreach ($child in $seriesChildren) {
    $childId = $child['_id'].ToString()
    $childName = Get-Name $child
    
    if ($childName -eq 'Series' -or $childName -eq 'Novelas') { continue }
    
    # Verifica se a pasta tem arquivos reais
    $childVirtual = "/raphael/Series/$childName"
    $hasFiles = $foldersWithPayload.Contains($childId) -or $foldersWithPayload.Contains($childVirtual) -or $foldersWithPayload.Contains("raphael/Series/$childName")
    
    if (-not $hasFiles) {
        # PASTA FANTASMA VAZIA!
        Write-Host "Pasta fantasma vazia em Series: '$childName' - Deletando nós órfãos..."
        if (-not $DryRun) {
            # Deleta subdiretórios órfãos (ex: Season 01, Season 02)
            $orArray = [MongoDB.Bson.BsonArray]::new()
            $null = $orArray.Add([MongoDB.Bson.BsonDocument]::new('parent', $child['_id']))
            $null = $orArray.Add([MongoDB.Bson.BsonDocument]::new('parent', $childVirtual))
            $null = $orArray.Add([MongoDB.Bson.BsonDocument]::new('parent', [MongoDB.Bson.BsonRegularExpression]::new('^' + [System.Text.RegularExpressions.Regex]::Escape($childVirtual))))
            $delManyFilter = [MongoDB.Bson.BsonDocument]::new('$or', $orArray)
            $null = $col.DeleteMany($delManyFilter)
            
            # Deleta o nó do título
            $null = $col.DeleteOne([MongoDB.Bson.BsonDocument]::new('_id', $child['_id']))
        }
        $ghostFoldersDeleted++
        continue
    }
    
    # Classificação de pastas com conteúdo real
    if ($childName -in $knownAnimacoes) {
        Move-TitleFolder $child $animacoesRoot 'Animações'
        $movedToAnimacoes++
    } elseif ($childName -in $knownDoramas) {
        Move-TitleFolder $child $doramasRoot 'Doramas'
        $movedToDoramas++
    } elseif ($childName -in $knownNovelas) {
        Move-TitleFolder $child $novelasRoot 'Novelas'
        $movedToNovelas++
    }
}

Write-Host "=========================================="
Write-Host "Varredura e Reorganização Concluídas!"
Write-Host "Pastas fantasmas vazias removidas: $ghostFoldersDeleted"
Write-Host "Títulos movidos para Animações: $movedToAnimacoes"
Write-Host "Títulos movidos para Doramas: $movedToDoramas"
Write-Host "Títulos movidos para Novelas: $movedToNovelas"
Write-Host "Títulos promovidos para Series: $movedToSeries"
Write-Host "=========================================="

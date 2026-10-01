[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://127.0.0.1:8096',
    [ValidateRange(1, 100)]
    [int]$Samples = 10,
    [string[]]$Paths = @('/health', '/ready', '/System/Info/Public'),
    [ValidateRange(1, 60)]
    [int]$TimeoutSeconds = 5,
    [int]$ServerProcessId = 0,
    [string]$DataPath
)

$ErrorActionPreference = 'Stop'

$baseUri = $null
if (-not [Uri]::TryCreate($BaseUrl, [UriKind]::Absolute, [ref]$baseUri) -or
    $baseUri.Scheme -notin @('http', 'https')) {
    throw 'BaseUrl must be an absolute HTTP or HTTPS URL.'
}

if ($Paths.Count -eq 0) {
    throw 'At least one endpoint path is required.'
}

foreach ($path in $Paths) {
    if (-not $path.StartsWith('/', [StringComparison]::Ordinal) -or
        $path.Contains('?') -or $path.Contains('#') -or $path.StartsWith('//', [StringComparison]::Ordinal)) {
        throw "Only root-relative GET paths without query or fragment are supported: $path"
    }
}

if ($ServerProcessId -eq 0) {
    $serverProcesses = @(Get-Process -Name 'MulletaFlix' -ErrorAction SilentlyContinue)
    if ($serverProcesses.Count -eq 1) {
        $ServerProcessId = $serverProcesses[0].Id
    }
}

function Get-ProcessSnapshot([int]$pidToMeasure) {
    if ($pidToMeasure -le 0) {
        return $null
    }

    $process = Get-Process -Id $pidToMeasure -ErrorAction SilentlyContinue
    if ($null -eq $process) {
        return $null
    }

    return [ordered]@{
        pid = $process.Id
        executable = $process.Path
        version = if ($process.Path) { (Get-Item -LiteralPath $process.Path).VersionInfo.ProductVersion } else { $null }
        startUtc = $process.StartTime.ToUniversalTime().ToString('o')
        workingSetBytes = [long]$process.WorkingSet64
        privateBytes = [long]$process.PrivateMemorySize64
        cpuSeconds = [math]::Round($process.TotalProcessorTime.TotalSeconds, 3)
    }
}

function Get-VolumeSnapshot([string]$pathToMeasure) {
    if ([string]::IsNullOrWhiteSpace($pathToMeasure) -or -not (Test-Path -LiteralPath $pathToMeasure)) {
        return $null
    }

    $root = [IO.Path]::GetPathRoot([IO.Path]::GetFullPath($pathToMeasure))
    if ([string]::IsNullOrWhiteSpace($root)) {
        return $null
    }

    try {
        $drive = [IO.DriveInfo]::new($root)
        return [ordered]@{
            root = $root
            totalBytes = [long]$drive.TotalSize
            freeBytes = [long]$drive.AvailableFreeSpace
        }
    }
    catch {
        return $null
    }
}

function Get-NearestRankPercentile([double[]]$values, [double]$percentile) {
    if ($values.Count -eq 0) {
        return $null
    }

    $sorted = @($values | Sort-Object)
    $index = [math]::Max(0, [math]::Ceiling($sorted.Count * $percentile) - 1)
    return [math]::Round([double]$sorted[$index], 3)
}

$before = Get-ProcessSnapshot $ServerProcessId
$applicationVolume = if ($before) { Get-VolumeSnapshot $before.executable } else { $null }
$dataVolume = Get-VolumeSnapshot $DataPath
$client = [Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds($TimeoutSeconds)
$endpointResults = @()
$failedRequests = 0

try {
    foreach ($path in $Paths) {
        $durations = [Collections.Generic.List[double]]::new()
        $statuses = [Collections.Generic.Dictionary[string, int]]::new([StringComparer]::Ordinal)
        for ($sample = 0; $sample -lt $Samples; $sample++) {
            $target = [Uri]::new($baseUri, $path)
            $watch = [Diagnostics.Stopwatch]::StartNew()
            try {
                $response = $client.GetAsync($target).GetAwaiter().GetResult()
                try {
                    [void]$response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
                    $watch.Stop()
                    $durations.Add($watch.Elapsed.TotalMilliseconds)
                    $status = [string][int]$response.StatusCode
                    if (-not $statuses.ContainsKey($status)) { $statuses[$status] = 0 }
                    $statuses[$status]++
                    if (-not $response.IsSuccessStatusCode) { $failedRequests++ }
                }
                finally {
                    $response.Dispose()
                }
            }
            catch {
                $watch.Stop()
                if (-not $statuses.ContainsKey('transport-error')) { $statuses['transport-error'] = 0 }
                $statuses['transport-error']++
                $failedRequests++
            }
        }

        $endpointResults += [ordered]@{
            path = $path
            samples = $Samples
            statuses = $statuses
            p50Milliseconds = Get-NearestRankPercentile $durations.ToArray() 0.50
            p95Milliseconds = Get-NearestRankPercentile $durations.ToArray() 0.95
            maxMilliseconds = if ($durations.Count -gt 0) { [math]::Round(($durations | Measure-Object -Maximum).Maximum, 3) } else { $null }
        }
    }
}
finally {
    $client.Dispose()
}

$after = Get-ProcessSnapshot $ServerProcessId
$result = [ordered]@{
    measuredAtUtc = [DateTime]::UtcNow.ToString('o')
    machine = [Environment]::MachineName
    operatingSystem = [Runtime.InteropServices.RuntimeInformation]::OSDescription
    baseUrl = $baseUri.GetLeftPart([UriPartial]::Authority)
    processBefore = $before
    processAfter = $after
    applicationVolume = $applicationVolume
    dataVolume = $dataVolume
    endpoints = $endpointResults
    failedRequests = $failedRequests
    method = 'Sequential GET; client wall-clock latency includes HTTP body; p50/p95 use nearest rank; no authentication or state-changing calls.'
}

$result | ConvertTo-Json -Depth 8
if ($failedRequests -gt 0) {
    exit 1
}

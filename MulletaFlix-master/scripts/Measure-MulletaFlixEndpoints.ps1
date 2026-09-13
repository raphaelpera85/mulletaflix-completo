[CmdletBinding()]
param(
    [string] $BaseUrl = 'http://127.0.0.1:8096',
    [string] $UserId,
    [string] $ItemId,
    [string] $AccessToken = $env:MFLX_ACCESS_TOKEN,
    [ValidateRange(1, 100)]
    [int] $Iterations = 5,
    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'

function Join-EndpointUrl {
    param([string] $Path)

    return '{0}/{1}' -f $BaseUrl.TrimEnd('/'), $Path.TrimStart('/')
}

function Invoke-MeasuredRequest {
    param(
        [Parameter(Mandatory = $true)][string] $Name,
        [Parameter(Mandatory = $true)][string] $Uri
    )

    $samples = [System.Collections.Generic.List[object]]::new()
    $headers = @{}
    if ($AccessToken) {
        $headers.Authorization = "Bearer $AccessToken"
    }

    for ($attempt = 1; $attempt -le $Iterations; $attempt++) {
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $statusCode = $null
        $errorMessage = $null
        try {
            $response = Invoke-WebRequest -Uri $Uri -Headers $headers -Method Get -TimeoutSec 30 -UseBasicParsing
            $statusCode = [int] $response.StatusCode
        } catch {
            if ($_.Exception.Response) {
                $statusCode = [int] $_.Exception.Response.StatusCode
            }
            $errorMessage = $_.Exception.Message
        } finally {
            $stopwatch.Stop()
        }

        $samples.Add([pscustomobject]@{
            Attempt = $attempt
            DurationMs = [math]::Round($stopwatch.Elapsed.TotalMilliseconds, 2)
            StatusCode = $statusCode
            Success = $null -ne $statusCode -and $statusCode -ge 200 -and $statusCode -lt 300
            Error = $errorMessage
        })
    }

    $successful = @($samples | Where-Object Success)
    $durations = @($successful | ForEach-Object DurationMs)
    $summary = [pscustomobject]@{
        Name = $Name
        Uri = $Uri
        Iterations = $Iterations
        Successes = $successful.Count
        Failures = $Iterations - $successful.Count
        MinMs = if ($durations.Count) { [math]::Round(($durations | Measure-Object -Minimum).Minimum, 2) } else { $null }
        AverageMs = if ($durations.Count) { [math]::Round(($durations | Measure-Object -Average).Average, 2) } else { $null }
        MaxMs = if ($durations.Count) { [math]::Round(($durations | Measure-Object -Maximum).Maximum, 2) } else { $null }
        Samples = $samples
    }

    return $summary
}

$requests = [System.Collections.Generic.List[object]]::new()
$requests.Add([pscustomobject]@{ Name = 'health'; Uri = Join-EndpointUrl '/health' })
if ($UserId) {
    $requests.Add([pscustomobject]@{
        Name = 'latest-media'
        Uri = Join-EndpointUrl "/Items/Latest?UserId=$([uri]::EscapeDataString($UserId))&Limit=24&Fields=PrimaryImageAspectRatio,Overview"
    })
}
if ($UserId -and $ItemId) {
    $requests.Add([pscustomobject]@{
        Name = 'item-detail'
        Uri = Join-EndpointUrl "/Users/$([uri]::EscapeDataString($UserId))/Items/$([uri]::EscapeDataString($ItemId))?Fields=PrimaryImageAspectRatio,Overview"
    })
}

$results = @($requests | ForEach-Object {
    Invoke-MeasuredRequest -Name $_.Name -Uri $_.Uri
})

$results | Select-Object Name, Iterations, Successes, Failures, MinMs, AverageMs, MaxMs | Format-Table -AutoSize

if ($OutputPath) {
    $results | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath -Encoding utf8
    Write-Host "Benchmark salvo em $OutputPath"
}

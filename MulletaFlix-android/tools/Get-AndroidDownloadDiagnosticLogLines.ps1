function Get-AndroidDownloadDiagnosticLogLines {
    [CmdletBinding()]
    param(
        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)]
        [string[]] $LogLines
    )

    $pattern = @(
        'DownloadManager'
        'DownloadService'
        'DownloadTask'
        'ExoPlayerImplInternal'
        'CacheDataSource'
        'CacheWriter'
        'MediaCodec'
        'AndroidRuntime'
        'FATAL EXCEPTION'
        'Fatal signal'
        'SIGSEGV'
        'SIGABRT'
        'am_crash'
        'am_anr'
        'AndroidJUnitRunner'
        'Instrumentation'
        'org\.mulletaflix\.android\.debug\.test'
        'has died'
        'Background start not allowed'
    ) -join '|'
    return @($LogLines | Where-Object { $_ -match $pattern } | ForEach-Object {
        $line = $_ -replace '(?i)(authorization\s*[:=]\s*)(?:bearer\s+)?[^\s,;]+', '$1[REDACTED]'
        $line = $line -replace '(?i)(\b(?:access[_-]?token|token|password|api[_-]?key)\b\s*[:=]\s*)[^\s,;&]+', '$1[REDACTED]'
        $line
    })
}

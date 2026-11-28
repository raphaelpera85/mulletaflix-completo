$ErrorActionPreference = 'Stop'
$configPath = Join-Path $PSScriptRoot '..\configure-https.ps1'
$tokens = $null
$parseErrors = $null
$configAst = [System.Management.Automation.Language.Parser]::ParseFile($configPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) { throw "HTTPS configurator has PowerShell parse errors: $($parseErrors[0].Message)" }
$definitions = $configAst.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true)
foreach ($name in @('ConvertTo-PowerShellSingleQuotedLiteral', 'Write-ReloadScript')) {
    $definition = $definitions | Where-Object Name -eq $name | Select-Object -First 1
    if ($null -eq $definition) { throw "$name function was not found." }
    Invoke-Expression $definition.Extent.Text
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("mflx-reload-quote-" + [guid]::NewGuid().ToString('N') + "'")
[void][IO.Directory]::CreateDirectory($tempRoot)
try {
    $script:nginxExe = Join-Path $tempRoot "nginx'; throw 'injected.exe"
    $script:nginxRoot = Join-Path $tempRoot "nginx'; throw 'injected"
    $script:reloadScript = Join-Path $tempRoot 'reload-nginx.ps1'

    Write-ReloadScript

    $generated = [IO.File]::ReadAllText($script:reloadScript)
    $generatedTokens = $null
    $generatedErrors = $null
    $generatedAst = [System.Management.Automation.Language.Parser]::ParseInput($generated, [ref]$generatedTokens, [ref]$generatedErrors)
    if ($generatedErrors.Count -gt 0) {
        throw "Generated renewal script is not valid PowerShell for a path containing PowerShell metacharacters: $($generatedErrors[0].Message)"
    }

    $escapedExe = $script:nginxExe.Replace("'", "''")
    $escapedRoot = $script:nginxRoot.Replace("'", "''")
    $expectedCommand = "& '$escapedExe' -p '$escapedRoot' -c conf/nginx.conf -s reload"
    if (-not $generated.Contains($expectedCommand)) { throw 'Generated renewal script did not preserve and escape the executable/config paths as PowerShell literals.' }
    if ($generatedAst.EndBlock.Statements.Count -ne 3) { throw 'Generated renewal script introduced extra executable statements.' }

    Write-Output 'Renewal script paths with PowerShell metacharacters remain literal and executable statements are unchanged.'
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force
}

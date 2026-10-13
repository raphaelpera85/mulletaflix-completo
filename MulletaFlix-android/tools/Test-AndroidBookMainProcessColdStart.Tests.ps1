Describe 'Main process book cold-start host contract' {
    It 'requires item metadata and book content but not best-effort conversion status' {
        $scriptPath = Join-Path $PSScriptRoot 'Test-AndroidBookMainProcessColdStart.ps1'
        $scriptText = Get-Content -LiteralPath $scriptPath -Raw
        $routeBlock = [regex]::Match(
            $scriptText,
            '(?s)foreach\s+\(\$requiredPath\s+in\s+@\((?<routes>.*?)\)\)'
        )

        $routeBlock.Success | Should Be $true
        $requiredRoutes = [regex]::Matches($routeBlock.Groups['routes'].Value, '"(?<route>[^"]+)"') |
            ForEach-Object { $_.Groups['route'].Value }

        ($requiredRoutes -contains '/Users/book-cold-start-user/Items/$itemId') | Should Be $true
        ($requiredRoutes -contains '/BookReader/Items/$itemId/BookReader/Epub') | Should Be $true
        ($requiredRoutes -notcontains '/BookReader/Items/$itemId/BookReader/Status') | Should Be $true
    }
}

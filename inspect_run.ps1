$run = Invoke-RestMethod -Uri 'https://api.github.com/repos/raphaelpera85/mulletaflix-completo/actions/runs/35101854537'
Write-Host "Name: $($run.name)"
Write-Host "Conclusion: $($run.conclusion)"
Write-Host "Jobs URL: $($run.jobs_url)"
$jobsResp = Invoke-RestMethod -Uri $run.jobs_url
$jobsResp | ConvertTo-Json -Depth 4

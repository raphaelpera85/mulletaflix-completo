$resp = Invoke-RestMethod -Uri 'https://api.github.com/repos/raphaelpera85/mulletaflix-completo/actions/runs/35101797158'
Write-Host "Name: $($resp.name)"
Write-Host "Conclusion: $($resp.conclusion)"
$jobs = Invoke-RestMethod -Uri $resp.jobs_url
$jobs.jobs | Select-Object id, name, status, conclusion, runner_name

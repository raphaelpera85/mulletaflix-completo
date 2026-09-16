$resp = Invoke-RestMethod -Uri 'https://api.github.com/repos/raphaelpera85/mulletaflix-completo/actions/runs?per_page=5'
$resp.workflow_runs | ForEach-Object {
    [PSCustomObject]@{
        Id = $_.id
        Name = $_.name
        Branch = $_.head_branch
        Event = $_.event
        Status = $_.status
        Conclusion = $_.conclusion
        Url = $_.html_url
    }
} | Format-Table -AutoSize

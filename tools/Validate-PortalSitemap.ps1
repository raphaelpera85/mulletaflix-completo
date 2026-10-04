[CmdletBinding()]
param(
    [string]$PortalRoot,
    [string]$SitemapPath
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($PortalRoot)) { $PortalRoot = Join-Path $scriptRoot '..\portal-site' }
if (-not $SitemapPath) { $SitemapPath = Join-Path $PortalRoot 'sitemap.xml' }

if (-not (Test-Path -LiteralPath $PortalRoot -PathType Container)) { throw "Portal root not found: $PortalRoot" }
if (-not (Test-Path -LiteralPath $SitemapPath -PathType Leaf)) { throw "Sitemap not found: $SitemapPath" }

[xml]$sitemap = Get-Content -Raw -LiteralPath $SitemapPath
$namespace = New-Object System.Xml.XmlNamespaceManager($sitemap.NameTable)
$namespace.AddNamespace('sm', 'http://www.sitemaps.org/schemas/sitemap/0.9')
$listed = @($sitemap.SelectNodes('//sm:url/sm:loc', $namespace) | ForEach-Object { $_.InnerText.TrimEnd('/') })
$baseUrl = 'https://mulletaflix-portal.vercel.app'
$excluded = @('googlecc0a01337fc9c092.html')
$missing = @()

Get-ChildItem -LiteralPath $PortalRoot -Filter '*.html' -File | ForEach-Object {
    if ($excluded -contains $_.Name) { return }
    $html = Get-Content -Raw -LiteralPath $_.FullName
    $canonical = [regex]::Match($html, '<link\s+rel="canonical"\s+href="([^"]+)"', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase).Groups[1].Value
    if ([string]::IsNullOrWhiteSpace($canonical)) { throw "Public page has no canonical URL: $($_.Name)" }
    $normalized = $canonical.TrimEnd('/')
    if ($normalized -eq $baseUrl) { $normalized = $baseUrl }
    if ($listed -notcontains $normalized) { $missing += "$($_.Name) -> $canonical" }
}

if ($missing.Count -gt 0) { throw "Pages missing from sitemap:`n$($missing -join "`n")" }
Write-Output "Portal sitemap covers $($listed.Count) canonical pages."

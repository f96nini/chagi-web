# Export finished Jirugi match summaries for both data sync and offline app assets.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SnapshotDirectory,
    [Parameter(Mandatory)][string]$ResultFile,
    [Parameter(Mandatory)][string]$DataDirectory,
    [string]$BundleDirectory
)
$ErrorActionPreference = 'Stop'
$header = Get-Content -LiteralPath $ResultFile -TotalCount 2
$name = $header[0].Trim()
$date = [datetime]::ParseExact($header[1].Substring(0, 10), 'yyyy-MM-dd', [cultureinfo]::InvariantCulture).ToString('yyyy-MM-dd')
$matches = @(Get-Content -LiteralPath (Join-Path $SnapshotDirectory 'matches.json') -Raw | ConvertFrom-Json)
if (!$matches.Count -or @($matches | Where-Object finished -ne 1).Count) { throw 'A finished match snapshot is required.' }
if (@($matches | Group-Object matchId | Where-Object Count -gt 1).Count) { throw 'Duplicate match IDs in snapshot.' }
$fields = @('categoryId','category','discipline','ring','matchId','matchNumber','finished','redId','redParticipant','redPoints','redFlags','blueId','blueParticipant','bluePoints','blueFlags','extraMatchId','startAt','group','winnerId','winner')
$json = ConvertTo-Json -InputObject @($matches | Select-Object $fields) -Depth 6 -Compress
$file = [IO.Path]::GetFileNameWithoutExtension($ResultFile) + '.json'
$directory = Join-Path $DataDirectory 'matches'
[void][IO.Directory]::CreateDirectory($directory)
$indexPath = Join-Path $directory 'index.json'
$index = if (Test-Path -LiteralPath $indexPath) { @(Get-Content -LiteralPath $indexPath -Raw | ConvertFrom-Json) } else { @() }
$index = @($index | Where-Object { $_.competitionDate -ne $date -or $_.competitionName -ne $name }) + @([ordered]@{ competitionDate = $date; competitionName = $name; file = $file })
$utf8 = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $directory $file), $json + "`n", $utf8)
[IO.File]::WriteAllText($indexPath, (ConvertTo-Json -InputObject @($index | Sort-Object competitionDate,competitionName) -Depth 5) + "`n", $utf8)
$manifestPath = Join-Path $DataDirectory 'manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$changed = $false
foreach ($relative in @("matches/$file", 'matches/index.json')) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $DataDirectory $relative) -Algorithm SHA256).Hash.ToLowerInvariant()
    $entry = $manifest.files | Where-Object path -eq $relative
    if (!$entry) {
        $manifest.files += [pscustomobject]@{ path = $relative; sha256 = $hash }
        $changed = $true
    } elseif ($entry.sha256 -ne $hash) { $entry.sha256 = $hash; $changed = $true }
}
if ($changed) {
    $manifest.version = [datetime]::UtcNow.ToString('yyyy-MM-dd')
    $manifest.updated = [datetime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 6) + "`n", $utf8)
}
if ($BundleDirectory) {
    [void][IO.Directory]::CreateDirectory($BundleDirectory)
    Copy-Item -LiteralPath $indexPath -Destination (Join-Path $BundleDirectory 'index.json')
    foreach ($entry in $index) {
        Copy-Item -LiteralPath (Join-Path $directory $entry.file) -Destination (Join-Path $BundleDirectory $entry.file)
    }
}
Write-Output "Exported $($matches.Count) match entries for $name ($date)."

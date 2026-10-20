<#
.SYNOPSIS
Adds every category participant from a finished Jirugi snapshot to an app result file.
.DESCRIPTION
The existing result file supplies the competition name, date and venue. Medal rows
must agree with placements.json. Other participants receive 0 (participation, not
a finishing position). Running this again produces the same result without duplicates.
Pass the data manifest to refresh the result file's SHA-256 for app synchronization.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SnapshotDirectory,
    [Parameter(Mandatory)][string]$ResultFile,
    [string]$ManifestPath
)
$ErrorActionPreference = 'Stop'
$participants = @(Get-Content -LiteralPath (Join-Path $SnapshotDirectory 'participants.json') -Raw | ConvertFrom-Json)
$placements = @(Get-Content -LiteralPath (Join-Path $SnapshotDirectory 'placements.json') -Raw | ConvertFrom-Json)
$categories = @(Get-Content -LiteralPath (Join-Path $SnapshotDirectory 'categories.json') -Raw | ConvertFrom-Json)
if (!$participants.Count -or !$categories.Count -or @($categories | Where-Object is_finished -ne 1).Count) {
    throw 'A complete, finished snapshot is required.'
}
# Finals can repeat a contestant ID within the same category in the source.
$participants = @($participants | Group-Object categoryId, id | ForEach-Object {
    if (@($_.Group.participant | Select-Object -Unique).Count -ne 1) { throw 'Conflicting names for a contestant ID.' }
    $_.Group[0]
})
$original = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $ResultFile)).Replace("`r`n", "`n")
$header = @($original.Split("`n") | Select-Object -First 3)
if ($header.Count -ne 3) { throw 'Missing competition header.' }
$rankByPlace = @{ first = 1; second = 2; third1 = 3; third2 = 3 }
$expectedMedals = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$output = [Collections.Generic.List[string]]::new()
$output.AddRange([string[]]$header)
$output.AddRange([string[]]@('', 'Resultat', '', 'Kategorier', ''))
$entryCount = 0
foreach ($category in ($categories | Sort-Object { $original.IndexOf("`n$($_.name)`n", [StringComparison]::Ordinal) })) {
    if ($original.IndexOf("`n$($category.name)`n", [StringComparison]::Ordinal) -lt 0) {
        throw "Category missing in the result file: $($category.name)"
    }
    $categoryParticipants = @($participants | Where-Object categoryId -eq $category.id)
    # The schedule's num_contestants can be stale after category changes.
    if ($categoryParticipants.Count -ne @($category.contestants.id | Select-Object -Unique).Count) { throw "Participant count mismatch: $($category.name)" }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $ranks = @{}
    foreach ($medal in ($placements | Where-Object categoryId -eq $category.id)) {
        if ($medal.finished -ne 1 -or !$rankByPlace.ContainsKey($medal.place) -or $ranks.ContainsKey($medal.participant)) {
            throw "Invalid placement in $($category.name)"
        }
        $ranks[$medal.participant] = $rankByPlace[$medal.place]
        [void]$expectedMedals.Add("$($category.name)|$($rankByPlace[$medal.place]). $($medal.participant)")
    }
    $rows = foreach ($participant in $categoryParticipants) {
        if (!$seen.Add($participant.participant) -or $participant.participant -notmatch '^.+ - .+$') {
            throw "Invalid or duplicate participant in $($category.name)"
        }
        $rank = if ($ranks.ContainsKey($participant.participant)) { $ranks[$participant.participant] } else { 0 }
        [pscustomobject]@{ Rank = $rank; Name = $participant.participant }
    }
    foreach ($name in $ranks.Keys) {
        if (!$seen.Contains($name)) { throw "Medalist missing from participants: $name" }
    }
    $output.Add($category.name)
    foreach ($row in ($rows | Sort-Object @{ Expression = { if ($_.Rank -gt 0) { $_.Rank } else { [int]::MaxValue } } }, Name)) {
        $output.Add("$($row.Rank). $($row.Name)")
        $entryCount++
    }
    $output.Add('')
}
if ($entryCount -ne $participants.Count) { throw 'Some participants have an unknown category.' }
$existingMedals = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$division = ''
foreach ($line in $original.Split("`n")) {
    if ($line -match '^[1-3]\. .+ - .+$') { [void]$existingMedals.Add("$division|$line") }
    elseif ($line -match '^\D' -and $line.Trim()) { $division = $line.Trim() }
}
if (!$expectedMedals.SetEquals($existingMedals)) { throw 'Snapshot medals differ from the result file. Review before importing.' }
$manifest = $null
if ($ManifestPath) {
    $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    $relativePath = [IO.Path]::GetRelativePath((Split-Path (Resolve-Path -LiteralPath $ManifestPath)), (Resolve-Path -LiteralPath $ResultFile)).Replace('\', '/')
    $manifestEntry = @($manifest.files | Where-Object path -eq $relativePath)
    if ($manifestEntry.Count -ne 1) { throw 'Result file must have exactly one manifest entry.' }
}
$text = ($output -join "`n") + "`n"
[IO.File]::WriteAllText((Resolve-Path -LiteralPath $ResultFile), $text, [Text.UTF8Encoding]::new($false))
if ($manifest) {
    $hash = (Get-FileHash -LiteralPath $ResultFile -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($manifestEntry[0].sha256 -ne $hash) {
        $manifestEntry[0].sha256 = $hash
        $manifest.updated = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
        $manifest.version = [DateTime]::UtcNow.ToString('yyyy-MM-dd')
        [IO.File]::WriteAllText((Resolve-Path -LiteralPath $ManifestPath), ($manifest | ConvertTo-Json -Depth 10) + "`n", [Text.UTF8Encoding]::new($false))
    }
}
Write-Output "Imported $entryCount category entries; $($expectedMedals.Count) medal entries preserved."

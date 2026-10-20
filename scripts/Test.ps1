$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $taskRoot
try {
    $projects = @(
        'Chagi.Tests/Chagi.Tests.csproj',
        'Chagi.Web.Checks/Chagi.Web.Checks.csproj',
        'Chagi.App/checks/MedalLeaderboardChecks.csproj',
        'Chagi.App/checks/ClubDetailsChecks.csproj',
        'Chagi.App/checks/BeltClassificationChecks.csproj',
        'Chagi.App/checks/HeadToHeadChecks.csproj',
        'Chagi.App/checks/DataSyncChecks.csproj',
        'Chagi.App/checks/NavigationChecks.csproj',
        'Chagi.App/checks/BackgroundWorkChecks.csproj'
    )
    foreach ($project in $projects) {
        dotnet run --project $project -p:NuGetAudit=false -p:UseSharedCompilation=false -p:BuildInParallel=false
        if ($LASTEXITCODE -ne 0) { throw "Checks failed: $project" }
    }
    node scripts/pages-checks.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Pages routing checks failed.' }
} finally { Pop-Location }

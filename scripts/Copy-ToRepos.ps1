param([string]$Destination = 'D:\work\repos\chagi-web')
$ErrorActionPreference = 'Stop'
$taskSource = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$taskDestination = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $taskDestination) { throw "Destination already exists: $taskDestination. Choose a new directory to avoid overwriting files." }
if ($taskDestination.StartsWith($taskSource + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Destination cannot be inside the source project.' }
# Run this outside the restricted Codex session, or open the destination as workspace.
# Copy source only; credentials, Git internals, generated outputs and local QA stay behind.
$taskExcluded = @('.git','.vs','.work','.dotnet','artifacts','bin','obj','node_modules','play-signing')
function Copy-SourceDirectory([string]$From, [string]$To) {
    New-Item -ItemType Directory -Path $To -Force | Out-Null
    foreach ($item in Get-ChildItem -LiteralPath $From -Force) {
        if ($item.PSIsContainer) {
            if ($item.Name -in $taskExcluded -or $item.Name -like '.*-checks') { continue }
            Copy-SourceDirectory $item.FullName (Join-Path $To $item.Name)
        } elseif ($item.Name -notmatch '\.(log|binlog|user|keystore|jks|pem|der|pfx)$') {
            Copy-Item -LiteralPath $item.FullName -Destination (Join-Path $To $item.Name)
        }
    }
}
Copy-SourceDirectory $taskSource $taskDestination
Write-Output "Project copied to $taskDestination"

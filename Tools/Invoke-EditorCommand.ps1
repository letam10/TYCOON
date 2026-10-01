param(
    [Parameter(Mandatory = $true)][string]$Command,
    [string]$Method,
    [string]$ArtifactPath,
    [string[]]$AssemblyNames,
    [ValidateRange(1,55)][int]$WaitSeconds = 45
)
$ErrorActionPreference = 'Stop'
$tycoonRoot = Split-Path $PSScriptRoot -Parent
$tycoonWork = Join-Path $tycoonRoot 'work'
$tycoonRequestPath = Join-Path $tycoonWork 'editor-command.json'
$tycoonResponsePath = Join-Path $tycoonWork 'editor-response.json'
$tycoonEditors = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Contains($tycoonRoot) -and $_.CommandLine -notmatch 'assetImportWorker'
})
if ($tycoonEditors.Count -eq 0) { throw 'No running Unity editor for this project.' }
if (Test-Path -LiteralPath $tycoonResponsePath) {
    $tycoonPrevious = Get-Content -LiteralPath $tycoonResponsePath -Raw | ConvertFrom-Json
    if ($tycoonPrevious.state -in @('running','reloading') -and $Command -ne 'status') {
        throw "Editor command $($tycoonPrevious.id) is still active. Inspect that command before submitting another."
    }
}
$tycoonRequest = @{ id = "$Command-$([Guid]::NewGuid().ToString('N'))"; command = $Command }
if ($Method) { $tycoonRequest.method = $Method }
if ($ArtifactPath) { $tycoonRequest.path = $ArtifactPath }
if ($AssemblyNames) { $tycoonRequest.assemblyNames = @($AssemblyNames) }
$tycoonPendingPath = Join-Path $tycoonWork 'editor-command.pending'
[IO.File]::WriteAllText($tycoonPendingPath, ($tycoonRequest | ConvertTo-Json -Depth 5))
Move-Item -LiteralPath $tycoonPendingPath -Destination $tycoonRequestPath -Force
$tycoonDeadline = [DateTime]::UtcNow.AddSeconds($WaitSeconds)
do {
    Start-Sleep -Milliseconds 500
    if (Test-Path -LiteralPath $tycoonResponsePath) {
        $tycoonResponse = Get-Content -LiteralPath $tycoonResponsePath -Raw | ConvertFrom-Json
        if ($tycoonResponse.id -eq $tycoonRequest.id -and $tycoonResponse.state -in @('complete','failed')) {
            $tycoonResponse | ConvertTo-Json -Depth 12
            if (-not $tycoonResponse.success) { exit 1 }
            exit 0
        }
    }
} while ([DateTime]::UtcNow -lt $tycoonDeadline)
[PSCustomObject]@{ id=$tycoonRequest.id; state='pending'; message='Observation timed out; do not restart this command. Read its matching response or inspect the same live editor.' } | ConvertTo-Json
exit 2

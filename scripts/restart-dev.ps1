param(
    [int]$Port = 5187
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "WebVN.Editor\WebVN.Editor.csproj"
$dotnetHome = Join-Path $root ".dotnet"

$listeners = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
if ($listeners) {
    $processIds = $listeners | Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($processId in $processIds) {
        try {
            Stop-Process -Id $processId -Force -ErrorAction Stop
        }
        catch {
            Write-Warning ("Could not stop process {0} on port {1}: {2}" -f $processId, $Port, $_.Exception.Message)
        }
    }

    Start-Sleep -Seconds 1
}

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_HOME = $dotnetHome

Push-Location $root
try {
    dotnet build $project
    $process = Start-Process dotnet -ArgumentList @(
        "run",
        "--project", $project,
        "--urls", "http://localhost:$Port"
    ) -WorkingDirectory $root -PassThru

    Start-Sleep -Seconds 3
    Write-Host "Started dev server on http://localhost:$Port (PID $($process.Id))"
}
finally {
    Pop-Location
}

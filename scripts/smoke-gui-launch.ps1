param(
    [string] $BuildOutputPath = (Join-Path $PSScriptRoot '..\BDInfo\bin\Release'),
    [string] $ReportPath = (Join-Path $PSScriptRoot '..\tests\fixtures\report-roundtrip\sample-snapshot-v2.bdinfo'),
    [int] $StartupTimeoutSeconds = 15,
    [int] $ObservationSeconds = 5
)

$ErrorActionPreference = 'Stop'

function Resolve-FullPath([string] $Path) {
    $executionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
}

function Wait-ForMainWindow([System.Diagnostics.Process] $Process, [int] $TimeoutSeconds) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($Process.HasExited) {
            throw "BDInfo GUI exited early with code $($Process.ExitCode)."
        }

        $Process.Refresh()
        if ($Process.MainWindowHandle -ne [IntPtr]::Zero) {
            return
        }

        Start-Sleep -Milliseconds 250
    }

    throw "BDInfo GUI did not create a main window within $TimeoutSeconds seconds."
}

function Stop-GuiProcess([System.Diagnostics.Process] $Process) {
    if ($null -eq $Process -or $Process.HasExited) {
        return
    }

    [void] $Process.CloseMainWindow()
    if (-not $Process.WaitForExit(5000)) {
        $Process.Kill()
        $Process.WaitForExit()
    }
}

function Invoke-GuiLaunchSmoke([string] $ExePath, [string[]] $Arguments, [string] $Description) {
    Write-Host "Starting GUI smoke: $Description"
    $process = Start-Process -FilePath $ExePath -ArgumentList $Arguments -PassThru
    try {
        Wait-ForMainWindow $process $StartupTimeoutSeconds
        Start-Sleep -Seconds $ObservationSeconds

        if ($process.HasExited) {
            throw "BDInfo GUI exited during observation for $Description with code $($process.ExitCode)."
        }
    }
    finally {
        Stop-GuiProcess $process
    }
}

$buildOutputFullPath = Resolve-FullPath $BuildOutputPath
$reportFullPath = Resolve-FullPath $ReportPath
$exePath = Join-Path $buildOutputFullPath 'BDInfo.exe'

if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) {
    throw "BDInfo.exe was not found at $exePath. Build Release first or pass -BuildOutputPath."
}

if (-not (Test-Path -LiteralPath $reportFullPath -PathType Leaf)) {
    throw "Snapshot report fixture was not found at $reportFullPath."
}

Invoke-GuiLaunchSmoke $exePath @() 'no arguments'
Invoke-GuiLaunchSmoke $exePath @($reportFullPath) 'single Snapshot v2 .bdinfo path'

Write-Host 'BDInfo GUI launch smoke passed.'

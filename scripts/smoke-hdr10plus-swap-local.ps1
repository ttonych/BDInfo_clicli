param(
    [string] $BuildOutputPath = (Join-Path $PSScriptRoot '..\BDInfo\bin\Release'),
    [string] $FirstSourcePath = 'V:\',
    [string] $SecondSourcePath = 'V:\',
    [string] $OutputPath = (Join-Path $PSScriptRoot '..\tmp_smoke_hdr10plus_swap'),
    [string] $FirstPlaylist = '00800',
    [string] $SecondPlaylist,
    [string] $ReportFormats = 'txt,bdinfo,bdinfo-json',
    [switch] $SelfTest,
    [switch] $WaitForDiscSwap,
    [switch] $KeepOutput
)

$ErrorActionPreference = 'Stop'

function Resolve-FullPath([string] $Path) {
    $executionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
}

function Invoke-BDInfoInProcess([string[]] $Arguments) {
    Write-Host "BDInfo.exe $($Arguments -join ' ')"
    [Environment]::ExitCode = 0
    $handled = $script:TryHandle.Invoke($null, [object[]] @(, $Arguments))

    if (-not $handled) {
        throw 'BDInfo ConsoleRunner did not handle the supplied arguments.'
    }

    if ([Environment]::ExitCode -ne 0) {
        throw "BDInfo.exe exited with code $([Environment]::ExitCode)."
    }
}

function Assert-DirectoryExists([string] $Path, [string] $Description) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Description was not created: $Path"
    }
}

function Test-FileContainsHdr10Plus([string] $Path) {
    $match = Select-String -LiteralPath $Path -SimpleMatch 'HDR10+' -List -ErrorAction SilentlyContinue
    $null -ne $match
}

function Get-ReportFile([string] $Directory, [string] $Format) {
    $pattern = switch ($Format) {
        'txt' { '*.txt' }
        'bdinfo' { '*.bdinfo' }
        'bdinfo-json' { '*.json.bdinfo' }
        default { throw "Unsupported report format in smoke script: $Format" }
    }

    $files = Get-ChildItem -LiteralPath $Directory -Filter $pattern -File -ErrorAction SilentlyContinue

    if ($Format -eq 'bdinfo') {
        $files = $files | Where-Object { $_.Name -notlike '*.json.bdinfo' }
    }

    $files | Select-Object -First 1
}

function Get-ReportFiles([string] $Directory, [string] $Formats) {
    Assert-DirectoryExists $Directory 'Report output directory'

    $reportFiles = New-Object 'System.Collections.Generic.List[System.IO.FileInfo]'
    foreach ($format in ($Formats -split ',' | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })) {
        $file = Get-ReportFile $Directory $format
        if ($null -eq $file) {
            throw "Expected $format report was not created in $Directory."
        }

        $reportFiles.Add($file)
    }

    $reportFiles
}

function Assert-ReportsContainHdr10Plus([string] $Directory, [string] $Formats) {
    foreach ($file in (Get-ReportFiles $Directory $Formats)) {
        if (-not (Test-FileContainsHdr10Plus $file.FullName)) {
            throw "Expected HDR10+ in report: $($file.FullName)"
        }
    }
}

function Assert-ReportsDoNotContainHdr10Plus([string] $Directory, [string] $Formats) {
    foreach ($file in (Get-ReportFiles $Directory $Formats)) {
        if (Test-FileContainsHdr10Plus $file.FullName) {
            throw "Unexpected HDR10+ carryover in report: $($file.FullName)"
        }
    }
}

$buildOutputFullPath = Resolve-FullPath $BuildOutputPath
$outputFullPath = Resolve-FullPath $OutputPath
$exePath = Join-Path $buildOutputFullPath 'BDInfo.exe'

if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) {
    throw "BDInfo.exe was not found at $exePath. Build Release first or pass -BuildOutputPath."
}

Get-ChildItem -LiteralPath $buildOutputFullPath -Filter '*.dll' | ForEach-Object {
    [Reflection.Assembly]::LoadFrom($_.FullName) | Out-Null
}
$assembly = [Reflection.Assembly]::LoadFrom($exePath)
$consoleRunner = $assembly.GetType('BDInfo.ConsoleRunner', $true)
$script:TryHandle = $consoleRunner.GetMethod('TryHandle', [Reflection.BindingFlags] 'Public,Static')
if ($null -eq $script:TryHandle) {
    throw 'BDInfo.ConsoleRunner.TryHandle was not found.'
}

if ($SelfTest) {
    Invoke-BDInfoInProcess @('--help')
    Write-Host 'BDInfo HDR10+ swap smoke self-test passed.'
    return
}

if (-not (Test-Path -LiteralPath $FirstSourcePath)) {
    throw "First source path was not found: $FirstSourcePath"
}

if (Test-Path -LiteralPath $outputFullPath) {
    Remove-Item -LiteralPath $outputFullPath -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $outputFullPath | Out-Null

try {
    $firstOutput = Join-Path $outputFullPath 'first-hdr10plus'
    $secondOutput = Join-Path $outputFullPath 'second-non-hdr10plus'

    Invoke-BDInfoInProcess @($FirstSourcePath, $outputFullPath, '--list')
    Invoke-BDInfoInProcess @($FirstSourcePath, $firstOutput, '-m', $FirstPlaylist, '-r', $ReportFormats)
    Assert-ReportsContainHdr10Plus $firstOutput $ReportFormats

    if ($WaitForDiscSwap) {
        Write-Host ''
        Write-Host 'Swap to a known non-HDR10+ disc without closing this PowerShell process.'
        if ([string]::IsNullOrWhiteSpace($SecondPlaylist)) {
            $SecondPlaylist = Read-Host 'Enter the non-HDR10+ playlist number, for example 00107'
        }
        else {
            Read-Host "Press Enter after the non-HDR10+ disc is ready at $SecondSourcePath"
        }
    }

    if ([string]::IsNullOrWhiteSpace($SecondPlaylist)) {
        throw 'SecondPlaylist is required. Pass -SecondPlaylist or use -WaitForDiscSwap to enter it interactively.'
    }

    if (-not (Test-Path -LiteralPath $SecondSourcePath)) {
        throw "Second source path was not found: $SecondSourcePath"
    }

    Invoke-BDInfoInProcess @($SecondSourcePath, $outputFullPath, '--list')
    Invoke-BDInfoInProcess @($SecondSourcePath, $secondOutput, '-m', $SecondPlaylist, '-r', $ReportFormats)
    Assert-ReportsDoNotContainHdr10Plus $secondOutput $ReportFormats

    Write-Host "BDInfo HDR10+ swap smoke passed: $outputFullPath"
}
finally {
    if (-not $KeepOutput -and (Test-Path -LiteralPath $outputFullPath)) {
        Remove-Item -LiteralPath $outputFullPath -Recurse -Force
    }
}

param(
    [string] $BuildOutputPath = (Join-Path $PSScriptRoot '..\BDInfo\bin\Release'),
    [string] $FirstSourcePath = 'V:\',
    [string] $SecondSourcePath = 'V:\',
    [string] $OutputPath = (Join-Path $PSScriptRoot '..\tmp_smoke_hdr10plus_swap'),
    [string] $FirstPlaylist = '00800',
    [string] $SecondPlaylist,
    [string] $ReportFormats = 'txt,bdinfo,bdinfo-json,bdinfo-xml',
    [string] $SwapReadySignalPath,
    [string] $DiscReadySignalPath,
    [int] $DiscReadyPollSeconds = 2,
    [switch] $SelfTest,
    [switch] $SyntheticFirst,
    [switch] $WaitForDiscSwap,
    [switch] $KeepOutput
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

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

function Invoke-HevcScan($Stream) {
    $buffer = New-Object BDInfo.TSStreamBuffer
    $buffer.BeginRead()
    $tag = $null
    [BDInfo.TSCodecHEVC]::Scan($Stream, $buffer, [ref] $tag)
}

function Set-SyntheticHdr10PlusState {
    $hdrData = New-Object 'BDInfo.TSCodecHEVC+ExtendedDataSet'
    $hdrData.IsHdr10Plus = $true
    $hdrStream = New-Object BDInfo.TSVideoStream
    $hdrStream.StreamType = [BDInfo.TSStreamType]::HEVC_VIDEO
    $hdrStream.ExtendedData = $hdrData

    Invoke-HevcScan $hdrStream
    if (-not [BDInfo.TSCodecHEVC]::IsHdr10Plus) {
        throw 'Synthetic HDR10+ scan did not set TSCodecHEVC.IsHdr10Plus.'
    }

    Write-Host 'Synthetic HDR10+ state set in current process.'
}

function Wait-ForDiscReadySignal([string] $ReadyPath, [string] $ContinuePath) {
    $readyFullPath = Resolve-FullPath $ReadyPath
    $continueFullPath = Resolve-FullPath $ContinuePath

    $readyDirectory = Split-Path -Parent $readyFullPath
    if ($readyDirectory) {
        New-Item -ItemType Directory -Force -Path $readyDirectory | Out-Null
    }

    Set-Content -LiteralPath $readyFullPath -Value ([DateTime]::UtcNow.ToString('O'))
    Write-Host "Waiting for disc-ready signal: $continueFullPath"

    while (-not (Test-Path -LiteralPath $continueFullPath -PathType Leaf)) {
        Start-Sleep -Seconds $DiscReadyPollSeconds
    }

    Remove-Item -LiteralPath $continueFullPath -Force
}

function Assert-DirectoryExists([string] $Path, [string] $Description) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Description was not created: $Path"
    }
}

function Test-FileContainsHdr10Plus([string] $Path) {
    (Get-ReportSearchText $Path) -match 'HDR10\+'
}

function Get-ReportSearchText([string] $Path) {
    $stream = [IO.File]::OpenRead($Path)
    try {
        $first = $stream.ReadByte()
        $second = $stream.ReadByte()
    }
    finally {
        $stream.Dispose()
    }

    if ($first -eq [byte][char]'P' -and $second -eq [byte][char]'K') {
        $archive = [IO.Compression.ZipFile]::OpenRead($Path)
        try {
            $text = New-Object Text.StringBuilder
            foreach ($entry in $archive.Entries) {
                $entryStream = $entry.Open()
                try {
                    $reader = New-Object IO.StreamReader($entryStream, [Text.Encoding]::UTF8)
                    try {
                        [void] $text.AppendLine($reader.ReadToEnd())
                    }
                    finally {
                        $reader.Dispose()
                    }
                }
                finally {
                    $entryStream.Dispose()
                }
            }

            return $text.ToString()
        }
        finally {
            $archive.Dispose()
        }
    }

    Get-Content -LiteralPath $Path -Raw
}

function Get-ReportFile([string] $Directory, [string] $Format) {
    $pattern = switch ($Format) {
        'txt' { '*.txt' }
        'bdinfo' { '*.bdinfo' }
        'bdinfo-json' { '*.json.bdinfo' }
        'bdinfo-xml' { '*.xml.bdinfo' }
        default { throw "Unsupported report format in smoke script: $Format" }
    }

    $files = Get-ChildItem -LiteralPath $Directory -Filter $pattern -File -ErrorAction SilentlyContinue

    if ($Format -eq 'bdinfo') {
        $files = $files | Where-Object { $_.Name -notlike '*.json.bdinfo' -and $_.Name -notlike '*.xml.bdinfo' }
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

if (-not $SyntheticFirst -and -not (Test-Path -LiteralPath $FirstSourcePath)) {
    throw "First source path was not found: $FirstSourcePath"
}

if (Test-Path -LiteralPath $outputFullPath) {
    Remove-Item -LiteralPath $outputFullPath -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $outputFullPath | Out-Null

try {
    $firstOutput = Join-Path $outputFullPath 'first-hdr10plus'
    $secondOutput = Join-Path $outputFullPath 'second-non-hdr10plus'

    if ($SyntheticFirst) {
        Set-SyntheticHdr10PlusState
    }
    else {
        Invoke-BDInfoInProcess @($FirstSourcePath, $outputFullPath, '--list')
        Invoke-BDInfoInProcess @($FirstSourcePath, $firstOutput, '-m', $FirstPlaylist, '-r', $ReportFormats)
        Assert-ReportsContainHdr10Plus $firstOutput $ReportFormats
    }

    if ($DiscReadySignalPath) {
        if ([string]::IsNullOrWhiteSpace($SwapReadySignalPath)) {
            throw 'SwapReadySignalPath is required when DiscReadySignalPath is used.'
        }

        Write-Host ''
        if ($SyntheticFirst) {
            Write-Host 'Insert or keep a known non-HDR10+ disc without closing this PowerShell process.'
        }
        else {
            Write-Host 'Swap to a known non-HDR10+ disc without closing this PowerShell process.'
        }

        Wait-ForDiscReadySignal $SwapReadySignalPath $DiscReadySignalPath
    }
    elseif ($WaitForDiscSwap) {
        Write-Host ''
        if ($SyntheticFirst) {
            Write-Host 'Insert or keep a known non-HDR10+ disc without closing this PowerShell process.'
        }
        else {
            Write-Host 'Swap to a known non-HDR10+ disc without closing this PowerShell process.'
        }
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

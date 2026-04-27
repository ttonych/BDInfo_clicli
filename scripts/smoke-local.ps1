param(
    [string] $BuildOutputPath = (Join-Path $PSScriptRoot '..\BDInfo\bin\Release'),
    [string] $SourcePath = 'V:\',
    [string] $OutputPath = (Join-Path $PSScriptRoot '..\tmp_smoke_local'),
    [string] $Playlist = '00107',
    [string] $PlainReportFormats = 'txt,bdinfo,bdinfo-json,bdinfo-xml',
    [string] $CompressedReportFormats = 'bdinfo,bdinfo-json,bdinfo-xml',
    [string] $ChartFormat = 'jpg',
    [switch] $SkipCharts,
    [switch] $KeepOutput
)

$ErrorActionPreference = 'Stop'

function Resolve-FullPath([string] $Path) {
    $executionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
}

function Invoke-BDInfo([string[]] $Arguments) {
    Write-Host "BDInfo.exe $($Arguments -join ' ')"
    & $script:ExePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "BDInfo.exe exited with code $LASTEXITCODE."
    }
}

function Assert-DirectoryExists([string] $Path, [string] $Description) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Description was not created: $Path"
    }
}

function Get-ExpectedReportPath([string] $Directory, [string] $Format, [bool] $Compressed) {
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

function Assert-ReportFormats([string] $Directory, [string] $Formats, [bool] $Compressed) {
    Assert-DirectoryExists $Directory 'Report output directory'

    foreach ($format in ($Formats -split ',' | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })) {
        $file = Get-ExpectedReportPath $Directory $format $Compressed
        if ($null -eq $file) {
            throw "Expected $format report was not created in $Directory."
        }

        if (($Compressed -and $format -ne 'txt') -or $format -eq 'bdinfo') {
            $stream = [IO.File]::OpenRead($file.FullName)
            try {
                $first = $stream.ReadByte()
                $second = $stream.ReadByte()
                if ($first -ne [byte][char]'P' -or $second -ne [byte][char]'K') {
                    throw "Expected compressed report is not a ZIP archive: $($file.FullName)"
                }
            }
            finally {
                $stream.Dispose()
            }
        }
    }
}

$buildOutputFullPath = Resolve-FullPath $BuildOutputPath
$outputFullPath = Resolve-FullPath $OutputPath
$script:ExePath = Join-Path $buildOutputFullPath 'BDInfo.exe'

if (-not (Test-Path -LiteralPath $script:ExePath -PathType Leaf)) {
    throw "BDInfo.exe was not found at $script:ExePath. Build Release first or pass -BuildOutputPath."
}

if (-not (Test-Path -LiteralPath $SourcePath)) {
    throw "Source path was not found: $SourcePath"
}

if (Test-Path -LiteralPath $outputFullPath) {
    Remove-Item -LiteralPath $outputFullPath -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $outputFullPath | Out-Null

try {
    $plainOutput = Join-Path $outputFullPath 'plain'
    $compressedOutput = Join-Path $outputFullPath 'compressed'

    Invoke-BDInfo @($SourcePath, $outputFullPath, '--list')

    $plainArgs = @($SourcePath, $plainOutput, '-m', $Playlist, '-r', $PlainReportFormats)
    if (-not $SkipCharts) {
        $plainArgs += @('--charts', $ChartFormat)
    }
    Invoke-BDInfo $plainArgs
    Assert-ReportFormats $plainOutput $PlainReportFormats $false

    if (-not $SkipCharts) {
        $chartsOutput = Join-Path $plainOutput 'charts'
        Assert-DirectoryExists $chartsOutput 'Charts output directory'
        $chartFiles = Get-ChildItem -LiteralPath $chartsOutput -Filter "*.$ChartFormat" -File -ErrorAction SilentlyContinue
        if (($chartFiles | Measure-Object).Count -lt 1) {
            throw "No .$ChartFormat chart files were created in $chartsOutput."
        }
    }

    Invoke-BDInfo @($SourcePath, $compressedOutput, '-m', $Playlist, '-r', $CompressedReportFormats, '--compress')
    Assert-ReportFormats $compressedOutput $CompressedReportFormats $true

    Write-Host "BDInfo local smoke passed: $outputFullPath"
}
finally {
    if (-not $KeepOutput -and (Test-Path -LiteralPath $outputFullPath)) {
        Remove-Item -LiteralPath $outputFullPath -Recurse -Force
    }
}

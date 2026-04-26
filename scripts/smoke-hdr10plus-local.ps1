param(
    [string] $BuildOutputPath = (Join-Path $PSScriptRoot '..\BDInfo\bin\Release'),
    [string] $SourcePath = 'V:\',
    [string] $OutputPath = (Join-Path $PSScriptRoot '..\tmp_smoke_hdr10plus_local'),
    [string] $Playlist = '00800',
    [string] $ReportFormats = 'txt,bdinfo,bdinfo-json',
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

function Assert-FileContainsHdr10Plus([string] $Path, [string] $Description) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description was not created: $Path"
    }

    if ((Get-Content -LiteralPath $Path -Raw) -notmatch 'HDR10\+') {
        throw "$Description does not contain HDR10+: $Path"
    }
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

function Assert-Hdr10PlusReports([string] $Directory, [string] $Formats) {
    Assert-DirectoryExists $Directory 'Report output directory'

    foreach ($format in ($Formats -split ',' | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })) {
        $file = Get-ReportFile $Directory $format
        if ($null -eq $file) {
            throw "Expected $format report was not created in $Directory."
        }

        Assert-FileContainsHdr10Plus $file.FullName "$format report"
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
    $reportOutput = Join-Path $outputFullPath 'report'

    Invoke-BDInfo @($SourcePath, $outputFullPath, '--list')

    $reportArgs = @($SourcePath, $reportOutput, '-m', $Playlist, '-r', $ReportFormats)
    if (-not $SkipCharts) {
        $reportArgs += @('--charts', $ChartFormat)
    }
    Invoke-BDInfo $reportArgs
    Assert-Hdr10PlusReports $reportOutput $ReportFormats

    if (-not $SkipCharts) {
        $chartsOutput = Join-Path $reportOutput 'charts'
        Assert-DirectoryExists $chartsOutput 'Charts output directory'
        $chartFiles = Get-ChildItem -LiteralPath $chartsOutput -Filter "*.$ChartFormat" -File -ErrorAction SilentlyContinue
        if (($chartFiles | Measure-Object).Count -lt 1) {
            throw "No .$ChartFormat chart files were created in $chartsOutput."
        }
    }

    Write-Host "BDInfo HDR10+ local smoke passed: $outputFullPath"
}
finally {
    if (-not $KeepOutput -and (Test-Path -LiteralPath $outputFullPath)) {
        Remove-Item -LiteralPath $outputFullPath -Recurse -Force
    }
}

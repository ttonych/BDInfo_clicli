param(
    [string] $BuildOutputPath = (Join-Path $PSScriptRoot '..\BDInfo\bin\Release')
)

$ErrorActionPreference = 'Stop'

function Resolve-FullPath([string] $Path) {
    $executionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
}

function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) {
        throw $Message
    }
}

function Invoke-HevcScan($Stream) {
    $buffer = New-Object BDInfo.TSStreamBuffer
    $buffer.BeginRead()
    $tag = $null
    [BDInfo.TSCodecHEVC]::Scan($Stream, $buffer, [ref] $tag)
}

$buildOutputFullPath = Resolve-FullPath $BuildOutputPath
$exePath = Join-Path $buildOutputFullPath 'BDInfo.exe'

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "BDInfo.exe was not found at $exePath. Build the Release configuration first or pass -BuildOutputPath."
}

Get-ChildItem -LiteralPath $buildOutputFullPath -Filter '*.dll' | ForEach-Object {
    [Reflection.Assembly]::LoadFrom($_.FullName) | Out-Null
}
[Reflection.Assembly]::LoadFrom($exePath) | Out-Null

$hdrData = New-Object 'BDInfo.TSCodecHEVC+ExtendedDataSet'
$hdrData.IsHdr10Plus = $true
$hdrStream = New-Object BDInfo.TSVideoStream
$hdrStream.StreamType = [BDInfo.TSStreamType]::HEVC_VIDEO
$hdrStream.ExtendedData = $hdrData

Invoke-HevcScan $hdrStream
Assert-True $hdrStream.ExtendedData.IsHdr10Plus 'HDR10+ source stream did not preserve its HDR10+ state.'
Assert-True ([BDInfo.TSCodecHEVC]::IsHdr10Plus) 'HDR10+ static state was not set by the HDR10+ source stream.'

$nonHdrStream = New-Object BDInfo.TSVideoStream
$nonHdrStream.StreamType = [BDInfo.TSStreamType]::HEVC_VIDEO

Invoke-HevcScan $nonHdrStream
Assert-True ($null -ne $nonHdrStream.ExtendedData) 'Fresh HEVC stream did not receive HEVC extended data.'
Assert-True (-not $nonHdrStream.ExtendedData.IsHdr10Plus) 'Fresh HEVC stream inherited HDR10+ state from a previous stream.'
Assert-True (-not [BDInfo.TSCodecHEVC]::IsHdr10Plus) 'HEVC static HDR10+ state was not reset for a fresh non-HDR10+ stream.'

Write-Host 'HDR10+ carryover smoke passed.'

param(
    [string] $BuildOutputPath = (Join-Path $PSScriptRoot '..\BDInfo\bin\Release'),
    [string] $OutputPath = (Join-Path $PSScriptRoot '..\tmp_report_roundtrip'),
    [string] $FixturePath = (Join-Path $PSScriptRoot '..\tests\fixtures\report-roundtrip'),
    [switch] $UpdateFixtures,
    [switch] $KeepOutput
)

$ErrorActionPreference = 'Stop'

function Resolve-FullPath([string] $Path) {
    $executionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
}

function New-GenericList([Type] $ItemType) {
    $listType = ([System.Collections.Generic.List[int]]).GetGenericTypeDefinition().MakeGenericType($ItemType)
    , [Activator]::CreateInstance($listType)
}

function Add-ListItem($List, $Item) {
    [void] $List.Add($Item)
}

function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) {
        throw $Message
    }
}

function Write-ReportFile($Report, [string] $Path, [string] $Format) {
    $directory = Split-Path -Parent $Path
    if ($directory) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $stream = [IO.File]::Create($Path)
    try {
        if ($Format -eq 'Json') {
            $settings = New-Object Runtime.Serialization.Json.DataContractJsonSerializerSettings
            $settings.UseSimpleDictionaryFormat = $true
            $serializer = New-Object Runtime.Serialization.Json.DataContractJsonSerializer(
                [BDInfo.Reporting.BDInfoReportData],
                $settings)
            $serializer.WriteObject($stream, $Report)
        }
        else {
            $serializer = New-Object Runtime.Serialization.DataContractSerializer(
                [BDInfo.Reporting.BDInfoReportData])
            $serializer.WriteObject($stream, $Report)
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Write-CompressedReportFile($Report, [string] $Path, [string] $Format) {
    $directory = Split-Path -Parent $Path
    if ($directory) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Force
    }

    $archive = [IO.Compression.ZipFile]::Open($Path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $entryName = if ($Format -eq 'Json') { 'report.json' } else { 'report.xml' }
        $entry = $archive.CreateEntry($entryName, [IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = [DateTimeOffset]::Parse('2026-01-01T00:00:00Z')
        $entryStream = $entry.Open()
        try {
            $tempPath = [IO.Path]::GetTempFileName()
            try {
                Write-ReportFile $Report $tempPath $Format
                $source = [IO.File]::OpenRead($tempPath)
                try {
                    $source.CopyTo($entryStream)
                }
                finally {
                    $source.Dispose()
                }
            }
            finally {
                if (Test-Path -LiteralPath $tempPath) {
                    Remove-Item -LiteralPath $tempPath -Force
                }
            }
        }
        finally {
            $entryStream.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

function New-SampleReport {
    $streamDataType = [BDInfo.Reporting.StreamData]

    $video = New-Object BDInfo.Reporting.VideoStreamData
    $video.PID = 4113
    $video.StreamType = [BDInfo.TSStreamType]::HEVC_VIDEO
    $video.IsVBR = $true
    $video.BitRate = 48000000
    $video.ActiveBitRate = 52000000
    $video.IsInitialized = $true
    $video.LanguageCode = 'und'
    $video.LanguageName = 'Undetermined'
    $video.PayloadBytes = 1200000
    $video.PacketCount = 6250
    $video.PacketSeconds = 10.0
    $video.VideoFormat = [BDInfo.TSVideoFormat]::VIDEOFORMAT_2160p
    $video.FrameRate = [BDInfo.TSFrameRate]::FRAMERATE_23_976
    $video.AspectRatio = [BDInfo.TSAspectRatio]::ASPECT_16_9
    $video.Width = 3840
    $video.Height = 2160
    $video.EncodingProfile = 'Main 10'
    $video.MasteringDisplayLuminance = 'min: 0.0050 cd/m2, max: 1000 cd/m2'
    $video.MaximumContentLightLevel = 1000
    $video.MaximumFrameAverageLightLevel = 400
    $video.LightLevelAvailable = $true

    $audio = New-Object BDInfo.Reporting.AudioStreamData
    $audio.PID = 4352
    $audio.StreamType = [BDInfo.TSStreamType]::AC3_AUDIO
    $audio.IsVBR = $false
    $audio.BitRate = 640000
    $audio.ActiveBitRate = 640000
    $audio.IsInitialized = $true
    $audio.LanguageCode = 'eng'
    $audio.LanguageName = 'English'
    $audio.PayloadBytes = 160000
    $audio.PacketCount = 834
    $audio.PacketSeconds = 10.0
    $audio.SampleRate = 48000
    $audio.ChannelCount = 6
    $audio.BitDepth = 16
    $audio.LFE = 1
    $audio.DialNorm = -27
    $audio.AudioMode = [BDInfo.TSAudioMode]::Surround
    $audio.ChannelLayout = [BDInfo.TSChannelLayout]::CHANNELLAYOUT_MULTI

    $subtitle = New-Object BDInfo.Reporting.TextStreamData
    $subtitle.PID = 4608
    $subtitle.StreamType = [BDInfo.TSStreamType]::SUBTITLE
    $subtitle.IsInitialized = $true
    $subtitle.LanguageCode = 'eng'
    $subtitle.LanguageName = 'English'

    $allStreams = New-GenericList $streamDataType
    Add-ListItem $allStreams $video
    Add-ListItem $allStreams $audio
    Add-ListItem $allStreams $subtitle

    $videoStreams = New-GenericList $streamDataType
    Add-ListItem $videoStreams $video

    $audioStreams = New-GenericList $streamDataType
    Add-ListItem $audioStreams $audio

    $textStreams = New-GenericList $streamDataType
    Add-ListItem $textStreams $subtitle

    $diagnostic = New-Object BDInfo.Reporting.StreamDiagnosticsData
    $diagnostic.Marker = 0.0
    $diagnostic.Interval = 1.0
    $diagnostic.Bytes = 1200000
    $diagnostic.Packets = 6250
    $diagnostic.Tag = 'sample'

    $diagnostics = New-Object BDInfo.Reporting.StreamDiagnosticsCollectionData
    $diagnostics.PID = 4113
    $diagnostics.Diagnostics = New-GenericList ([BDInfo.Reporting.StreamDiagnosticsData])
    Add-ListItem $diagnostics.Diagnostics $diagnostic

    $streamFile = New-Object BDInfo.Reporting.StreamFileData
    $streamFile.Name = '00001.M2TS'
    $streamFile.Size = 10485760
    $streamFile.Length = 10.0
    $streamFile.Streams = $allStreams
    $streamFile.Diagnostics = New-GenericList ([BDInfo.Reporting.StreamDiagnosticsCollectionData])
    Add-ListItem $streamFile.Diagnostics $diagnostics

    $clipFile = New-Object BDInfo.Reporting.StreamClipFileData
    $clipFile.Name = '00001.CLPI'
    $clipFile.FileType = 'CLPI'
    $clipFile.Streams = $allStreams

    $clip = New-Object BDInfo.Reporting.StreamClipData
    $clip.Name = '00001.M2TS'
    $clip.StreamFileName = '00001.M2TS'
    $clip.StreamClipFileName = '00001.CLPI'
    $clip.TimeIn = 0.0
    $clip.TimeOut = 10.0
    $clip.RelativeTimeIn = 0.0
    $clip.RelativeTimeOut = 10.0
    $clip.Length = 10.0
    $clip.RelativeLength = 10.0
    $clip.FileSize = 10485760
    $clip.PayloadBytes = 1360000
    $clip.PacketCount = 7084
    $clip.PacketSeconds = 10.0
    $clip.Chapters = New-GenericList ([double])
    Add-ListItem $clip.Chapters 0.0

    $playlist = New-Object BDInfo.Reporting.PlaylistData
    $playlist.Name = '00001.MPLS'
    $playlist.AngleCount = 1
    $playlist.Chapters = New-GenericList ([double])
    Add-ListItem $playlist.Chapters 0.0
    $playlist.StreamClips = New-GenericList ([BDInfo.Reporting.StreamClipData])
    Add-ListItem $playlist.StreamClips $clip
    $playlist.AngleClips = New-GenericList ([BDInfo.Reporting.AngleClipData])
    $playlist.AngleStreams = New-GenericList ([BDInfo.Reporting.AngleStreamData])
    $playlist.Streams = $allStreams
    $playlist.PlaylistStreams = $allStreams
    $playlist.SortedStreams = $allStreams
    $playlist.VideoStreams = $videoStreams
    $playlist.AudioStreams = $audioStreams
    $playlist.TextStreams = $textStreams
    $playlist.GraphicsStreams = New-GenericList $streamDataType

    $disc = New-Object BDInfo.Reporting.BDROMData
    $disc.VolumeLabel = 'ROUNDTRIP_SMOKE'
    $disc.DiscTitle = 'Round-trip Smoke'
    $disc.Size = 10485760
    $disc.IsUHD = $true
    $disc.StreamFiles = New-GenericList ([BDInfo.Reporting.StreamFileData])
    Add-ListItem $disc.StreamFiles $streamFile
    $disc.StreamClipFiles = New-GenericList ([BDInfo.Reporting.StreamClipFileData])
    Add-ListItem $disc.StreamClipFiles $clipFile
    $disc.InterleavedFiles = New-GenericList ([BDInfo.Reporting.InterleavedFileData])
    $disc.Playlists = New-GenericList ([BDInfo.Reporting.PlaylistData])
    Add-ListItem $disc.Playlists $playlist

    $fileException = New-Object BDInfo.Reporting.ScanFileExceptionData
    $fileException.FileName = '00002.M2TS'
    $fileException.Message = 'Synthetic smoke exception'
    $fileException.StackTrace = 'at smoke'

    $scan = New-Object BDInfo.Reporting.ScanResultData
    $scan.FileExceptions = New-GenericList ([BDInfo.Reporting.ScanFileExceptionData])
    Add-ListItem $scan.FileExceptions $fileException

    $report = New-Object BDInfo.Reporting.BDInfoReportData
    $report.Version = 'smoke'
    $report.CreatedUtc = [DateTime]::Parse('2026-01-01T00:00:00Z').ToUniversalTime()
    $report.SourcePath = 'synthetic'
    $report.Disc = $disc
    $report.ScanResult = $scan
    $report
}

function Write-SampleFixtureSet($Report, [string] $Directory) {
    New-Item -ItemType Directory -Force -Path $Directory | Out-Null

    Get-ChildItem -LiteralPath $Directory -Filter '*.bdinfo' -File -ErrorAction SilentlyContinue |
        Remove-Item -Force

    Write-ReportFile $Report (Join-Path $Directory 'sample-xml.bdinfo') 'Xml'
    Write-ReportFile $Report (Join-Path $Directory 'sample-json.bdinfo') 'Json'
    Write-CompressedReportFile $Report (Join-Path $Directory 'sample-xml-compressed.bdinfo') 'Xml'
    Write-CompressedReportFile $Report (Join-Path $Directory 'sample-json-compressed.bdinfo') 'Json'
}

function Test-ReportFixtureDirectory([string] $Directory) {
    if (-not (Test-Path -LiteralPath $Directory -PathType Container)) {
        throw "Report fixture directory was not found: $Directory"
    }

    $files = Get-ChildItem -LiteralPath $Directory -Filter '*.bdinfo' -File -ErrorAction SilentlyContinue
    Assert-True (($files | Measure-Object).Count -gt 0) "No .bdinfo report fixtures were found in $Directory"

    foreach ($file in $files) {
        Test-ReportLoad $file.FullName | Out-Null
    }
}

function Test-ReportLoad([string] $Path) {
    $loaded = [BDInfo.Reporting.BDInfoReportSerializer]::Load($Path)
    Assert-True ($null -ne $loaded) "Report did not load: $Path"
    Assert-True ($loaded.Disc.VolumeLabel -eq 'ROUNDTRIP_SMOKE') "Unexpected volume label in $Path"
    Assert-True ($loaded.SourcePath -eq $Path) "Loaded report SourcePath was not rewritten to the loaded file path."

    $bdrom = [BDInfo.Reporting.BDInfoReportSerializer]::CreateBDROM($loaded)
    Assert-True $bdrom.IsReport "CreateBDROM did not mark BDROM as report-backed."
    Assert-True ($bdrom.VolumeLabel -eq 'ROUNDTRIP_SMOKE') "CreateBDROM lost the volume label."
    Assert-True ($bdrom.PlaylistFiles.ContainsKey('00001.MPLS')) "Playlist was not restored."
    Assert-True ($bdrom.StreamFiles.ContainsKey('00001.M2TS')) "Stream file was not restored."
    Assert-True ($bdrom.StreamClipFiles.ContainsKey('00001.CLPI')) "Stream clip file was not restored."

    $playlist = $bdrom.PlaylistFiles['00001.MPLS']
    Assert-True ($playlist.StreamClips.Count -eq 1) "Playlist stream clips were not restored."
    Assert-True ($playlist.VideoStreams.Count -eq 1) "Playlist video streams were not restored."
    Assert-True ($playlist.AudioStreams.Count -eq 1) "Playlist audio streams were not restored."
    Assert-True ($playlist.TextStreams.Count -eq 1) "Playlist text streams were not restored."
    Assert-True ($playlist.VideoStreams[0].Width -eq 3840) "Video stream width was not restored."
    Assert-True ($playlist.AudioStreams[0].ChannelCount -eq 6) "Audio stream channel count was not restored."

    $streamFile = $bdrom.StreamFiles['00001.M2TS']
    Assert-True ($streamFile.StreamDiagnostics.ContainsKey([ushort] 4113)) "Stream diagnostics were not restored."
    Assert-True ($streamFile.StreamDiagnostics[[ushort] 4113].Count -eq 1) "Unexpected diagnostics count."

    $scan = [BDInfo.Reporting.BDInfoReportSerializer]::CreateScanResult($loaded.ScanResult)
    Assert-True ($scan.FileExceptions.Count -eq 1) "Scan file exceptions were not restored."

    @{
        Report = $loaded
        BDROM = $bdrom
        ScanResult = $scan
    }
}

function Test-SnapshotV2Archive([string] $Path) {
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entry = $archive.GetEntry('snapshot.json')
        Assert-True ($null -ne $entry) "Snapshot v2 archive is missing snapshot.json: $Path"
        $stream = $entry.Open()
        try {
            $reader = New-Object IO.StreamReader($stream, [Text.Encoding]::UTF8)
            $json = $reader.ReadToEnd()
            Assert-True ($json -match '"format"\s*:\s*"BDInfo_clicli"') "Snapshot v2 format marker was not written."
            Assert-True ($json -match '"schemaVersion"\s*:\s*2') "Snapshot v2 schema version was not written."
            Assert-True ($json -match '"payloadKind"\s*:\s*"reportSnapshot"') "Snapshot v2 payload kind was not written."
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

$buildOutputFullPath = Resolve-FullPath $BuildOutputPath
$outputFullPath = Resolve-FullPath $OutputPath
$fixtureFullPath = Resolve-FullPath $FixturePath
$exePath = Join-Path $buildOutputFullPath 'BDInfo.exe'

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "BDInfo.exe was not found at $exePath. Build the Release configuration first or pass -BuildOutputPath."
}

Add-Type -AssemblyName System.Runtime.Serialization
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

Get-ChildItem -LiteralPath $buildOutputFullPath -Filter '*.dll' | ForEach-Object {
    [Reflection.Assembly]::LoadFrom($_.FullName) | Out-Null
}
[Reflection.Assembly]::LoadFrom($exePath) | Out-Null

if (Test-Path -LiteralPath $outputFullPath) {
    Remove-Item -LiteralPath $outputFullPath -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $outputFullPath | Out-Null

try {
    $report = New-SampleReport

    if ($UpdateFixtures) {
        Write-SampleFixtureSet $report $fixtureFullPath
    }

    Test-ReportFixtureDirectory $fixtureFullPath

    $seedXmlPath = Join-Path $outputFullPath 'seed-xml.bdinfo'
    $seedJsonPath = Join-Path $outputFullPath 'seed-json.bdinfo'
    $seedZipJsonPath = Join-Path $outputFullPath 'seed-json-compressed.bdinfo'

    Write-ReportFile $report $seedXmlPath 'Xml'
    Write-ReportFile $report $seedJsonPath 'Json'
    Write-CompressedReportFile $report $seedZipJsonPath 'Json'

    $seedXml = Test-ReportLoad $seedXmlPath
    Test-ReportLoad $seedJsonPath | Out-Null
    Test-ReportLoad $seedZipJsonPath | Out-Null

    $savedXmlPath = Join-Path $outputFullPath 'saved-xml.bdinfo'
    $savedJsonPath = Join-Path $outputFullPath 'saved-json.bdinfo'
    $savedZipXmlPath = Join-Path $outputFullPath 'saved-xml-compressed.bdinfo'
    $savedZipJsonPath = Join-Path $outputFullPath 'saved-json-compressed.bdinfo'
    $savedSnapshotV2Path = Join-Path $outputFullPath 'saved-snapshot-v2.bdinfo'

    [BDInfo.Reporting.BDInfoReportSerializer]::Save(
        $savedXmlPath,
        $seedXml.BDROM,
        $seedXml.BDROM.PlaylistFiles.Values,
        $seedXml.ScanResult,
        [BDInfo.Reporting.BDInfoReportFormat]::Xml,
        $false)
    [BDInfo.Reporting.BDInfoReportSerializer]::Save(
        $savedJsonPath,
        $seedXml.BDROM,
        $seedXml.BDROM.PlaylistFiles.Values,
        $seedXml.ScanResult,
        [BDInfo.Reporting.BDInfoReportFormat]::Json,
        $false)
    [BDInfo.Reporting.BDInfoReportSerializer]::Save(
        $savedZipXmlPath,
        $seedXml.BDROM,
        $seedXml.BDROM.PlaylistFiles.Values,
        $seedXml.ScanResult,
        [BDInfo.Reporting.BDInfoReportFormat]::Xml,
        $true)
    [BDInfo.Reporting.BDInfoReportSerializer]::Save(
        $savedZipJsonPath,
        $seedXml.BDROM,
        $seedXml.BDROM.PlaylistFiles.Values,
        $seedXml.ScanResult,
        [BDInfo.Reporting.BDInfoReportFormat]::Json,
        $true)
    [BDInfo.Reporting.BDInfoReportSerializer]::SaveSnapshotV2(
        $savedSnapshotV2Path,
        $seedXml.BDROM,
        $seedXml.BDROM.PlaylistFiles.Values,
        $seedXml.ScanResult,
        $true)

    Test-ReportLoad $savedXmlPath | Out-Null
    Test-ReportLoad $savedJsonPath | Out-Null
    Test-ReportLoad $savedZipXmlPath | Out-Null
    Test-ReportLoad $savedZipJsonPath | Out-Null
    Test-SnapshotV2Archive $savedSnapshotV2Path
    Test-ReportLoad $savedSnapshotV2Path | Out-Null

    Write-Host "BDInfo report round-trip smoke passed: $outputFullPath"
}
finally {
    if (-not $KeepOutput -and (Test-Path -LiteralPath $outputFullPath)) {
        Remove-Item -LiteralPath $outputFullPath -Recurse -Force
    }
}

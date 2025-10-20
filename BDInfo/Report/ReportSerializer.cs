using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using BDInfo;

namespace BDInfo.Reporting
{
    public enum BDInfoReportFormat
    {
        Xml,
        Json
    }

    public static class BDInfoReportSerializer
    {
        private static readonly DataContractSerializer XmlSerializer = new DataContractSerializer(typeof(BDInfoReportData));
        private static readonly DataContractJsonSerializer JsonSerializer = new DataContractJsonSerializer(
            typeof(BDInfoReportData),
            new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true
            });

        public static void Save(
            string path,
            BDROM bdrom,
            IEnumerable<TSPlaylistFile> playlists,
            ScanBDROMResult scanResult,
            BDInfoReportFormat format = BDInfoReportFormat.Xml,
            bool compress = false)
        {
            if (bdrom == null)
            {
                throw new ArgumentNullException(nameof(bdrom));
            }

            if (playlists == null)
            {
                throw new ArgumentNullException(nameof(playlists));
            }

            var playlistList = playlists.ToList();
            var report = new BDInfoReportData
            {
                CreatedUtc = DateTime.UtcNow,
                Version = typeof(BDInfoReportSerializer).Assembly.GetName().Version?.ToString(),
                SourcePath = GetSourcePath(bdrom),
                Disc = BuildDiscData(bdrom, playlistList),
                ScanResult = ConvertScanResult(scanResult)
            };

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

            if (compress)
            {
                using (var fileStream = File.Create(path))
                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
                {
                    string entryName = format == BDInfoReportFormat.Json ? "report.json" : "report.xml";
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                    using (var entryStream = entry.Open())
                    {
                        WriteReport(entryStream, report, format);
                    }
                }
            }
            else
            {
                using (var stream = File.Create(path))
                {
                    WriteReport(stream, report, format);
                }
            }
        }

        public static BDInfoReportData Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be empty.", nameof(path));
            }
            using (var stream = File.OpenRead(path))
            {
                if (IsZipArchive(stream))
                {
                    using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
                    {
                        var entry = SelectReportEntry(archive);
                        if (entry == null)
                        {
                            throw new SerializationException("Report archive does not contain a report entry.");
                        }

                        using (var entryStream = entry.Open())
                        using (var buffer = new MemoryStream())
                        {
                            entryStream.CopyTo(buffer);
                            buffer.Seek(0, SeekOrigin.Begin);

                            var format = DetectFormat(buffer);
                            buffer.Seek(0, SeekOrigin.Begin);
                            var report = ReadReport(buffer, format);
                            if (report != null)
                            {
                                report.SourcePath = path;
                            }

                            return report;
                        }
                    }
                }
                else
                {
                    var format = DetectFormat(stream);
                    stream.Seek(0, SeekOrigin.Begin);
                    var report = ReadReport(stream, format);
                    if (report != null)
                    {
                        report.SourcePath = path;
                    }

                    return report;
                }
            }
        }

        private static BDInfoReportFormat DetectFormat(Stream stream)
        {
            if (!stream.CanSeek)
            {
                throw new NotSupportedException("Report streams must be seekable.");
            }

            long originalPosition = stream.Position;
            try
            {
                stream.Seek(0, SeekOrigin.Begin);

                int nextByte;
                do
                {
                    nextByte = stream.ReadByte();
                }
                while (nextByte != -1 && char.IsWhiteSpace((char)nextByte));

                if (nextByte == -1)
                {
                    throw new SerializationException("Report file is empty.");
                }

                if (nextByte == '{' || nextByte == '[')
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    return BDInfoReportFormat.Json;
                }

                stream.Seek(0, SeekOrigin.Begin);
                return BDInfoReportFormat.Xml;
            }
            finally
            {
                stream.Seek(originalPosition, SeekOrigin.Begin);
            }
        }

        private static bool IsZipArchive(Stream stream)
        {
            if (!stream.CanSeek)
            {
                return false;
            }

            long originalPosition = stream.Position;
            try
            {
                stream.Seek(0, SeekOrigin.Begin);
                int first = stream.ReadByte();
                int second = stream.ReadByte();
                return first == 'P' && second == 'K';
            }
            finally
            {
                stream.Seek(originalPosition, SeekOrigin.Begin);
            }
        }

        private static ZipArchiveEntry SelectReportEntry(ZipArchive archive)
        {
            if (archive == null)
            {
                return null;
            }

            var entry = archive.Entries
                .FirstOrDefault(e => string.Equals(e.Name, "report.json", StringComparison.OrdinalIgnoreCase))
                       ?? archive.Entries
                .FirstOrDefault(e => string.Equals(e.Name, "report.xml", StringComparison.OrdinalIgnoreCase));

            if (entry != null)
            {
                return entry;
            }

            return archive.Entries.FirstOrDefault(e => e.Length > 0);
        }

        private static void WriteReport(Stream stream, BDInfoReportData report, BDInfoReportFormat format)
        {
            switch (format)
            {
                case BDInfoReportFormat.Json:
                    using (var writer = JsonReaderWriterFactory.CreateJsonWriter(
                               stream,
                               Encoding.UTF8,
                               ownsStream: false,
                               indent: true,
                               indentChars: "  "))
                    {
                        JsonSerializer.WriteObject(writer, report);
                        writer.Flush();
                    }

                    if (stream.CanSeek)
                    {
                        stream.SetLength(stream.Position);
                    }

                    break;

                default:
                    using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Indent = true }))
                    {
                        XmlSerializer.WriteObject(writer, report);
                    }

                    break;
            }
        }

        private static BDInfoReportData ReadReport(Stream stream, BDInfoReportFormat format)
        {
            switch (format)
            {
                case BDInfoReportFormat.Json:
                    using (var reader = JsonReaderWriterFactory.CreateJsonReader(stream, Encoding.UTF8, XmlDictionaryReaderQuotas.Max, null))
                    {
                        return (BDInfoReportData)JsonSerializer.ReadObject(reader);
                    }

                default:
                    using (var reader = XmlReader.Create(stream))
                    {
                        return (BDInfoReportData)XmlSerializer.ReadObject(reader);
                    }
            }
        }

        public static BDROM CreateBDROM(BDInfoReportData report)
        {
            if (report?.Disc == null)
            {
                throw new ArgumentException("Report data is incomplete.", nameof(report));
            }

            var bdrom = new BDROM
            {
                VolumeLabel = report.Disc.VolumeLabel,
                DiscTitle = report.Disc.DiscTitle,
                Size = report.Disc.Size,
                IsBDPlus = report.Disc.IsBDPlus,
                IsBDJava = report.Disc.IsBDJava,
                IsDBOX = report.Disc.IsDBOX,
                IsPSP = report.Disc.IsPSP,
                Is3D = report.Disc.Is3D,
                Is50Hz = report.Disc.Is50Hz,
                IsUHD = report.Disc.IsUHD,
                ReportPath = report.SourcePath,
                IsImage = false
            };

            var interleavedMap = new Dictionary<string, TSInterleavedFile>(StringComparer.OrdinalIgnoreCase);
            if (report.Disc.InterleavedFiles != null)
            {
                foreach (var interleavedData in report.Disc.InterleavedFiles)
                {
                    if (string.IsNullOrWhiteSpace(interleavedData.Name))
                    {
                        continue;
                    }

                    var file = new TSInterleavedFile(interleavedData.Name);
                    bdrom.InterleavedFiles[file.Name] = file;
                    interleavedMap[file.Name] = file;
                }
            }

            if (report.Disc.StreamClipFiles != null)
            {
                foreach (var clipFileData in report.Disc.StreamClipFiles)
                {
                    if (string.IsNullOrWhiteSpace(clipFileData.Name))
                    {
                        continue;
                    }

                    var clipFile = new TSStreamClipFile(clipFileData.Name)
                    {
                        FileType = clipFileData.FileType,
                        IsValid = true
                    };

                    clipFile.Streams.Clear();
                    if (clipFileData.Streams != null)
                    {
                        foreach (var streamData in clipFileData.Streams)
                        {
                            var stream = CreateStream(streamData);
                            clipFile.Streams[stream.PID] = stream;
                        }
                        LinkAudioCoreStreams(clipFile.Streams.Values, clipFileData.Streams);
                    }

                    bdrom.StreamClipFiles[clipFile.Name] = clipFile;
                }
            }

            if (report.Disc.StreamFiles != null)
            {
                foreach (var streamFileData in report.Disc.StreamFiles)
                {
                    if (string.IsNullOrWhiteSpace(streamFileData.Name))
                    {
                        continue;
                    }

                    var streamFile = new TSStreamFile(streamFileData.Name, streamFileData.Size, streamFileData.Length);
                    if (!string.IsNullOrWhiteSpace(streamFileData.InterleavedFileName) &&
                        interleavedMap.TryGetValue(streamFileData.InterleavedFileName.ToUpperInvariant(), out var interleaved))
                    {
                        streamFile.InterleavedFile = interleaved;
                    }

                    streamFile.Streams.Clear();
                    if (streamFileData.Streams != null)
                    {
                        foreach (var streamData in streamFileData.Streams)
                        {
                            var stream = CreateStream(streamData);
                            streamFile.Streams[stream.PID] = stream;
                        }
                        LinkAudioCoreStreams(streamFile.Streams.Values, streamFileData.Streams);
                    }

                    streamFile.StreamDiagnostics.Clear();
                    if (streamFileData.Diagnostics != null)
                    {
                        foreach (var diagCollection in streamFileData.Diagnostics)
                        {
                            if (diagCollection?.Diagnostics == null)
                            {
                                continue;
                            }

                            var diagList = new List<TSStreamDiagnostics>();
                            foreach (var diag in diagCollection.Diagnostics)
                            {
                                if (diag == null)
                                {
                                    continue;
                                }

                                diagList.Add(new TSStreamDiagnostics
                                {
                                    Marker = diag.Marker,
                                    Interval = diag.Interval,
                                    Bytes = diag.Bytes,
                                    Packets = diag.Packets,
                                    Tag = diag.Tag
                                });
                            }
                            streamFile.StreamDiagnostics[diagCollection.PID] = diagList;
                        }
                    }

                    bdrom.StreamFiles[streamFile.Name] = streamFile;
                }
            }

            if (report.Disc.Playlists != null)
            {
                foreach (var playlistData in report.Disc.Playlists)
                {
                    if (string.IsNullOrWhiteSpace(playlistData.Name))
                    {
                        continue;
                    }

                    var playlist = new TSPlaylistFile(bdrom, playlistData.Name)
                    {
                        HasHiddenTracks = playlistData.HasHiddenTracks,
                        HasLoops = playlistData.HasLoops,
                        IsCustom = playlistData.IsCustom,
                        MVCBaseViewR = playlistData.MVCBaseViewR,
                        AngleCount = playlistData.AngleCount,
                        IsInitialized = true
                    };

                    playlist.Chapters.Clear();
                    if (playlistData.Chapters != null)
                    {
                        playlist.Chapters.AddRange(playlistData.Chapters);
                    }

                    var clipLookup = new Dictionary<string, TSStreamClip>(StringComparer.OrdinalIgnoreCase);
                    playlist.StreamClips.Clear();
                    if (playlistData.StreamClips != null)
                    {
                        foreach (var clipData in playlistData.StreamClips)
                        {
                            var streamFile = ResolveStreamFile(bdrom, clipData.StreamFileName);
                            var clipFile = ResolveStreamClipFile(bdrom, clipData.StreamClipFileName);
                            var clip = new TSStreamClip(streamFile, clipFile)
                            {
                                Name = clipData.Name,
                                AngleIndex = clipData.AngleIndex,
                                TimeIn = clipData.TimeIn,
                                TimeOut = clipData.TimeOut,
                                RelativeTimeIn = clipData.RelativeTimeIn,
                                RelativeTimeOut = clipData.RelativeTimeOut,
                                Length = clipData.Length,
                                RelativeLength = clipData.RelativeLength,
                                FileSize = clipData.FileSize,
                                InterleavedFileSize = clipData.InterleavedFileSize,
                                PayloadBytes = clipData.PayloadBytes,
                                PacketCount = clipData.PacketCount,
                                PacketSeconds = clipData.PacketSeconds
                            };

                            if (clipData.Chapters != null)
                            {
                                clip.Chapters.AddRange(clipData.Chapters);
                            }

                            playlist.StreamClips.Add(clip);
                            if (!string.IsNullOrWhiteSpace(clip.Name))
                            {
                                clipLookup[clip.Name] = clip;
                            }
                        }
                    }

                    playlist.AngleClips.Clear();
                    if (playlistData.AngleClips != null)
                    {
                        foreach (var angleClipData in playlistData.AngleClips)
                        {
                            var angleClips = new Dictionary<double, TSStreamClip>();
                            if (angleClipData?.Clips != null)
                            {
                                foreach (var entry in angleClipData.Clips)
                                {
                                    if (clipLookup.TryGetValue(entry.ClipName ?? string.Empty, out var clip))
                                    {
                                        angleClips[entry.RelativeStart] = clip;
                                    }
                                }
                            }
                            playlist.AngleClips.Add(angleClips);
                        }
                    }

                    playlist.AngleStreams.Clear();
                    if (playlistData.AngleStreams != null)
                    {
                        foreach (var angleStreamData in playlistData.AngleStreams)
                        {
                            var angleStreams = new Dictionary<ushort, TSStream>();
                            if (angleStreamData?.Streams != null)
                            {
                                foreach (var streamData in angleStreamData.Streams)
                                {
                                    var stream = CreateStream(streamData);
                                    angleStreams[stream.PID] = stream;
                                }
                                LinkAudioCoreStreams(angleStreams.Values, angleStreamData.Streams);
                            }
                            playlist.AngleStreams.Add(angleStreams);
                        }
                    }

                    playlist.Streams.Clear();
                    if (playlistData.Streams != null)
                    {
                        foreach (var streamData in playlistData.Streams)
                        {
                            var stream = CreateStream(streamData);
                            playlist.Streams[stream.PID] = stream;
                        }
                        LinkAudioCoreStreams(playlist.Streams.Values, playlistData.Streams);
                    }

                    playlist.PlaylistStreams.Clear();
                    if (playlistData.PlaylistStreams != null)
                    {
                        foreach (var streamData in playlistData.PlaylistStreams)
                        {
                            var stream = CreateStream(streamData);
                            playlist.PlaylistStreams[stream.PID] = stream;
                        }
                        LinkAudioCoreStreams(playlist.PlaylistStreams.Values, playlistData.PlaylistStreams);
                    }

                    playlist.SortedStreams = CreateStreamList(playlistData.SortedStreams);
                    playlist.VideoStreams = CreateStreamList<TSVideoStream>(playlistData.VideoStreams);
                    playlist.AudioStreams = CreateStreamList<TSAudioStream>(playlistData.AudioStreams);
                    playlist.TextStreams = CreateStreamList<TSTextStream>(playlistData.TextStreams);
                    playlist.GraphicsStreams = CreateStreamList<TSGraphicsStream>(playlistData.GraphicsStreams);

                    bdrom.PlaylistFiles[playlist.Name] = playlist;
                }
            }

            return bdrom;
        }

        public static ScanBDROMResult CreateScanResult(ScanResultData data)
        {
            var result = new ScanBDROMResult();
            if (data == null)
            {
                result.ScanException = null;
                return result;
            }

            if (!string.IsNullOrWhiteSpace(data.ScanExceptionMessage))
            {
                var message = data.ScanExceptionMessage;
                if (!string.IsNullOrWhiteSpace(data.ScanExceptionStack))
                {
                    message += Environment.NewLine + data.ScanExceptionStack;
                }
                result.ScanException = new Exception(message);
            }
            else
            {
                result.ScanException = null;
            }

            result.FileExceptions.Clear();
            if (data.FileExceptions != null)
            {
                foreach (var fileException in data.FileExceptions)
                {
                    var message = fileException.Message ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(fileException.StackTrace))
                    {
                        message += Environment.NewLine + fileException.StackTrace;
                    }
                    result.FileExceptions[fileException.FileName ?? string.Empty] = new Exception(message);
                }
            }

            return result;
        }

        private static string GetSourcePath(BDROM bdrom)
        {
            if (!string.IsNullOrWhiteSpace(bdrom.ReportPath))
            {
                return bdrom.ReportPath;
            }

            if (bdrom.IsImage && bdrom.IoStream != null)
            {
                return bdrom.IoStream.Name;
            }

            if (bdrom.DirectoryBDMV != null)
            {
                return bdrom.DirectoryBDMV.FullName;
            }

            if (bdrom.DirectoryRoot != null)
            {
                return bdrom.DirectoryRoot.FullName;
            }

            return bdrom.VolumeLabel;
        }

        private static BDROMData BuildDiscData(BDROM bdrom, List<TSPlaylistFile> playlists)
        {
            var disc = new BDROMData
            {
                VolumeLabel = bdrom.VolumeLabel,
                DiscTitle = bdrom.DiscTitle,
                Size = bdrom.Size,
                IsBDPlus = bdrom.IsBDPlus,
                IsBDJava = bdrom.IsBDJava,
                IsDBOX = bdrom.IsDBOX,
                IsPSP = bdrom.IsPSP,
                Is3D = bdrom.Is3D,
                Is50Hz = bdrom.Is50Hz,
                IsUHD = bdrom.IsUHD,
                StreamFiles = new List<StreamFileData>(),
                StreamClipFiles = new List<StreamClipFileData>(),
                InterleavedFiles = new List<InterleavedFileData>(),
                Playlists = new List<PlaylistData>()
            };

            var exportedInterleaved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var interleaved in bdrom.InterleavedFiles.Values)
            {
                if (interleaved == null || string.IsNullOrWhiteSpace(interleaved.Name) || !exportedInterleaved.Add(interleaved.Name))
                {
                    continue;
                }
                disc.InterleavedFiles.Add(new InterleavedFileData { Name = interleaved.Name });
            }

            foreach (var streamFile in bdrom.StreamFiles.Values)
            {
                if (streamFile == null)
                {
                    continue;
                }

                var streamFileData = new StreamFileData
                {
                    Name = streamFile.Name,
                    Size = streamFile.Size,
                    Length = streamFile.Length,
                    InterleavedFileName = streamFile.InterleavedFile?.Name,
                    Streams = ConvertStreams(streamFile.Streams?.Values),
                    Diagnostics = ConvertDiagnostics(streamFile.StreamDiagnostics)
                };

                disc.StreamFiles.Add(streamFileData);
            }

            foreach (var clipFile in bdrom.StreamClipFiles.Values)
            {
                if (clipFile == null)
                {
                    continue;
                }

                var clipData = new StreamClipFileData
                {
                    Name = clipFile.Name,
                    FileType = clipFile.FileType,
                    Streams = ConvertStreams(clipFile.Streams?.Values)
                };

                disc.StreamClipFiles.Add(clipData);
            }

            foreach (var playlist in playlists)
            {
                if (playlist == null)
                {
                    continue;
                }

                disc.Playlists.Add(ConvertPlaylist(playlist));
            }

            return disc;
        }

        private static List<StreamDiagnosticsCollectionData> ConvertDiagnostics(Dictionary<ushort, List<TSStreamDiagnostics>> diagnostics)
        {
            if (diagnostics == null || diagnostics.Count == 0)
            {
                return new List<StreamDiagnosticsCollectionData>();
            }

            var list = new List<StreamDiagnosticsCollectionData>();
            foreach (var kvp in diagnostics)
            {
                var collection = new StreamDiagnosticsCollectionData
                {
                    PID = kvp.Key,
                    Diagnostics = new List<StreamDiagnosticsData>()
                };

                if (kvp.Value != null)
                {
                    foreach (var diag in kvp.Value)
                    {
                        if (diag == null)
                        {
                            continue;
                        }

                        collection.Diagnostics.Add(new StreamDiagnosticsData
                        {
                            Marker = diag.Marker,
                            Interval = diag.Interval,
                            Bytes = diag.Bytes,
                            Packets = diag.Packets,
                            Tag = diag.Tag
                        });
                    }
                }

                list.Add(collection);
            }

            return list;
        }

        private static PlaylistData ConvertPlaylist(TSPlaylistFile playlist)
        {
            var data = new PlaylistData
            {
                Name = playlist.Name,
                HasHiddenTracks = playlist.HasHiddenTracks,
                HasLoops = playlist.HasLoops,
                IsCustom = playlist.IsCustom,
                MVCBaseViewR = playlist.MVCBaseViewR,
                AngleCount = playlist.AngleCount,
                Chapters = playlist.Chapters != null ? new List<double>(playlist.Chapters) : new List<double>(),
                StreamClips = ConvertStreamClips(playlist.StreamClips),
                AngleClips = ConvertAngleClips(playlist.AngleClips),
                AngleStreams = ConvertAngleStreams(playlist.AngleStreams),
                Streams = ConvertStreams(playlist.Streams?.Values),
                PlaylistStreams = ConvertStreams(playlist.PlaylistStreams?.Values),
                SortedStreams = ConvertStreams(playlist.SortedStreams),
                VideoStreams = ConvertStreams(playlist.VideoStreams),
                AudioStreams = ConvertStreams(playlist.AudioStreams),
                TextStreams = ConvertStreams(playlist.TextStreams),
                GraphicsStreams = ConvertStreams(playlist.GraphicsStreams)
            };

            return data;
        }

        private static List<StreamClipData> ConvertStreamClips(IEnumerable<TSStreamClip> clips)
        {
            var list = new List<StreamClipData>();
            if (clips == null)
            {
                return list;
            }

            foreach (var clip in clips)
            {
                if (clip == null)
                {
                    continue;
                }

                var clipData = new StreamClipData
                {
                    Name = clip.Name,
                    StreamFileName = clip.StreamFile?.Name,
                    StreamClipFileName = clip.StreamClipFile?.Name,
                    AngleIndex = clip.AngleIndex,
                    TimeIn = clip.TimeIn,
                    TimeOut = clip.TimeOut,
                    RelativeTimeIn = clip.RelativeTimeIn,
                    RelativeTimeOut = clip.RelativeTimeOut,
                    Length = clip.Length,
                    RelativeLength = clip.RelativeLength,
                    FileSize = clip.FileSize,
                    InterleavedFileSize = clip.InterleavedFileSize,
                    PayloadBytes = clip.PayloadBytes,
                    PacketCount = clip.PacketCount,
                    PacketSeconds = clip.PacketSeconds,
                    Chapters = clip.Chapters != null ? new List<double>(clip.Chapters) : new List<double>()
                };

                list.Add(clipData);
            }

            return list;
        }

        private static List<AngleClipData> ConvertAngleClips(IEnumerable<Dictionary<double, TSStreamClip>> angleClips)
        {
            var list = new List<AngleClipData>();
            if (angleClips == null)
            {
                return list;
            }

            var index = 0;
            foreach (var dictionary in angleClips)
            {
                var angleData = new AngleClipData
                {
                    AngleIndex = index++,
                    Clips = new List<AngleClipEntryData>()
                };

                if (dictionary != null)
                {
                    foreach (var kvp in dictionary)
                    {
                        angleData.Clips.Add(new AngleClipEntryData
                        {
                            RelativeStart = kvp.Key,
                            ClipName = kvp.Value?.Name
                        });
                    }
                }

                list.Add(angleData);
            }

            return list;
        }

        private static List<AngleStreamData> ConvertAngleStreams(IEnumerable<Dictionary<ushort, TSStream>> angleStreams)
        {
            var list = new List<AngleStreamData>();
            if (angleStreams == null)
            {
                return list;
            }

            var index = 0;
            foreach (var dictionary in angleStreams)
            {
                var angleData = new AngleStreamData
                {
                    AngleIndex = index++,
                    Streams = ConvertStreams(dictionary?.Values)
                };
                list.Add(angleData);
            }

            return list;
        }

        private static List<StreamData> ConvertStreams(IEnumerable<TSStream> streams)
        {
            var list = new List<StreamData>();
            if (streams == null)
            {
                return list;
            }

            foreach (var stream in streams)
            {
                if (stream == null)
                {
                    continue;
                }
                list.Add(CreateStreamData(stream));
            }

            return list;
        }

        private static StreamData CreateStreamData(TSStream stream)
        {
            StreamData data;
            if (stream is TSVideoStream video)
            {
                data = new VideoStreamData
                {
                    VideoFormat = video.VideoFormat,
                    FrameRate = video.FrameRate,
                    AspectRatio = video.AspectRatio,
                    Width = video.Width,
                    Height = video.Height,
                    IsInterlaced = video.IsInterlaced,
                    FrameRateEnumerator = video.FrameRateEnumerator,
                    FrameRateDenominator = video.FrameRateDenominator,
                    EncodingProfile = video.EncodingProfile
                };
            }
            else if (stream is TSAudioStream audio)
            {
                data = new AudioStreamData
                {
                    SampleRate = audio.SampleRate,
                    ChannelCount = audio.ChannelCount,
                    BitDepth = audio.BitDepth,
                    LFE = audio.LFE,
                    DialNorm = audio.DialNorm,
                    HasExtensions = audio.HasExtensions,
                    AudioMode = audio.AudioMode,
                    ChannelLayout = audio.ChannelLayout,
                    ExtendedData = audio.ExtendedData as string,
                    CoreStreamPID = audio.CoreStream?.PID
                };
            }
            else if (stream is TSGraphicsStream graphics)
            {
                data = new GraphicsStreamData
                {
                    Width = graphics.Width,
                    Height = graphics.Height,
                    Captions = graphics.Captions,
                    ForcedCaptions = graphics.ForcedCaptions
                };
            }
            else if (stream is TSTextStream)
            {
                data = new TextStreamData();
            }
            else
            {
                data = new StreamData();
            }

            data.PID = stream.PID;
            data.StreamType = stream.StreamType;
            data.IsVBR = stream.IsVBR;
            data.BitRate = stream.BitRate;
            data.ActiveBitRate = stream.ActiveBitRate;
            data.IsInitialized = stream.IsInitialized;
            data.LanguageCode = stream.LanguageCode;
            data.LanguageName = stream.LanguageName;
            data.IsHidden = stream.IsHidden;
            data.PayloadBytes = stream.PayloadBytes;
            data.PacketCount = stream.PacketCount;
            data.PacketSeconds = stream.PacketSeconds;
            data.AngleIndex = stream.AngleIndex;
            data.BaseView = stream.BaseView;

            if (!(stream is TSAudioStream) && stream is TSVideoStream videoStream)
            {
                data.ExtendedData = videoStream.ExtendedData as string;
            }

            return data;
        }

        private static TSStream CreateStream(StreamData data)
        {
            if (data == null)
            {
                return null;
            }

            TSStream stream;
            switch (data.StreamType)
            {
                case TSStreamType.MPEG1_VIDEO:
                case TSStreamType.MPEG2_VIDEO:
                case TSStreamType.AVC_VIDEO:
                case TSStreamType.MVC_VIDEO:
                case TSStreamType.VC1_VIDEO:
                case TSStreamType.HEVC_VIDEO:
                {
                    var video = new TSVideoStream
                    {
                        VideoFormat = data.VideoFormat,
                        FrameRate = data.FrameRate,
                        AspectRatio = data.AspectRatio,
                        EncodingProfile = data.EncodingProfile
                    };
                    video.Width = data.Width;
                    video.Height = data.Height;
                    video.IsInterlaced = data.IsInterlaced;
                    video.FrameRateEnumerator = data.FrameRateEnumerator;
                    video.FrameRateDenominator = data.FrameRateDenominator;
                    stream = video;
                    break;
                }

                case TSStreamType.MPEG1_AUDIO:
                case TSStreamType.MPEG2_AUDIO:
                case TSStreamType.MPEG2_AAC_AUDIO:
                case TSStreamType.MPEG4_AAC_AUDIO:
                case TSStreamType.LPCM_AUDIO:
                case TSStreamType.AC3_AUDIO:
                case TSStreamType.AC3_PLUS_AUDIO:
                case TSStreamType.AC3_PLUS_SECONDARY_AUDIO:
                case TSStreamType.AC3_TRUE_HD_AUDIO:
                case TSStreamType.DTS_AUDIO:
                case TSStreamType.DTS_HD_AUDIO:
                case TSStreamType.DTS_HD_SECONDARY_AUDIO:
                case TSStreamType.DTS_HD_MASTER_AUDIO:
                {
                    var audio = new TSAudioStream
                    {
                        SampleRate = data.SampleRate,
                        ChannelCount = data.ChannelCount,
                        BitDepth = data.BitDepth,
                        LFE = data.LFE,
                        DialNorm = data.DialNorm,
                        HasExtensions = data.HasExtensions,
                        AudioMode = data.AudioMode,
                        ChannelLayout = data.ChannelLayout,
                        ExtendedData = data.ExtendedData
                    };
                    stream = audio;
                    break;
                }

                case TSStreamType.PRESENTATION_GRAPHICS:
                case TSStreamType.INTERACTIVE_GRAPHICS:
                {
                    var graphics = new TSGraphicsStream
                    {
                        Width = data.Width,
                        Height = data.Height,
                        Captions = data.Captions,
                        ForcedCaptions = data.ForcedCaptions
                    };
                    stream = graphics;
                    break;
                }

                case TSStreamType.SUBTITLE:
                {
                    stream = new TSTextStream();
                    break;
                }

                default:
                {
                    stream = new TSTextStream();
                    break;
                }
            }

            stream.PID = data.PID;
            stream.StreamType = data.StreamType;
            stream.IsVBR = data.IsVBR;
            stream.BitRate = data.BitRate;
            stream.ActiveBitRate = data.ActiveBitRate;
            stream.IsInitialized = data.IsInitialized;
            stream.LanguageCode = data.LanguageCode;
            stream.LanguageName = data.LanguageName;
            stream.IsHidden = data.IsHidden;
            stream.PayloadBytes = data.PayloadBytes;
            stream.PacketCount = data.PacketCount;
            stream.PacketSeconds = data.PacketSeconds;
            stream.AngleIndex = data.AngleIndex;
            stream.BaseView = data.BaseView;

            return stream;
        }

        private static void LinkAudioCoreStreams(IEnumerable<TSStream> streams, IEnumerable<StreamData> data)
        {
            if (streams == null || data == null)
            {
                return;
            }

            var audioMap = new Dictionary<ushort, TSAudioStream>();
            foreach (var stream in streams)
            {
                if (stream is TSAudioStream audio)
                {
                    audioMap[stream.PID] = audio;
                }
            }

            foreach (var streamData in data)
            {
                if (streamData is AudioStreamData audioData && audioData.CoreStreamPID.HasValue)
                {
                    if (audioMap.TryGetValue(streamData.PID, out var audio) &&
                        audioMap.TryGetValue(audioData.CoreStreamPID.Value, out var core))
                    {
                        audio.CoreStream = core;
                    }
                }
            }
        }

        private static List<TStream> CreateStreamList<TStream>(List<StreamData> data) where TStream : TSStream
        {
            var list = new List<TStream>();
            if (data == null)
            {
                return list;
            }

            foreach (var streamData in data)
            {
                var stream = CreateStream(streamData);
                if (stream is TStream typed)
                {
                    list.Add(typed);
                }
            }

            LinkAudioCoreStreams(list.Cast<TSStream>(), data);
            return list;
        }

        private static List<TSStream> CreateStreamList(List<StreamData> data)
        {
            var list = new List<TSStream>();
            if (data == null)
            {
                return list;
            }

            foreach (var streamData in data)
            {
                var stream = CreateStream(streamData);
                if (stream != null)
                {
                    list.Add(stream);
                }
            }

            LinkAudioCoreStreams(list, data);
            return list;
        }

        private static TSStreamFile ResolveStreamFile(BDROM bdrom, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var key = name.ToUpperInvariant();
            return bdrom.StreamFiles.ContainsKey(key) ? bdrom.StreamFiles[key] : null;
        }

        private static TSStreamClipFile ResolveStreamClipFile(BDROM bdrom, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var key = name.ToUpperInvariant();
            return bdrom.StreamClipFiles.ContainsKey(key) ? bdrom.StreamClipFiles[key] : null;
        }

        private static ScanResultData ConvertScanResult(ScanBDROMResult result)
        {
            if (result == null)
            {
                return null;
            }

            var data = new ScanResultData
            {
                ScanExceptionMessage = result.ScanException?.Message,
                ScanExceptionStack = result.ScanException?.StackTrace,
                FileExceptions = new List<ScanFileExceptionData>()
            };

            if (result.FileExceptions != null)
            {
                foreach (var kvp in result.FileExceptions)
                {
                    data.FileExceptions.Add(new ScanFileExceptionData
                    {
                        FileName = kvp.Key,
                        Message = kvp.Value?.Message,
                        StackTrace = kvp.Value?.StackTrace
                    });
                }
            }

            return data;
        }
    }
}

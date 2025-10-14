using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;

namespace BDInfo
{
    [DataContract]
    public sealed class SavedReportData
    {
        [DataMember]
        public string VolumeLabel { get; set; }

        [DataMember]
        public List<SavedPlaylistData> Playlists { get; set; } = new List<SavedPlaylistData>();
    }

    [DataContract]
    public sealed class SavedPlaylistData
    {
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public int AngleCount { get; set; }

        [DataMember]
        public double TotalLength { get; set; }

        [DataMember]
        public List<SavedVideoStreamData> VideoStreams { get; set; } = new List<SavedVideoStreamData>();

        [DataMember]
        public List<SavedStreamClipData> Clips { get; set; } = new List<SavedStreamClipData>();
    }

    [DataContract]
    public sealed class SavedVideoStreamData
    {
        [DataMember]
        public ushort PID { get; set; }

        [DataMember]
        public string DisplayName { get; set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    [DataContract]
    public sealed class SavedStreamClipData
    {
        [DataMember]
        public int AngleIndex { get; set; }

        [DataMember]
        public double TimeIn { get; set; }

        [DataMember]
        public double RelativeTimeIn { get; set; }

        [DataMember]
        public double RelativeTimeOut { get; set; }

        [DataMember]
        public double Length { get; set; }

        [DataMember]
        public Dictionary<ushort, List<SavedStreamDiagnosticsData>> Diagnostics { get; set; } = new Dictionary<ushort, List<SavedStreamDiagnosticsData>>();
    }

    [DataContract]
    public sealed class SavedStreamDiagnosticsData
    {
        [DataMember]
        public double Marker { get; set; }

        [DataMember]
        public double Interval { get; set; }

        [DataMember]
        public ulong Bytes { get; set; }

        [DataMember]
        public ulong Packets { get; set; }

        [DataMember]
        public string Tag { get; set; }
    }

    internal static class ReportPersistence
    {
        private const string ReportExtension = ".bdinfo";

        internal const string EmbeddedMetadataBeginMarker = "==== BDINFO METADATA BEGIN ====";
        internal const string EmbeddedMetadataEndMarker = "==== BDINFO METADATA END ====";

        public static void Save(string reportPath, BDROM bdrom, IEnumerable<TSPlaylistFile> playlists)
        {
            if (string.IsNullOrWhiteSpace(reportPath))
            {
                throw new ArgumentException("reportPath");
            }

            if (bdrom == null)
            {
                throw new ArgumentNullException(nameof(bdrom));
            }

            if (playlists == null)
            {
                throw new ArgumentNullException(nameof(playlists));
            }

            SavedReportData data = Capture(bdrom, playlists);
            Save(reportPath, data);
        }

        public static void Save(string reportPath, SavedReportData data)
        {
            if (string.IsNullOrWhiteSpace(reportPath))
            {
                throw new ArgumentException("reportPath");
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            string metadataPath = GetMetadataPath(reportPath);

            try
            {
                using (FileStream stream = File.Create(metadataPath))
                {
                    DataContractSerializer serializer = new DataContractSerializer(typeof(SavedReportData));
                    serializer.WriteObject(stream, data);
                }
            }
            catch
            {
                try
                {
                    if (File.Exists(metadataPath))
                    {
                        File.Delete(metadataPath);
                    }
                }
                catch
                {
                }

                throw;
            }
        }

        public static SavedReportData Load(string reportPath)
        {
            if (string.IsNullOrWhiteSpace(reportPath))
            {
                throw new ArgumentException("reportPath");
            }

            string metadataPath = GetMetadataPath(reportPath);
            if (!File.Exists(metadataPath))
            {
                return null;
            }

            try
            {
                using (FileStream stream = File.OpenRead(metadataPath))
                {
                    DataContractSerializer serializer = new DataContractSerializer(typeof(SavedReportData));
                    return (SavedReportData)serializer.ReadObject(stream);
                }
            }
            catch
            {
                return null;
            }
        }

        internal static SavedReportData Capture(BDROM bdrom, IEnumerable<TSPlaylistFile> playlists)
        {
            SavedReportData data = new SavedReportData
            {
                VolumeLabel = bdrom.VolumeLabel
            };

            foreach (TSPlaylistFile playlist in playlists)
            {
                SavedPlaylistData playlistData = new SavedPlaylistData
                {
                    Name = playlist.Name,
                    AngleCount = playlist.AngleCount,
                    TotalLength = playlist.TotalLength
                };

                foreach (TSVideoStream videoStream in playlist.VideoStreams)
                {
                    playlistData.VideoStreams.Add(new SavedVideoStreamData
                    {
                        PID = videoStream.PID,
                        DisplayName = videoStream.ToString()
                    });
                }

                foreach (TSStreamClip clip in playlist.StreamClips)
                {
                    SavedStreamClipData clipData = new SavedStreamClipData
                    {
                        AngleIndex = clip.AngleIndex,
                        TimeIn = clip.TimeIn,
                        RelativeTimeIn = clip.RelativeTimeIn,
                        RelativeTimeOut = clip.RelativeTimeOut,
                        Length = clip.Length
                    };

                    if (clip.StreamFile != null && clip.StreamFile.StreamDiagnostics != null)
                    {
                        foreach (TSVideoStream videoStream in playlist.VideoStreams)
                        {
                            if (!clip.StreamFile.StreamDiagnostics.ContainsKey(videoStream.PID))
                            {
                                continue;
                            }

                            List<TSStreamDiagnostics> diagnostics = clip.StreamFile.StreamDiagnostics[videoStream.PID];
                            if (diagnostics == null || diagnostics.Count == 0)
                            {
                                continue;
                            }

                            clipData.Diagnostics[videoStream.PID] = diagnostics
                                .Select(diag => new SavedStreamDiagnosticsData
                                {
                                    Marker = diag.Marker,
                                    Interval = diag.Interval,
                                    Bytes = diag.Bytes,
                                    Packets = diag.Packets,
                                    Tag = diag.Tag
                                })
                                .ToList();
                        }
                    }

                    playlistData.Clips.Add(clipData);
                }

                data.Playlists.Add(playlistData);
            }

            return data;
        }

        internal static string CreateEmbeddedMetadataBlock(SavedReportData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            using (MemoryStream stream = new MemoryStream())
            {
                DataContractSerializer serializer = new DataContractSerializer(typeof(SavedReportData));
                serializer.WriteObject(stream, data);
                string base64 = Convert.ToBase64String(stream.ToArray());

                StringBuilder builder = new StringBuilder();
                builder.AppendLine();
                builder.AppendLine(EmbeddedMetadataBeginMarker);
                builder.AppendLine(base64);
                builder.AppendLine(EmbeddedMetadataEndMarker);
                return builder.ToString();
            }
        }

        internal static bool TryExtractEmbeddedMetadata(
            string reportText,
            out string sanitizedText,
            out SavedReportData data)
        {
            sanitizedText = reportText;
            data = null;

            if (string.IsNullOrEmpty(reportText))
            {
                return false;
            }

            int footerIndex = reportText.LastIndexOf(EmbeddedMetadataEndMarker, StringComparison.Ordinal);
            if (footerIndex < 0)
            {
                return false;
            }

            int headerIndex = reportText.LastIndexOf(EmbeddedMetadataBeginMarker, footerIndex, StringComparison.Ordinal);
            if (headerIndex < 0)
            {
                return false;
            }

            int metadataStart = headerIndex + EmbeddedMetadataBeginMarker.Length;
            string metadataPayload = reportText.Substring(metadataStart, footerIndex - metadataStart);
            string base64 = new string(metadataPayload.Where(c => !char.IsWhiteSpace(c)).ToArray());

            if (base64.Length == 0)
            {
                return false;
            }

            try
            {
                byte[] buffer = Convert.FromBase64String(base64);
                using (MemoryStream stream = new MemoryStream(buffer))
                {
                    DataContractSerializer serializer = new DataContractSerializer(typeof(SavedReportData));
                    data = (SavedReportData)serializer.ReadObject(stream);
                }
            }
            catch
            {
                data = null;
                return false;
            }

            int blockEnd = footerIndex + EmbeddedMetadataEndMarker.Length;
            string beforeBlock = reportText.Substring(0, headerIndex);
            string afterBlock = blockEnd < reportText.Length
                ? reportText.Substring(blockEnd)
                : string.Empty;

            sanitizedText = beforeBlock.TrimEnd('\r', '\n');
            if (!string.IsNullOrEmpty(afterBlock))
            {
                if (sanitizedText.Length > 0)
                {
                    sanitizedText += Environment.NewLine;
                }
                sanitizedText += afterBlock.TrimStart('\r', '\n');
            }

            return true;
        }

        private static string GetMetadataPath(string reportPath)
        {
            string directory = Path.GetDirectoryName(reportPath) ?? Environment.CurrentDirectory;
            string fileName = Path.GetFileNameWithoutExtension(reportPath);
            string safeFileName = ToolBox.GetSafeFileName(fileName);
            return Path.Combine(directory, string.Format(CultureInfo.InvariantCulture, "{0}{1}", safeFileName, ReportExtension));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using BDInfo;

namespace BDInfo.Reporting
{
    [DataContract]
    [KnownType(typeof(VideoStreamData))]
    [KnownType(typeof(AudioStreamData))]
    [KnownType(typeof(TextStreamData))]
    [KnownType(typeof(GraphicsStreamData))]
    public class StreamData
    {
        [DataMember] public ushort PID;
        [DataMember] public TSStreamType StreamType;
        [DataMember] public bool IsVBR;
        [DataMember] public long BitRate;
        [DataMember] public long ActiveBitRate;
        [DataMember] public bool IsInitialized;
        [DataMember] public string LanguageCode;
        [DataMember] public string LanguageName;
        [DataMember] public bool IsHidden;
        [DataMember] public ulong PayloadBytes;
        [DataMember] public ulong PacketCount;
        [DataMember] public double PacketSeconds;
        [DataMember] public int AngleIndex;
        [DataMember] public bool? BaseView;
        [DataMember] public string ExtendedData;
        [DataMember] public bool HasExtensions;
        [DataMember] public TSAudioMode AudioMode;
        [DataMember] public TSChannelLayout ChannelLayout;
        [DataMember] public ushort? CoreStreamPID;
        [DataMember] public int SampleRate;
        [DataMember] public int ChannelCount;
        [DataMember] public int BitDepth;
        [DataMember] public int LFE;
        [DataMember] public int DialNorm;
        [DataMember] public int Width;
        [DataMember] public int Height;
        [DataMember] public bool IsInterlaced;
        [DataMember] public int FrameRateEnumerator;
        [DataMember] public int FrameRateDenominator;
        [IgnoreDataMember]
        public TSAspectRatio AspectRatio { get; set; }

        [DataMember(Name = "AspectRatio")]
        public string AspectRatioValue
        {
            get => ((int)AspectRatio).ToString(CultureInfo.InvariantCulture);
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    AspectRatio = TSAspectRatio.Unknown;
                    return;
                }

                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericValue))
                {
                    AspectRatio = (TSAspectRatio)numericValue;
                }
                else if (Enum.TryParse(value, true, out TSAspectRatio parsedValue))
                {
                    AspectRatio = parsedValue;
                }
                else
                {
                    AspectRatio = TSAspectRatio.Unknown;
                }
            }
        }
        [DataMember] public TSVideoFormat VideoFormat;
        [DataMember] public TSFrameRate FrameRate;
        [DataMember] public string EncodingProfile;
        [DataMember] public int Captions;
        [DataMember] public int ForcedCaptions;
    }

    [DataContract]
    public class VideoStreamData : StreamData
    {
        [DataMember(EmitDefaultValue = false)] public List<string> ExtendedFormatInfo;
        [DataMember(EmitDefaultValue = false)] public string MasteringDisplayColorPrimaries;
        [DataMember(EmitDefaultValue = false)] public string MasteringDisplayLuminance;
        [DataMember(EmitDefaultValue = false)] public uint MaximumContentLightLevel;
        [DataMember(EmitDefaultValue = false)] public uint MaximumFrameAverageLightLevel;
        [DataMember(EmitDefaultValue = false)] public bool LightLevelAvailable;
        [DataMember(EmitDefaultValue = false)] public byte PreferredTransferCharacteristics;
        [DataMember(EmitDefaultValue = false)] public bool IsHdr10Plus;
    }

    [DataContract]
    public class AudioStreamData : StreamData
    {
        [DataMember(EmitDefaultValue = false)] public AudioStreamData CoreStream;
    }

    [DataContract]
    public class TextStreamData : StreamData
    {
    }

    [DataContract]
    public class GraphicsStreamData : StreamData
    {
    }

    [DataContract]
    public class StreamDiagnosticsData
    {
        [DataMember] public double Marker;
        [DataMember] public double Interval;
        [DataMember] public ulong Bytes;
        [DataMember] public ulong Packets;
        [DataMember] public string Tag;
    }

    [DataContract]
    public class StreamDiagnosticsCollectionData
    {
        [DataMember] public ushort PID;
        [DataMember] public List<StreamDiagnosticsData> Diagnostics;
    }

    [DataContract]
    public class StreamFileData
    {
        [DataMember] public string Name;
        [DataMember] public long Size;
        [DataMember] public double Length;
        [DataMember] public string InterleavedFileName;
        [DataMember] public List<StreamData> Streams;
        [DataMember] public List<StreamDiagnosticsCollectionData> Diagnostics;
    }

    [DataContract]
    public class StreamClipFileData
    {
        [DataMember] public string Name;
        [DataMember] public List<StreamData> Streams;
        [DataMember] public string FileType;
    }

    [DataContract]
    public class StreamClipData
    {
        [DataMember] public string Name;
        [DataMember] public string StreamFileName;
        [DataMember] public string StreamClipFileName;
        [DataMember] public int AngleIndex;
        [DataMember] public double TimeIn;
        [DataMember] public double TimeOut;
        [DataMember] public double RelativeTimeIn;
        [DataMember] public double RelativeTimeOut;
        [DataMember] public double Length;
        [DataMember] public double RelativeLength;
        [DataMember] public ulong FileSize;
        [DataMember] public ulong InterleavedFileSize;
        [DataMember] public ulong PayloadBytes;
        [DataMember] public ulong PacketCount;
        [DataMember] public double PacketSeconds;
        [DataMember] public List<double> Chapters;
    }

    [DataContract]
    public class AngleClipEntryData
    {
        [DataMember] public double RelativeStart;
        [DataMember] public string ClipName;
    }

    [DataContract]
    public class AngleClipData
    {
        [DataMember] public int AngleIndex;
        [DataMember] public List<AngleClipEntryData> Clips;
    }

    [DataContract]
    public class AngleStreamData
    {
        [DataMember] public int AngleIndex;
        [DataMember] public List<StreamData> Streams;
    }

    [DataContract]
    public class PlaylistData
    {
        [DataMember] public string Name;
        [DataMember] public bool HasHiddenTracks;
        [DataMember] public bool HasLoops;
        [DataMember] public bool IsCustom;
        [DataMember] public bool MVCBaseViewR;
        [DataMember] public int AngleCount;
        [DataMember] public List<double> Chapters;
        [DataMember] public List<StreamClipData> StreamClips;
        [DataMember] public List<AngleClipData> AngleClips;
        [DataMember] public List<AngleStreamData> AngleStreams;
        [DataMember] public List<StreamData> Streams;
        [DataMember] public List<StreamData> PlaylistStreams;
        [DataMember] public List<StreamData> SortedStreams;
        [DataMember] public List<StreamData> VideoStreams;
        [DataMember] public List<StreamData> AudioStreams;
        [DataMember] public List<StreamData> TextStreams;
        [DataMember] public List<StreamData> GraphicsStreams;
    }

    [DataContract]
    public class InterleavedFileData
    {
        [DataMember] public string Name;
    }

    [DataContract]
    public class ScanFileExceptionData
    {
        [DataMember] public string FileName;
        [DataMember] public string Message;
        [DataMember] public string StackTrace;
    }

    [DataContract]
    public class ScanResultData
    {
        [DataMember] public string ScanExceptionMessage;
        [DataMember] public string ScanExceptionStack;
        [DataMember] public List<ScanFileExceptionData> FileExceptions;
    }

    [DataContract]
    public class BDROMData
    {
        [DataMember] public string VolumeLabel;
        [DataMember] public string DiscTitle;
        [DataMember] public ulong Size;
        [DataMember] public bool IsBDPlus;
        [DataMember] public bool IsBDJava;
        [DataMember] public bool IsDBOX;
        [DataMember] public bool IsPSP;
        [DataMember] public bool Is3D;
        [DataMember] public bool Is50Hz;
        [DataMember] public bool IsUHD;
        [DataMember] public List<StreamFileData> StreamFiles;
        [DataMember] public List<StreamClipFileData> StreamClipFiles;
        [DataMember] public List<InterleavedFileData> InterleavedFiles;
        [DataMember] public List<PlaylistData> Playlists;
    }

    [DataContract]
    public class BDInfoReportData
    {
        [DataMember] public string Version;
        [DataMember] public DateTime CreatedUtc;
        [DataMember] public string SourcePath;
        [DataMember] public BDROMData Disc;
        [DataMember] public ScanResultData ScanResult;
    }
}

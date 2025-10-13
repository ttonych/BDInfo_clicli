using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BDInfo
{
    internal static class ConsoleRunner
    {
        private static readonly string[] ChartTypes = new[]
        {
            "Video Bitrate: 1-Second Window",
            "Video Bitrate: 5-Second Window",
            "Video Bitrate: 10-Second Window",
            "Video Frame Size (Min / Max)",
            "Video Frame Type Counts",
            "Video Frame Type Sizes"
        };

        private sealed class CliOptions
        {
            public string BdPath;
            public string ReportDestination;
            public bool ShowHelp;
            public bool ShowVersion;
            public bool ListPlaylists;
            public bool WholeDisc;
            public bool SaveCharts;
            public string ChartFormat = "png";
            public List<string> PlaylistNames { get; } = new List<string>();
        }

        public static bool TryHandle(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return false;
            }

            using (ConsoleWindow.EnsureAttached())
            {
                CliOptions options;
                try
                {
                    options = ParseArguments(args);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    PrintUsage();
                    Environment.ExitCode = 1;
                    return true;
                }

                if (options.ShowVersion)
                {
                    Console.WriteLine(GetVersionString());
                    return true;
                }

                if (options.ShowHelp)
                {
                    PrintUsage();
                    return true;
                }

                if (string.IsNullOrWhiteSpace(options.BdPath))
                {
                    Console.Error.WriteLine("BD_PATH is required.");
                    PrintUsage();
                    Environment.ExitCode = 1;
                    return true;
                }

                try
                {
                    Run(options);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    Environment.ExitCode = 1;
                }

                return true;
            }
        }

        private static CliOptions ParseArguments(string[] args)
        {
            var options = new CliOptions();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.IsNullOrWhiteSpace(arg))
                {
                    continue;
                }

                if (IsHelpOption(arg))
                {
                    options.ShowHelp = true;
                    continue;
                }

                if (IsVersionOption(arg))
                {
                    options.ShowVersion = true;
                    continue;
                }

                if (IsOption(arg, "-l", "--list"))
                {
                    options.ListPlaylists = true;
                    continue;
                }

                if (IsOption(arg, "-w", "--whole"))
                {
                    options.WholeDisc = true;
                    continue;
                }

                if (IsOption(arg, "-m", "--mpls"))
                {
                    string value = ExtractOptionValue(args, ref i, "-m", "--mpls");
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        throw new ArgumentException("The --mpls option requires a comma separated list of playlists.");
                    }
                    options.PlaylistNames.AddRange(SplitValues(value));
                    continue;
                }

                if (IsOption(arg, "-c", "--charts"))
                {
                    string value = ExtractOptionValue(args, ref i, "-c", "--charts", allowMissingValue: true);
                    options.SaveCharts = true;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        options.ChartFormat = value;
                    }
                    continue;
                }

                if (arg.StartsWith("-", StringComparison.Ordinal))
                {
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Unknown option: {0}", arg));
                }

                if (string.IsNullOrEmpty(options.BdPath))
                {
                    options.BdPath = arg;
                }
                else if (string.IsNullOrEmpty(options.ReportDestination))
                {
                    options.ReportDestination = arg;
                }
                else
                {
                    throw new ArgumentException("Too many positional arguments supplied.");
                }
            }

            return options;
        }

        private static bool IsHelpOption(string value)
        {
            return string.Equals(value, "-?", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(value, "-h", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(value, "--help", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsVersionOption(string value)
        {
            return string.Equals(value, "-v", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(value, "--version", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOption(string value, string shortOption, string longOption)
        {
            if (string.Equals(value, shortOption, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, longOption, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(longOption)
                && value.StartsWith(longOption + "=", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(shortOption)
                && value.StartsWith(shortOption + "=", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(shortOption)
                && shortOption.Length == 2
                && value.StartsWith(shortOption, StringComparison.OrdinalIgnoreCase)
                && value.Length > shortOption.Length
                && value[shortOption.Length] != '=')
            {
                return true;
            }

            return false;
        }

        private static string ExtractOptionValue(string[] args, ref int index, string shortOption, string longOption, bool allowMissingValue = false)
        {
            string arg = args[index];

            if (!string.IsNullOrEmpty(longOption) && arg.StartsWith(longOption, StringComparison.OrdinalIgnoreCase))
            {
                if (arg.Length == longOption.Length)
                {
                    if (allowMissingValue)
                    {
                        return null;
                    }

                    if (index + 1 >= args.Length)
                    {
                        throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Missing value for option {0}.", longOption));
                    }

                    index++;
                    return args[index];
                }

                if (arg[longOption.Length] == '=')
                {
                    return arg.Substring(longOption.Length + 1);
                }
            }

            if (!string.IsNullOrEmpty(shortOption) && arg.StartsWith(shortOption, StringComparison.OrdinalIgnoreCase))
            {
                if (arg.Length == shortOption.Length)
                {
                    if (allowMissingValue)
                    {
                        return null;
                    }

                    if (index + 1 >= args.Length)
                    {
                        throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Missing value for option {0}.", shortOption));
                    }

                    index++;
                    return args[index];
                }

                if (arg[shortOption.Length] == '=')
                {
                    return arg.Substring(shortOption.Length + 1);
                }

                return arg.Substring(shortOption.Length);
            }

            return null;
        }

        private static IEnumerable<string> SplitValues(string value)
        {
            return value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => v.Trim())
                        .Where(v => v.Length > 0);
        }

        private static void Run(CliOptions options)
        {
            string bdPath = Path.GetFullPath(options.BdPath);
            bool isDirectory = Directory.Exists(bdPath);
            bool isFile = File.Exists(bdPath);

            if (!isDirectory && !isFile)
            {
                throw new FileNotFoundException(string.Format(CultureInfo.InvariantCulture, "The specified path does not exist: {0}", bdPath));
            }

            if (isFile && !string.Equals(Path.GetExtension(bdPath), ".iso", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("BD_PATH must be a directory containing a BDMV folder or an ISO file.");
            }

            string reportDestination = options.ReportDestination;
            if (string.IsNullOrWhiteSpace(reportDestination))
            {
                if (isFile)
                {
                    throw new ArgumentException("REPORT_DEST is required when BD_PATH is an ISO file.");
                }
                reportDestination = bdPath;
            }

            reportDestination = Path.GetFullPath(reportDestination);
            Directory.CreateDirectory(reportDestination);

            var (chartFormat, chartExtension) = GetImageFormat(options.ChartFormat);

            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "BDInfo v{0}", GetVersionString()));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Source: {0}", bdPath));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Report destination: {0}", reportDestination));

            BDROM bdrom = null;
            try
            {
                bdrom = new BDROM(bdPath);
                AttachErrorHandlers(bdrom);
                bdrom.Scan();

                if (options.ListPlaylists)
                {
                    PrintPlaylistList(bdrom);
                    if (!NeedsFurtherProcessing(options))
                    {
                        return;
                    }
                }

                List<TSPlaylistFile> selectedPlaylists = SelectPlaylists(bdrom, options);
                if (selectedPlaylists.Count == 0)
                {
                    throw new InvalidOperationException("No playlists matched the supplied criteria.");
                }

                List<TSStreamFile> streamFiles = SelectStreamFiles(bdrom, selectedPlaylists, options.WholeDisc);

                ScanBDROMResult scanResult = ScanStreamFiles(bdrom, selectedPlaylists, streamFiles);

                if (scanResult.ScanException != null)
                {
                    throw scanResult.ScanException;
                }

                if (scanResult.FileExceptions.Count > 0)
                {
                    Console.Error.WriteLine("Scan completed with errors:");
                    foreach (var kvp in scanResult.FileExceptions)
                    {
                        Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture, "  {0}: {1}", kvp.Key, kvp.Value.Message));
                    }
                }

                string reportPath = GenerateReport(bdrom, selectedPlaylists, scanResult, reportDestination);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Report written to: {0}", reportPath));

                if (options.SaveCharts)
                {
                    string chartsDirectory = Path.Combine(reportDestination, "charts");
                    int chartsSaved = SaveCharts(selectedPlaylists, chartsDirectory, chartFormat, chartExtension);
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Charts saved to: {0} ({1} files)", chartsDirectory, chartsSaved));
                }
            }
            finally
            {
                bdrom?.CloseDiscImage();
            }
        }

        private static bool NeedsFurtherProcessing(CliOptions options)
        {
            return options.PlaylistNames.Count > 0 || options.WholeDisc || options.SaveCharts;
        }

        private static void AttachErrorHandlers(BDROM bdrom)
        {
            bdrom.StreamClipFileScanError += (clip, ex) =>
            {
                Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture, "Error scanning stream clip {0}: {1}", clip?.Name, ex.Message));
                return true;
            };

            bdrom.StreamFileScanError += (stream, ex) =>
            {
                Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture, "Error scanning stream file {0}: {1}", stream?.Name, ex.Message));
                return true;
            };

            bdrom.PlaylistFileScanError += (playlist, ex) =>
            {
                Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture, "Error scanning playlist {0}: {1}", playlist?.Name, ex.Message));
                return true;
            };
        }

        private static void PrintPlaylistList(BDROM bdrom)
        {
            Console.WriteLine("Playlists:");
            var playlists = bdrom.PlaylistFiles.Values
                .Where(p => p != null && p.IsValid)
                .ToList();

            playlists.Sort(FormMain.ComparePlaylistFiles);

            foreach (TSPlaylistFile playlist in playlists)
            {
                TimeSpan length = new TimeSpan((long)(playlist.TotalLength * 10000000));
                string size = playlist.FileSize > 0
                    ? ToolBox.FormatFileSize(playlist.FileSize)
                    : "-";

                Console.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "  {0,-12}  {1}  {2,12}  {3,6} clips",
                    playlist.Name,
                    string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2}", length.Hours, length.Minutes, length.Seconds),
                    size,
                    playlist.StreamClips.Count));
            }
        }

        private static List<TSPlaylistFile> SelectPlaylists(BDROM bdrom, CliOptions options)
        {
            var playlists = new List<TSPlaylistFile>();
            var playlistMap = bdrom.PlaylistFiles;

            if (options.WholeDisc)
            {
                playlists.AddRange(playlistMap.Values.Where(p => p.IsValid));
            }
            else if (options.PlaylistNames.Count > 0)
            {
                foreach (string name in options.PlaylistNames)
                {
                    string normalized = NormalizePlaylistName(name);
                    if (playlistMap.TryGetValue(normalized, out TSPlaylistFile playlist))
                    {
                        if (playlist.IsValid)
                        {
                            if (!playlists.Contains(playlist))
                            {
                                playlists.Add(playlist);
                            }
                        }
                        else
                        {
                            Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture, "Playlist {0} is filtered out by the current settings.", normalized));
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture, "Playlist {0} was not found on the disc.", normalized));
                    }
                }
            }
            else
            {
                playlists.AddRange(playlistMap.Values.Where(p => p.IsValid));
                playlists.Sort(FormMain.ComparePlaylistFiles);
                if (playlists.Count > 1)
                {
                    playlists = new List<TSPlaylistFile> { playlists[0] };
                }
            }

            playlists.Sort(FormMain.ComparePlaylistFiles);
            return playlists;
        }

        private static string NormalizePlaylistName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            string trimmed = name.Trim();
            if (!trimmed.EndsWith(".MPLS", StringComparison.OrdinalIgnoreCase))
            {
                trimmed += ".MPLS";
            }

            return trimmed.ToUpperInvariant();
        }

        private static List<TSStreamFile> SelectStreamFiles(BDROM bdrom, IEnumerable<TSPlaylistFile> playlists, bool wholeDisc)
        {
            if (wholeDisc)
            {
                return bdrom.StreamFiles.Values.Where(s => s != null).Distinct().ToList();
            }

            var streamFiles = new List<TSStreamFile>();
            foreach (TSPlaylistFile playlist in playlists)
            {
                foreach (TSStreamClip clip in playlist.StreamClips)
                {
                    if (clip.StreamFile != null && !streamFiles.Contains(clip.StreamFile))
                    {
                        streamFiles.Add(clip.StreamFile);
                    }
                }
            }
            return streamFiles;
        }

        private static ScanBDROMResult ScanStreamFiles(BDROM bdrom, List<TSPlaylistFile> playlists, List<TSStreamFile> streamFiles)
        {
            var scanResult = new ScanBDROMResult
            {
                ScanException = null
            };

            foreach (TSPlaylistFile playlist in playlists)
            {
                playlist.ClearBitrates();
            }

            var playlistMap = new Dictionary<string, List<TSPlaylistFile>>(StringComparer.OrdinalIgnoreCase);
            foreach (TSStreamFile streamFile in streamFiles)
            {
                var mappedPlaylists = new List<TSPlaylistFile>();
                foreach (TSPlaylistFile playlist in playlists)
                {
                    foreach (TSStreamClip clip in playlist.StreamClips)
                    {
                        if (clip.Name == streamFile.Name)
                        {
                            if (!mappedPlaylists.Contains(playlist))
                            {
                                mappedPlaylists.Add(playlist);
                            }
                            break;
                        }
                    }
                }
                playlistMap[streamFile.Name] = mappedPlaylists;
            }

            foreach (TSStreamFile streamFile in streamFiles)
            {
                try
                {
                    if (!playlistMap.TryGetValue(streamFile.Name, out List<TSPlaylistFile> mappedPlaylists) || mappedPlaylists.Count == 0)
                    {
                        continue;
                    }

                    streamFile.Scan(mappedPlaylists, true);
                }
                catch (Exception ex)
                {
                    scanResult.FileExceptions[streamFile.Name] = ex;
                }
            }

            return scanResult;
        }

        private static string GenerateReport(BDROM bdrom, List<TSPlaylistFile> playlists, ScanBDROMResult scanResult, string destination)
        {
            using (var report = new FormReport())
            {
                report.Generate(bdrom, playlists, scanResult);
                string reportText = report.ReportText;

                string volumeLabel = string.IsNullOrWhiteSpace(bdrom.VolumeLabel)
                    ? "UNKNOWN"
                    : bdrom.VolumeLabel;

                string fileName = ToolBox.GetSafeFileName(string.Format(CultureInfo.InvariantCulture, "BDINFO.{0}.txt", volumeLabel));
                string reportPath = Path.Combine(destination, fileName);
                File.WriteAllText(reportPath, reportText);
                return reportPath;
            }
        }

        private static int SaveCharts(IEnumerable<TSPlaylistFile> playlists, string directory, ImageFormat format, string extension)
        {
            Directory.CreateDirectory(directory);
            int savedCount = 0;

            foreach (TSPlaylistFile playlist in playlists)
            {
                foreach (TSVideoStream videoStream in playlist.VideoStreams)
                {
                    for (int angleIndex = 0; angleIndex <= playlist.AngleStreams.Count; angleIndex++)
                    {
                        foreach (string chartType in ChartTypes)
                        {
                            try
                            {
                                using (var chart = new FormChart())
                                {
                                    chart.CreateControl();
                                    chart.Generate(chartType, playlist, videoStream.PID, angleIndex);
                                    chart.SaveChartImage(directory, format, extension);
                                    savedCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture, "Failed to save chart for {0} ({1}): {2}", playlist.Name, chartType, ex.Message));
                            }
                        }
                    }
                }
            }

            return savedCount;
        }

        private static (ImageFormat format, string extension) GetImageFormat(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return (ImageFormat.Png, ".png");
            }

            switch (name.Trim().ToLowerInvariant())
            {
                case "png":
                    return (ImageFormat.Png, ".png");
                case "jpg":
                case "jpeg":
                    return (ImageFormat.Jpeg, ".jpg");
                case "bmp":
                    return (ImageFormat.Bmp, ".bmp");
                case "gif":
                    return (ImageFormat.Gif, ".gif");
                case "tif":
                case "tiff":
                    return (ImageFormat.Tiff, ".tiff");
                default:
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Unsupported chart image format: {0}", name));
            }
        }

        private static string GetVersionString()
        {
            return Application.ProductVersion;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: BDInfo.exe <BD_PATH> [REPORT_DEST]");
            Console.WriteLine("BD_PATH may be a directory containing a BDMV folder or a BluRay ISO file.");
            Console.WriteLine("REPORT_DEST is the folder the BDInfo report is to be written to. If not given, the report will be written to BD_PATH. REPORT_DEST is required if BD_PATH is an ISO file.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -?, --help, -h             Print out the options.");
            Console.WriteLine("  -l, --list                 Print the list of playlists.");
            Console.WriteLine("  -m, --mpls=VALUE           Comma separated list of playlists to scan.");
            Console.WriteLine("  -w, --whole                Scan whole disc - every playlist.");
            Console.WriteLine("  -v, --version              Print the version.");
            Console.WriteLine("  -c, --charts               Save all charts as image (select image format, default png)");
        }
    }
}

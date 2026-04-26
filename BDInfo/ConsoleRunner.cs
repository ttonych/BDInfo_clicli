using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using BDInfo.Reporting;

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

        private enum ReportFormat
        {
            Text,
            Bdinfo,
            BdinfoJson
        }

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
            public bool ReportFormatsSpecified;
            public List<string> PlaylistNames { get; } = new List<string>();
            public HashSet<ReportFormat> ReportFormats { get; } = new HashSet<ReportFormat> { ReportFormat.Text };
            public bool CompressReports;
        }

        public static bool TryHandle(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return false;
            }

            if (!ShouldHandle(args))
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

        private static bool ShouldHandle(string[] args)
        {
            bool hasOption = false;
            int positionalCount = 0;

            foreach (string arg in args)
            {
                if (string.IsNullOrWhiteSpace(arg))
                {
                    continue;
                }

                if (arg.StartsWith("-", StringComparison.Ordinal))
                {
                    hasOption = true;
                }
                else
                {
                    positionalCount++;
                }
            }

            return hasOption || positionalCount > 1;
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

                if (IsOption(arg, "-m", "--mpls", allowShortValue: true, allowLongValue: true))
                {
                    string value = ExtractOptionValue(args, ref i, "-m", "--mpls");
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        throw new ArgumentException("The --mpls option requires a comma separated list of playlists.");
                    }
                    options.PlaylistNames.AddRange(SplitValues(value));
                    continue;
                }

                if (IsOption(arg, "-c", "--charts", allowLongValue: true))
                {
                    string value = ExtractOptionValue(args, ref i, "-c", "--charts", allowMissingValue: true);
                    options.SaveCharts = true;
                    if (string.IsNullOrWhiteSpace(value) &&
                        i + 1 < args.Length &&
                        IsChartFormatName(args[i + 1]))
                    {
                        i++;
                        value = args[i];
                    }

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        options.ChartFormat = value;
                    }
                    continue;
                }

                if (IsOption(arg, "-z", "--compress"))
                {
                    options.CompressReports = true;
                    continue;
                }

                if (IsOption(arg, "-r", "--report", allowShortValue: true, allowLongValue: true))
                {
                    string value = ExtractOptionValue(args, ref i, "-r", "--report");
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        throw new ArgumentException("The --report option requires a comma separated list of formats.");
                    }

                    if (!options.ReportFormatsSpecified)
                    {
                        options.ReportFormats.Clear();
                    }

                    bool anyFormat = false;
                    foreach (string token in SplitValues(value))
                    {
                        options.ReportFormats.Add(ParseReportFormat(token));
                        anyFormat = true;
                    }

                    if (!anyFormat)
                    {
                        throw new ArgumentException("The --report option did not include any formats.");
                    }

                    options.ReportFormatsSpecified = true;
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

        private static bool IsOption(string value, string shortOption, string longOption, bool allowShortValue = false, bool allowLongValue = false)
        {
            if (string.Equals(value, shortOption, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, longOption, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(longOption)
                && allowLongValue
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
                && allowShortValue
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

            bool requiresReportDestination = !options.ListPlaylists || NeedsFurtherProcessing(options);
            string reportDestination = null;
            ImageFormat chartFormat = null;
            string chartExtension = null;

            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "BDInfo v{0}", GetVersionString()));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Source: {0}", bdPath));

            if (requiresReportDestination)
            {
                reportDestination = options.ReportDestination;
                if (string.IsNullOrWhiteSpace(reportDestination))
                {
                    string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    if (string.IsNullOrWhiteSpace(baseDirectory))
                    {
                        baseDirectory = Environment.CurrentDirectory;
                    }

                    reportDestination = baseDirectory;
                }

                reportDestination = Path.GetFullPath(reportDestination);
                EnsureReportDestination(reportDestination);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Report destination: {0}", reportDestination));

                if (options.SaveCharts)
                {
                    (chartFormat, chartExtension) = GetImageFormat(options.ChartFormat);
                }
            }

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

                if (selectedPlaylists.Count > 0)
                {
                    Console.WriteLine("Playlists selected for scan:");
                    foreach (TSPlaylistFile playlist in selectedPlaylists)
                    {
                        if (playlist != null)
                        {
                            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  {0}", playlist.Name));
                        }
                    }
                }

                List<TSStreamFile> streamFiles = SelectStreamFiles(bdrom, selectedPlaylists, options.WholeDisc);

                ScanBDROMResult scanResult = ScanStreamFiles(bdrom, selectedPlaylists, streamFiles);

                UpdatePlaylistStreams(selectedPlaylists);

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

                if (reportDestination == null)
                {
                    throw new InvalidOperationException("Report destination was not resolved.");
                }

                if (options.ReportFormats.Count == 0)
                {
                    options.ReportFormats.Add(ReportFormat.Text);
                }

                List<string> reportPaths = GenerateReports(
                    bdrom,
                    selectedPlaylists,
                    scanResult,
                    reportDestination,
                    options.ReportFormats,
                    options.CompressReports);
                foreach (string path in reportPaths)
                {
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Report written to: {0}", path));
                }

                if (options.SaveCharts)
                {
                    if (chartFormat == null || string.IsNullOrEmpty(chartExtension))
                    {
                        (chartFormat, chartExtension) = GetImageFormat(options.ChartFormat);
                    }

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

        private static void EnsureReportDestination(string destination)
        {
            if (string.IsNullOrWhiteSpace(destination))
            {
                throw new ArgumentException("Report destination cannot be empty.");
            }

            if (File.Exists(destination))
            {
                throw new IOException(string.Format(CultureInfo.InvariantCulture, "Report destination '{0}' is a file.", destination));
            }

            try
            {
                Directory.CreateDirectory(destination);
            }
            catch (Exception ex)
            {
                throw new IOException(string.Format(CultureInfo.InvariantCulture, "Unable to create report destination '{0}': {1}", destination, ex.Message), ex);
            }

            string probeFile = Path.Combine(destination, Path.GetRandomFileName());
            try
            {
                using (File.Create(probeFile, 1, FileOptions.DeleteOnClose))
                {
                }
            }
            catch (Exception)
            {
                try
                {
                    using (File.Create(probeFile, 1))
                    {
                    }
                }
                catch (Exception ex)
                {
                    throw new IOException(string.Format(CultureInfo.InvariantCulture, "Unable to write to report destination '{0}': {1}", destination, ex.Message), ex);
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(probeFile))
                    {
                        File.Delete(probeFile);
                    }
                }
                catch
                {
                }
            }
        }

        private static bool NeedsFurtherProcessing(CliOptions options)
        {
            return options.PlaylistNames.Count > 0
                   || options.WholeDisc
                   || options.SaveCharts
                   || options.ReportFormatsSpecified;
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
                var seenPlaylists = new HashSet<TSPlaylistFile>();
                foreach (string name in options.PlaylistNames)
                {
                    IReadOnlyList<string> candidates = GetPlaylistNameCandidates(name);
                    TSPlaylistFile playlist = ResolvePlaylistByCandidates(playlistMap, candidates);
                    string displayName = GetPlaylistDisplayName(candidates, name);

                    if (playlist != null)
                    {
                        if (playlist.IsValid)
                        {
                            if (seenPlaylists.Add(playlist))
                            {
                                playlists.Add(playlist);
                            }
                        }
                        else
                        {
                            Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture, "Playlist {0} is filtered out by the current settings.", displayName));
                        }
                    }
                    else if (!string.IsNullOrEmpty(displayName))
                    {
                        Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture, "Playlist {0} was not found on the disc.", displayName));
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
            string fileName = Path.GetFileName(trimmed);
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = trimmed;
            }

            string baseName = Path.GetFileNameWithoutExtension(fileName);
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = fileName;
            }

            if (baseName.All(char.IsDigit))
            {
                baseName = baseName.PadLeft(5, '0');
            }

            baseName = baseName.ToUpperInvariant();

            return string.Format(CultureInfo.InvariantCulture, "{0}.MPLS", baseName);
        }

        private static IReadOnlyList<string> GetPlaylistNameCandidates(string name)
        {
            var results = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(name))
            {
                return results;
            }

            string trimmed = name.Trim();

            void AddCandidate(string candidate)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    return;
                }

                string trimmedCandidate = candidate.Trim();
                if (trimmedCandidate.Length == 0)
                {
                    return;
                }

                string normalizedCandidate = trimmedCandidate.ToUpperInvariant();
                if (seen.Add(normalizedCandidate))
                {
                    results.Add(normalizedCandidate);
                }
            }

            AddCandidate(trimmed);

            string fileName = Path.GetFileName(trimmed);
            AddCandidate(fileName);

            string baseName = Path.GetFileNameWithoutExtension(fileName);
            AddCandidate(baseName);

            if (!string.IsNullOrEmpty(baseName))
            {
                AddCandidate(baseName + ".MPLS");

                if (baseName.All(char.IsDigit))
                {
                    string padded = baseName.PadLeft(5, '0');
                    AddCandidate(padded);
                    AddCandidate(padded + ".MPLS");

                    string unpadded = baseName.TrimStart('0');
                    if (unpadded.Length == 0 && baseName.Length > 0)
                    {
                        unpadded = "0";
                    }

                    AddCandidate(unpadded);
                    AddCandidate(unpadded + ".MPLS");
                }
            }

            string normalized = NormalizePlaylistName(trimmed);
            AddCandidate(normalized);

            return results;
        }

        private static TSPlaylistFile ResolvePlaylistByCandidates(Dictionary<string, TSPlaylistFile> playlistMap, IReadOnlyList<string> candidates)
        {
            if (playlistMap == null || playlistMap.Count == 0 || candidates == null || candidates.Count == 0)
            {
                return null;
            }

            foreach (string candidate in candidates)
            {
                if (playlistMap.TryGetValue(candidate, out TSPlaylistFile playlist) && playlist != null)
                {
                    return playlist;
                }
            }

            foreach (TSPlaylistFile playlist in playlistMap.Values)
            {
                if (playlist == null)
                {
                    continue;
                }

                string playlistName = playlist.Name ?? string.Empty;
                string playlistBase = Path.GetFileNameWithoutExtension(playlistName) ?? string.Empty;

                foreach (string candidate in candidates)
                {
                    if (string.Equals(playlistName, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        return playlist;
                    }

                    if (!string.IsNullOrEmpty(playlistBase) && string.Equals(playlistBase, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        return playlist;
                    }
                }
            }

            return null;
        }

        private static string GetPlaylistDisplayName(IReadOnlyList<string> candidates, string originalName)
        {
            if (candidates != null && candidates.Count > 0)
            {
                return candidates[0];
            }

            string normalized = NormalizePlaylistName(originalName);
            if (!string.IsNullOrEmpty(normalized))
            {
                return normalized;
            }

            if (!string.IsNullOrWhiteSpace(originalName))
            {
                return originalName.Trim();
            }

            return string.Empty;
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
            long totalBytes = 0;

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
                totalBytes += GetStreamFileLength(streamFile);
            }

            var progressBar = new CliProgressBar(totalBytes, streamFiles.Count);
            long finishedBytes = 0;

            progressBar.Report("Preparing scan", finishedBytes);

            foreach (TSStreamFile streamFile in streamFiles)
            {
                string displayName = streamFile?.DisplayName ?? streamFile?.Name ?? string.Empty;
                List<TSPlaylistFile> mappedPlaylists = null;
                if (!playlistMap.TryGetValue(streamFile.Name, out mappedPlaylists) || mappedPlaylists == null)
                {
                    mappedPlaylists = new List<TSPlaylistFile>();
                }

                string playlistSummary = BuildPlaylistSummary(mappedPlaylists);
                string message = string.IsNullOrEmpty(playlistSummary)
                    ? string.Format(CultureInfo.InvariantCulture, "Scanning {0}", displayName)
                    : string.Format(CultureInfo.InvariantCulture, "Scanning {0} ({1})", displayName, playlistSummary);
                progressBar.Report(message, finishedBytes);

                try
                {
                    if (mappedPlaylists.Count > 0)
                    {
                        Exception scanException = ScanStreamFileWithProgress(streamFile, mappedPlaylists, progressBar, message, finishedBytes);
                        if (scanException != null)
                        {
                            scanResult.FileExceptions[streamFile.Name] = scanException;
                        }
                    }
                }
                catch (Exception ex)
                {
                    scanResult.FileExceptions[streamFile.Name] = ex;
                }

                finishedBytes += GetStreamFileLength(streamFile);
                progressBar.Report(message, finishedBytes);
            }

            progressBar.Complete();

            return scanResult;
        }

        private static string BuildPlaylistSummary(IEnumerable<TSPlaylistFile> playlists)
        {
            if (playlists == null)
            {
                return string.Empty;
            }

            var names = playlists
                .Where(p => p != null && !string.IsNullOrWhiteSpace(p.Name))
                .Select(p => p.Name.Trim())
                .Where(n => n.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (names.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(", ", names);
        }

        private static Exception ScanStreamFileWithProgress(
            TSStreamFile streamFile,
            List<TSPlaylistFile> playlists,
            CliProgressBar progressBar,
            string message,
            long completedBytes)
        {
            if (streamFile == null)
            {
                return null;
            }

            Exception scanException = null;

            using (var scanCompleted = new ManualResetEventSlim(false))
            {
                Thread scanThread = new Thread(() =>
                {
                    try
                    {
                        streamFile.Scan(playlists, true);
                    }
                    catch (Exception ex)
                    {
                        scanException = ex;
                    }
                    finally
                    {
                        scanCompleted.Set();
                    }
                })
                {
                    IsBackground = true
                };

                scanThread.Start();

                const int pollIntervalMilliseconds = 200;
                while (!scanCompleted.Wait(pollIntervalMilliseconds))
                {
                    progressBar.Report(message, completedBytes + streamFile.Size);
                }

                scanThread.Join();
            }

            progressBar.Report(message, completedBytes + streamFile.Size);

            return scanException;
        }

        private static void UpdatePlaylistStreams(IEnumerable<TSPlaylistFile> playlists)
        {
            if (playlists == null)
            {
                return;
            }

            foreach (TSPlaylistFile playlist in playlists)
            {
                if (playlist == null)
                {
                    continue;
                }

                foreach (TSStream stream in playlist.Streams.Values)
                {
                    if (stream.IsGraphicsStream)
                    {
                        var graphicsStream = (TSGraphicsStream)stream;
                        graphicsStream.Captions = 0;
                        graphicsStream.ForcedCaptions = 0;
                    }
                }

                foreach (TSStreamClip clip in playlist.StreamClips)
                {
                    TSStreamFile streamFile = clip?.StreamFile;
                    if (streamFile == null)
                    {
                        continue;
                    }

                    foreach (TSStream clipStream in streamFile.Streams.Values)
                    {
                        if (!playlist.Streams.TryGetValue(clipStream.PID, out TSStream playlistStream))
                        {
                            continue;
                        }

                        if (clipStream.IsGraphicsStream && playlistStream.IsGraphicsStream)
                        {
                            var playlistGraphicsStream = (TSGraphicsStream)playlistStream;
                            var clipGraphicsStream = (TSGraphicsStream)clipStream;

                            playlistGraphicsStream.Captions += clipGraphicsStream.Captions;
                            playlistGraphicsStream.ForcedCaptions += clipGraphicsStream.ForcedCaptions;

                            if (playlistGraphicsStream.Width == 0 && clipGraphicsStream.Width > 0)
                            {
                                playlistGraphicsStream.Width = clipGraphicsStream.Width;
                            }

                            if (playlistGraphicsStream.Height == 0 && clipGraphicsStream.Height > 0)
                            {
                                playlistGraphicsStream.Height = clipGraphicsStream.Height;
                            }
                        }
                        else if (clipStream.IsAudioStream && playlistStream.IsAudioStream)
                        {
                            var playlistAudioStream = (TSAudioStream)playlistStream;
                            var clipAudioStream = (TSAudioStream)clipStream;

                            if (clipAudioStream.CoreStream != null)
                            {
                                if (playlistAudioStream.CoreStream == null)
                                {
                                    playlistAudioStream.CoreStream = (TSAudioStream)clipAudioStream.CoreStream.Clone();
                                }
                                else if (!playlistAudioStream.CoreStream.IsInitialized && clipAudioStream.CoreStream.IsInitialized)
                                {
                                    playlistAudioStream.CoreStream = (TSAudioStream)clipAudioStream.CoreStream.Clone();
                                }
                            }

                            MergeAudioStream(playlistAudioStream, clipAudioStream);
                            UpdatePlaylistAudioReferences(playlist, clipAudioStream);
                        }
                        else if (clipStream.IsVideoStream && playlistStream.IsVideoStream)
                        {
                            var playlistVideoStream = (TSVideoStream)playlistStream;
                            var clipVideoStream = (TSVideoStream)clipStream;

                            MergeVideoStream(playlistVideoStream, clipVideoStream);
                            UpdatePlaylistVideoReferences(playlist, clipVideoStream);
                        }
                    }
                }
            }
        }

        private static void MergeAudioStream(TSAudioStream target, TSAudioStream source)
        {
            if (target == null || source == null)
            {
                return;
            }

            if (source.ChannelCount > target.ChannelCount)
            {
                target.ChannelCount = source.ChannelCount;
                target.ChannelLayout = source.ChannelLayout;
            }

            if (source.LFE > target.LFE)
            {
                target.LFE = source.LFE;
            }

            if (source.SampleRate > target.SampleRate)
            {
                target.SampleRate = source.SampleRate;
            }

            if (source.BitDepth > target.BitDepth)
            {
                target.BitDepth = source.BitDepth;
            }

            if (source.BitRate > target.BitRate)
            {
                target.BitRate = source.BitRate;
            }

            if (source.DialNorm != 0 && (target.DialNorm == 0 || source.DialNorm < target.DialNorm))
            {
                target.DialNorm = source.DialNorm;
            }

            if (source.AudioMode != TSAudioMode.Unknown)
            {
                target.AudioMode = source.AudioMode;
            }

            if (source.HasExtensions != target.HasExtensions)
            {
                target.HasExtensions = source.HasExtensions;
            }

            if (source.ExtendedData != target.ExtendedData)
            {
                target.ExtendedData = source.ExtendedData;
            }

            if (!string.IsNullOrEmpty(source.LanguageCode))
            {
                target.LanguageCode = source.LanguageCode;
            }

            if (!string.IsNullOrEmpty(source.LanguageName))
            {
                target.LanguageName = source.LanguageName;
            }

            if (source.IsInitialized)
            {
                target.IsInitialized = true;
            }

            target.IsVBR = source.IsVBR;

            MergeAudioCoreStream(target, source.CoreStream as TSAudioStream);
        }

        private static void MergeAudioCoreStream(TSAudioStream target, TSAudioStream sourceCore)
        {
            if (target == null || sourceCore == null)
            {
                return;
            }

            if (target.CoreStream == null)
            {
                target.CoreStream = (TSAudioStream)sourceCore.Clone();
            }
            else
            {
                MergeAudioStream(target.CoreStream, sourceCore);
            }
        }

        private static void MergeVideoStream(TSVideoStream target, TSVideoStream source)
        {
            if (target == null || source == null)
            {
                return;
            }

            if (source.Width > target.Width)
            {
                target.Width = source.Width;
            }

            if (source.Height > target.Height)
            {
                target.Height = source.Height;
            }

            if (source.FrameRate != TSFrameRate.Unknown)
            {
                target.FrameRate = source.FrameRate;
            }

            if (source.FrameRateEnumerator > 0)
            {
                target.FrameRateEnumerator = source.FrameRateEnumerator;
            }

            if (source.FrameRateDenominator > 0)
            {
                target.FrameRateDenominator = source.FrameRateDenominator;
            }

            if (source.AspectRatio != TSAspectRatio.Unknown)
            {
                target.AspectRatio = source.AspectRatio;
            }

            if (!string.IsNullOrEmpty(source.EncodingProfile))
            {
                target.EncodingProfile = source.EncodingProfile;
            }

            if (source.BitRate > target.BitRate)
            {
                target.BitRate = source.BitRate;
            }

            if (source.ActiveBitRate > target.ActiveBitRate)
            {
                target.ActiveBitRate = source.ActiveBitRate;
            }

            if (source.BaseView != null && target.BaseView == null)
            {
                target.BaseView = source.BaseView;
            }

            if (source.IsInitialized)
            {
                target.IsInitialized = true;
            }

            target.IsVBR = source.IsVBR;

            MergeVideoExtendedData(target, source);
        }

        private static void MergeVideoExtendedData(TSVideoStream target, TSVideoStream source)
        {
            if (target == null || source == null)
            {
                return;
            }

            if (source.ExtendedData is TSCodecHEVC.ExtendedDataSet sourceExtended)
            {
                if (target.ExtendedData is TSCodecHEVC.ExtendedDataSet targetExtended)
                {
                    MergeHevcExtendedData(targetExtended, sourceExtended);
                }
                else
                {
                    target.ExtendedData = CloneHevcExtendedData(sourceExtended);
                }
            }
            else if (source.ExtendedData != null && target.ExtendedData == null)
            {
                target.ExtendedData = source.ExtendedData;
            }
        }

        private static TSCodecHEVC.ExtendedDataSet CloneHevcExtendedData(TSCodecHEVC.ExtendedDataSet source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new TSCodecHEVC.ExtendedDataSet
            {
                MasteringDisplayColorPrimaries = source.MasteringDisplayColorPrimaries,
                MasteringDisplayLuminance = source.MasteringDisplayLuminance,
                MaximumContentLightLevel = source.MaximumContentLightLevel,
                MaximumFrameAverageLightLevel = source.MaximumFrameAverageLightLevel,
                LightLevelAvailable = source.LightLevelAvailable,
                PreferredTransferCharacteristics = source.PreferredTransferCharacteristics,
                IsHdr10Plus = source.IsHdr10Plus
            };

            if (source.ExtendedFormatInfo != null && source.ExtendedFormatInfo.Count > 0)
            {
                clone.ExtendedFormatInfo.AddRange(source.ExtendedFormatInfo);
            }

            if (source.VideoParamSets != null && source.VideoParamSets.Count > 0)
            {
                clone.VideoParamSets.AddRange(source.VideoParamSets);
            }

            if (source.VUIParameterSets != null && source.VUIParameterSets.Count > 0)
            {
                clone.VUIParameterSets.AddRange(source.VUIParameterSets);
            }

            if (source.SeqParameterSets != null && source.SeqParameterSets.Count > 0)
            {
                clone.SeqParameterSets.AddRange(source.SeqParameterSets);
            }

            if (source.PicParameterSets != null && source.PicParameterSets.Count > 0)
            {
                clone.PicParameterSets.AddRange(source.PicParameterSets);
            }

            return clone;
        }

        private static void MergeHevcExtendedData(TSCodecHEVC.ExtendedDataSet target, TSCodecHEVC.ExtendedDataSet source)
        {
            if (target == null || source == null)
            {
                return;
            }

            if (source.ExtendedFormatInfo != null)
            {
                foreach (string value in source.ExtendedFormatInfo)
                {
                    if (!string.IsNullOrEmpty(value) && !target.ExtendedFormatInfo.Contains(value))
                    {
                        target.ExtendedFormatInfo.Add(value);
                    }
                }
            }

            if (string.IsNullOrEmpty(target.MasteringDisplayColorPrimaries) && !string.IsNullOrEmpty(source.MasteringDisplayColorPrimaries))
            {
                target.MasteringDisplayColorPrimaries = source.MasteringDisplayColorPrimaries;
            }

            if (string.IsNullOrEmpty(target.MasteringDisplayLuminance) && !string.IsNullOrEmpty(source.MasteringDisplayLuminance))
            {
                target.MasteringDisplayLuminance = source.MasteringDisplayLuminance;
            }

            if (source.MaximumContentLightLevel > target.MaximumContentLightLevel)
            {
                target.MaximumContentLightLevel = source.MaximumContentLightLevel;
            }

            if (source.MaximumFrameAverageLightLevel > target.MaximumFrameAverageLightLevel)
            {
                target.MaximumFrameAverageLightLevel = source.MaximumFrameAverageLightLevel;
            }

            if (source.LightLevelAvailable)
            {
                target.LightLevelAvailable = true;
            }

            if (source.PreferredTransferCharacteristics != 0)
            {
                target.PreferredTransferCharacteristics = source.PreferredTransferCharacteristics;
            }

            if (source.IsHdr10Plus)
            {
                target.IsHdr10Plus = true;
            }
        }

        private static void UpdatePlaylistVideoReferences(TSPlaylistFile playlist, TSVideoStream clipVideoStream)
        {
            if (playlist == null || clipVideoStream == null)
            {
                return;
            }

            if (playlist.PlaylistStreams != null && playlist.PlaylistStreams.TryGetValue(clipVideoStream.PID, out TSStream playlistStream))
            {
                MergeVideoStream(playlistStream as TSVideoStream, clipVideoStream);
            }

            if (playlist.AngleStreams == null)
            {
                return;
            }

            foreach (Dictionary<ushort, TSStream> angleStreamMap in playlist.AngleStreams)
            {
                if (angleStreamMap == null)
                {
                    continue;
                }

                if (angleStreamMap.TryGetValue(clipVideoStream.PID, out TSStream angleStream))
                {
                    MergeVideoStream(angleStream as TSVideoStream, clipVideoStream);
                }
            }
        }

        private static void UpdatePlaylistAudioReferences(TSPlaylistFile playlist, TSAudioStream clipAudioStream)
        {
            if (playlist == null || clipAudioStream == null)
            {
                return;
            }

            if (playlist.PlaylistStreams != null && playlist.PlaylistStreams.TryGetValue(clipAudioStream.PID, out TSStream playlistStream))
            {
                MergeAudioStream(playlistStream as TSAudioStream, clipAudioStream);
            }

            if (playlist.AngleStreams != null)
            {
                foreach (Dictionary<ushort, TSStream> angleStreamMap in playlist.AngleStreams)
                {
                    if (angleStreamMap == null)
                    {
                        continue;
                    }

                    if (angleStreamMap.TryGetValue(clipAudioStream.PID, out TSStream angleStream))
                    {
                        MergeAudioStream(angleStream as TSAudioStream, clipAudioStream);
                    }
                }
            }
        }

        private static long GetStreamFileLength(TSStreamFile streamFile)
        {
            if (streamFile == null)
            {
                return 0;
            }

            if (BDInfoSettings.EnableSSIF && streamFile.InterleavedFile != null)
            {
                if (streamFile.InterleavedFile.FileInfo != null)
                {
                    return streamFile.InterleavedFile.FileInfo.Length;
                }

                if (streamFile.InterleavedFile.DFileInfo != null)
                {
                    return streamFile.InterleavedFile.DFileInfo.Length;
                }
            }

            if (streamFile.FileInfo != null)
            {
                return streamFile.FileInfo.Length;
            }

            if (streamFile.DFileInfo != null)
            {
                return streamFile.DFileInfo.Length;
            }

            if (streamFile.Size > 0)
            {
                return streamFile.Size;
            }

            return 0;
        }

        private sealed class CliProgressBar
        {
            private readonly long _totalBytes;
            private readonly int _barWidth;
            private readonly bool _enabled;
            private int _lastLength;

            public CliProgressBar(long totalBytes, int itemCount, int barWidth = 40)
            {
                _enabled = itemCount > 0;
                _barWidth = barWidth;
                _totalBytes = totalBytes > 0 ? totalBytes : 1;

                if (_enabled)
                {
                    Console.WriteLine();
                }
            }

            public void Report(string message, long completedBytes)
            {
                if (!_enabled)
                {
                    return;
                }

                if (message == null)
                {
                    message = string.Empty;
                }

                long boundedBytes = completedBytes;
                if (boundedBytes < 0)
                {
                    boundedBytes = 0;
                }
                if (boundedBytes > _totalBytes)
                {
                    boundedBytes = _totalBytes;
                }

                double progress = (double)boundedBytes / _totalBytes;
                if (progress < 0)
                {
                    progress = 0;
                }
                if (progress > 1)
                {
                    progress = 1;
                }

                int filled = (int)Math.Round(progress * _barWidth);
                if (filled < 0)
                {
                    filled = 0;
                }
                if (filled > _barWidth)
                {
                    filled = _barWidth;
                }

                if (message.Length > 60)
                {
                    message = message.Substring(0, 57) + "...";
                }

                string bar = new string('#', filled).PadRight(_barWidth);
                string line = string.Format(CultureInfo.InvariantCulture, "[{0}] {1,3}% {2}", bar, (int)Math.Round(progress * 100), message);
                int padding = Math.Max(0, _lastLength - line.Length);
                Console.Write("\r{0}{1}", line, new string(' ', padding));
                _lastLength = line.Length;
            }

            public void Complete()
            {
                if (!_enabled)
                {
                    return;
                }

                Report("Scan complete", _totalBytes);
                Console.WriteLine();
            }
        }

        private static List<string> GenerateReports(
            BDROM bdrom,
            List<TSPlaylistFile> playlists,
            ScanBDROMResult scanResult,
            string destination,
            IReadOnlyCollection<ReportFormat> formats,
            bool compress)
        {
            if (formats == null || formats.Count == 0)
            {
                formats = new[] { ReportFormat.Text };
            }

            string volumeLabel = string.IsNullOrWhiteSpace(bdrom.VolumeLabel)
                ? "UNKNOWN"
                : bdrom.VolumeLabel;

            string sanitizedVolumeLabel = ToolBox.GetSafeFileName(volumeLabel);
            if (string.IsNullOrWhiteSpace(sanitizedVolumeLabel))
            {
                sanitizedVolumeLabel = "BDINFO";
            }

            string sanitizedTextName = ToolBox.GetSafeFileName(string.Format(CultureInfo.InvariantCulture, "BDINFO.{0}.txt", sanitizedVolumeLabel));
            string sanitizedBaseName = sanitizedVolumeLabel;

            var writtenPaths = new List<string>();

            if (formats.Contains(ReportFormat.Text))
            {
                using (var report = new FormReport())
                {
                    report.Generate(bdrom, playlists, scanResult);
                    string reportText = report.ReportText;
                    string reportPath = Path.Combine(destination, sanitizedTextName);
                    File.WriteAllText(reportPath, reportText);
                    writtenPaths.Add(reportPath);
                }
            }

            if (formats.Contains(ReportFormat.BdinfoJson))
            {
                string jsonFileName = formats.Contains(ReportFormat.Bdinfo)
                    ? sanitizedBaseName + ".json.bdinfo"
                    : sanitizedBaseName + ".bdinfo";
                string jsonReportPath = Path.Combine(destination, jsonFileName);
                BDInfoReportSerializer.Save(jsonReportPath, bdrom, playlists, scanResult, BDInfoReportFormat.Json, compress);
                writtenPaths.Add(jsonReportPath);
            }

            if (formats.Contains(ReportFormat.Bdinfo))
            {
                string reportPath = Path.Combine(destination, sanitizedBaseName + ".bdinfo");
                BDInfoReportSerializer.Save(reportPath, bdrom, playlists, scanResult, BDInfoReportFormat.Xml, compress);
                writtenPaths.Add(reportPath);
            }

            return writtenPaths;
        }

        private static ReportFormat ParseReportFormat(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Report format value cannot be empty.");
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case "txt":
                case "text":
                    return ReportFormat.Text;
                case "bdinfo":
                case "bdinfo-xml":
                    return ReportFormat.Bdinfo;
                case "bdinfo-json":
                case "json":
                    return ReportFormat.BdinfoJson;
                default:
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Unsupported report format: {0}", value));
            }
        }

        private static int SaveCharts(IEnumerable<TSPlaylistFile> playlists, string directory, ImageFormat format, string extension)
        {
            Directory.CreateDirectory(directory);
            int savedCount = 0;

            foreach (TSPlaylistFile playlist in playlists)
            {
                bool qualifyNames = playlist.VideoStreams.Count > 1 || playlist.AngleStreams.Count > 0;
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
                                    string qualifier = qualifyNames
                                        ? string.Format(CultureInfo.InvariantCulture, "pid-{0:X4}-angle-{1}", videoStream.PID, angleIndex)
                                        : null;
                                    chart.SaveChartImage(directory, format, extension, qualifier);
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

        private static bool IsChartFormatName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            switch (name.Trim().ToLowerInvariant())
            {
                case "png":
                case "jpg":
                case "jpeg":
                case "bmp":
                case "gif":
                case "tif":
                case "tiff":
                    return true;
                default:
                    return false;
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
            Console.WriteLine("REPORT_DEST is the folder the BDInfo report is to be written to. If not given, the report will be written next to BDInfo.exe.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -?, --help, -h             Print out the options.");
            Console.WriteLine("  -l, --list                 Print the list of playlists.");
            Console.WriteLine("  -m, --mpls=VALUE           Comma separated list of playlists to scan.");
            Console.WriteLine("  -w, --whole                Scan whole disc - every playlist.");
            Console.WriteLine("  -v, --version              Print the version.");
            Console.WriteLine("  -c, --charts[=FORMAT]      Save all charts as image (png by default; also jpg, bmp, gif, tiff).");
            Console.WriteLine("  -r, --report               Choose report formats (txt, bdinfo, bdinfo-json). Use commas for multiple.");
            Console.WriteLine("  -z, --compress             Compress generated .bdinfo reports using ZIP.");
        }
    }
}

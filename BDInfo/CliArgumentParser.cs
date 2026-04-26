using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BDInfo
{
    internal enum ReportFormat
    {
        Text,
        Bdinfo,
        BdinfoJson
    }

    internal sealed class CliOptions
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

    internal static class CliArgumentParser
    {
        public static bool ShouldHandle(string[] args)
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

        public static CliOptions Parse(string[] args)
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
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BDInfo.Reporting;

namespace BDInfo
{
    internal static class CliReportWriter
    {
        public static List<string> WriteReports(
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

            CliReportFileNames fileNames = CreateFileNames(bdrom.VolumeLabel, formats);
            var writtenPaths = new List<string>();

            if (formats.Contains(ReportFormat.Text))
            {
                using (var report = new FormReport())
                {
                    report.Generate(bdrom, playlists, scanResult);
                    string reportText = report.ReportText;
                    string reportPath = Path.Combine(destination, fileNames.TextFileName);
                    File.WriteAllText(reportPath, reportText);
                    writtenPaths.Add(reportPath);
                }
            }

            if (formats.Contains(ReportFormat.BdinfoJson))
            {
                string jsonReportPath = Path.Combine(destination, fileNames.JsonReportFileName);
                BDInfoReportSerializer.Save(jsonReportPath, bdrom, playlists, scanResult, BDInfoReportFormat.Json, compress);
                writtenPaths.Add(jsonReportPath);
            }

            if (formats.Contains(ReportFormat.Bdinfo))
            {
                string reportPath = Path.Combine(destination, fileNames.XmlReportFileName);
                BDInfoReportSerializer.Save(reportPath, bdrom, playlists, scanResult, BDInfoReportFormat.Xml, compress);
                writtenPaths.Add(reportPath);
            }

            return writtenPaths;
        }

        internal static CliReportFileNames CreateFileNames(string volumeLabel, IReadOnlyCollection<ReportFormat> formats)
        {
            if (formats == null || formats.Count == 0)
            {
                formats = new[] { ReportFormat.Text };
            }

            string sanitizedVolumeLabel = ReportFileNameHelper.CreateSafeBaseName(volumeLabel, "UNKNOWN", "BDINFO");

            string textFileName = ToolBox.GetSafeFileName(string.Format(CultureInfo.InvariantCulture, "BDINFO.{0}.txt", sanitizedVolumeLabel));
            string xmlFileName = sanitizedVolumeLabel + ".bdinfo";
            string jsonFileName = formats.Contains(ReportFormat.Bdinfo)
                ? sanitizedVolumeLabel + ".json.bdinfo"
                : sanitizedVolumeLabel + ".bdinfo";

            return new CliReportFileNames(textFileName, xmlFileName, jsonFileName);
        }
    }

    internal sealed class CliReportFileNames
    {
        public CliReportFileNames(string textFileName, string xmlReportFileName, string jsonReportFileName)
        {
            TextFileName = textFileName;
            XmlReportFileName = xmlReportFileName;
            JsonReportFileName = jsonReportFileName;
        }

        public string TextFileName { get; private set; }
        public string XmlReportFileName { get; private set; }
        public string JsonReportFileName { get; private set; }
    }
}

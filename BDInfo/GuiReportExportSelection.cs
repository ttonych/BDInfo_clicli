using System;
using System.IO;
using BDInfo.Reporting;

namespace BDInfo
{
    internal static class GuiReportExportSelection
    {
        public static GuiReportExportOptions Create(int filterIndex, string fileName)
        {
            switch (filterIndex)
            {
                case 2:
                    return GuiReportExportOptions.CreateSnapshot(GetFileNameWithSuffix(fileName, ".bdinfo"));
                case 3:
                    return GuiReportExportOptions.CreateReport(GetFileNameWithSuffix(fileName, ".json.bdinfo"), BDInfoReportFormat.Json, compress: false);
                case 4:
                    return GuiReportExportOptions.CreateReport(GetFileNameWithSuffix(fileName, ".json.bdinfo"), BDInfoReportFormat.Json, compress: true);
                case 5:
                    return GuiReportExportOptions.CreateReport(GetFileNameWithSuffix(fileName, ".xml.bdinfo"), BDInfoReportFormat.Xml, compress: false);
                case 6:
                    return GuiReportExportOptions.CreateReport(GetFileNameWithSuffix(fileName, ".xml.bdinfo"), BDInfoReportFormat.Xml, compress: true);
                case 7:
                    return CreateFromFileName(fileName);
                case 1:
                default:
                    return GuiReportExportOptions.CreateText(GetFileNameWithSuffix(fileName, ".txt"));
            }
        }

        private static GuiReportExportOptions CreateFromFileName(string fileName)
        {
            if (fileName.EndsWith(".json.bdinfo", StringComparison.OrdinalIgnoreCase))
            {
                return GuiReportExportOptions.CreateReport(fileName, BDInfoReportFormat.Json, compress: false);
            }

            if (fileName.EndsWith(".xml.bdinfo", StringComparison.OrdinalIgnoreCase))
            {
                return GuiReportExportOptions.CreateReport(fileName, BDInfoReportFormat.Xml, compress: false);
            }

            if (fileName.EndsWith(".bdinfo", StringComparison.OrdinalIgnoreCase))
            {
                return GuiReportExportOptions.CreateSnapshot(fileName);
            }

            return GuiReportExportOptions.CreateText(GetFileNameWithSuffix(fileName, ".txt"));
        }

        private static string GetFileNameWithSuffix(string fileName, string suffix)
        {
            string directory = Path.GetDirectoryName(fileName);
            string baseName = Path.GetFileName(fileName);

            baseName = RemoveKnownSuffix(baseName, ".json.bdinfo");
            baseName = RemoveKnownSuffix(baseName, ".xml.bdinfo");
            baseName = RemoveKnownSuffix(baseName, ".bdinfo");
            baseName = RemoveKnownSuffix(baseName, ".txt");

            string normalized = baseName + suffix;
            return string.IsNullOrEmpty(directory)
                ? normalized
                : Path.Combine(directory, normalized);
        }

        private static string RemoveKnownSuffix(string value, string suffix)
        {
            if (value != null && value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return value.Substring(0, value.Length - suffix.Length);
            }

            return value;
        }
    }

    internal sealed class GuiReportExportOptions
    {
        private GuiReportExportOptions()
        {
        }

        public bool IsText { get; private set; }
        public bool IsSnapshot { get; private set; }
        public string FileName { get; private set; }
        public BDInfoReportFormat Format { get; private set; }
        public bool Compress { get; private set; }

        public static GuiReportExportOptions CreateReport(string fileName, BDInfoReportFormat format, bool compress)
        {
            return new GuiReportExportOptions
            {
                IsText = false,
                IsSnapshot = false,
                FileName = fileName,
                Format = format,
                Compress = compress
            };
        }

        public static GuiReportExportOptions CreateSnapshot(string fileName)
        {
            return new GuiReportExportOptions
            {
                IsText = false,
                IsSnapshot = true,
                FileName = fileName,
                Format = BDInfoReportFormat.Json,
                Compress = true
            };
        }

        public static GuiReportExportOptions CreateText(string fileName)
        {
            return new GuiReportExportOptions
            {
                IsText = true,
                IsSnapshot = false,
                FileName = fileName,
                Format = BDInfoReportFormat.Xml,
                Compress = false
            };
        }
    }
}

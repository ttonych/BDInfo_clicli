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
                    return GuiReportExportOptions.CreateReport(fileName, BDInfoReportFormat.Json, compress: false);
                case 3:
                    return GuiReportExportOptions.CreateReport(fileName, BDInfoReportFormat.Xml, compress: true);
                case 4:
                    return GuiReportExportOptions.CreateReport(fileName, BDInfoReportFormat.Json, compress: true);
                case 5:
                    return GuiReportExportOptions.CreateText(GetTextFileName(fileName));
                case 1:
                default:
                    return GuiReportExportOptions.CreateReport(fileName, BDInfoReportFormat.Xml, compress: false);
            }
        }

        private static string GetTextFileName(string fileName)
        {
            if (!string.Equals(Path.GetExtension(fileName), ".txt", StringComparison.OrdinalIgnoreCase))
            {
                return Path.ChangeExtension(fileName, ".txt");
            }

            return fileName;
        }
    }

    internal sealed class GuiReportExportOptions
    {
        private GuiReportExportOptions()
        {
        }

        public bool IsText { get; private set; }
        public string FileName { get; private set; }
        public BDInfoReportFormat Format { get; private set; }
        public bool Compress { get; private set; }

        public static GuiReportExportOptions CreateReport(string fileName, BDInfoReportFormat format, bool compress)
        {
            return new GuiReportExportOptions
            {
                IsText = false,
                FileName = fileName,
                Format = format,
                Compress = compress
            };
        }

        public static GuiReportExportOptions CreateText(string fileName)
        {
            return new GuiReportExportOptions
            {
                IsText = true,
                FileName = fileName,
                Format = BDInfoReportFormat.Xml,
                Compress = false
            };
        }
    }
}

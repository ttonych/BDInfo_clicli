using System;

namespace BDInfo
{
    internal static class ReportFileNameHelper
    {
        public static string CreateSafeBaseName(string volumeLabel, string emptyFallbackName, string unsafeFallbackName)
        {
            string baseName = string.IsNullOrWhiteSpace(volumeLabel)
                ? emptyFallbackName
                : volumeLabel;

            baseName = ToolBox.GetSafeFileName(baseName);
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = unsafeFallbackName;
            }

            return baseName;
        }

        public static string CreateGuiDefaultReportFileName(string volumeLabel)
        {
            return CreateSafeBaseName(volumeLabel, "BDINFO", "BDINFO") + ".bdinfo";
        }
    }
}

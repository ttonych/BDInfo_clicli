using System;
using BDInfo.Reporting;

namespace BDInfo
{
    internal static class GuiReportLoader
    {
        public static GuiReportLoadResult Load(string path)
        {
            BDInfoReportData reportData = BDInfoReportSerializer.Load(path);
            return FromReportData(reportData);
        }

        public static GuiReportLoadResult FromReportData(BDInfoReportData reportData)
        {
            if (reportData == null)
            {
                throw new ArgumentNullException(nameof(reportData));
            }

            BDROM bdrom = BDInfoReportSerializer.CreateBDROM(reportData);
            ScanBDROMResult scanResult = BDInfoReportSerializer.CreateScanResult(reportData.ScanResult);
            return new GuiReportLoadResult(bdrom, scanResult, reportData);
        }
    }

    internal sealed class GuiReportLoadResult
    {
        public GuiReportLoadResult(BDROM bdrom, ScanBDROMResult scanResult, BDInfoReportData reportData)
        {
            BDROM = bdrom ?? throw new ArgumentNullException(nameof(bdrom));
            ScanResult = scanResult ?? new ScanBDROMResult();
            ReportData = reportData ?? throw new ArgumentNullException(nameof(reportData));
        }

        public BDROM BDROM { get; private set; }
        public ScanBDROMResult ScanResult { get; private set; }
        public BDInfoReportData ReportData { get; private set; }
    }
}

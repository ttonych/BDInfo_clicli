using System;
using System.Drawing.Imaging;
using System.Globalization;

namespace BDInfo
{
    internal static class CliChartFormat
    {
        public static CliChartImageFormat Resolve(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return new CliChartImageFormat(ImageFormat.Png, ".png");
            }

            CliChartImageFormat format;
            if (TryResolve(name, out format))
            {
                return format;
            }

            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Unsupported chart image format: {0}", name));
        }

        public static bool IsKnownName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            CliChartImageFormat unused;
            return TryResolve(name, out unused);
        }

        private static bool TryResolve(string name, out CliChartImageFormat format)
        {
            switch (name.Trim().ToLowerInvariant())
            {
                case "png":
                    format = new CliChartImageFormat(ImageFormat.Png, ".png");
                    return true;
                case "jpg":
                case "jpeg":
                    format = new CliChartImageFormat(ImageFormat.Jpeg, ".jpg");
                    return true;
                case "bmp":
                    format = new CliChartImageFormat(ImageFormat.Bmp, ".bmp");
                    return true;
                case "gif":
                    format = new CliChartImageFormat(ImageFormat.Gif, ".gif");
                    return true;
                case "tif":
                case "tiff":
                    format = new CliChartImageFormat(ImageFormat.Tiff, ".tiff");
                    return true;
                default:
                    format = null;
                    return false;
            }
        }
    }

    internal sealed class CliChartImageFormat
    {
        public CliChartImageFormat(ImageFormat imageFormat, string extension)
        {
            ImageFormat = imageFormat;
            Extension = extension;
        }

        public ImageFormat ImageFormat { get; private set; }
        public string Extension { get; private set; }
    }
}

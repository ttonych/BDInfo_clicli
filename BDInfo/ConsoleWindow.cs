using System;
using System.Runtime.InteropServices;

namespace BDInfo
{
    internal static class ConsoleWindow
    {
        private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        public static IDisposable EnsureAttached()
        {
            if (HasConsole())
            {
                return NoOp.Instance;
            }

            if (!AttachConsole(ATTACH_PARENT_PROCESS) && !AllocConsole())
            {
                return NoOp.Instance;
            }

            return new ConsoleScope();
        }

        private static bool HasConsole()
        {
            return GetConsoleWindow() != IntPtr.Zero;
        }

        private sealed class ConsoleScope : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                try
                {
                    Console.Out.Flush();
                }
                catch
                {
                }

                try
                {
                    Console.Error.Flush();
                }
                catch
                {
                }

                FreeConsole();
            }
        }

        private sealed class NoOp : IDisposable
        {
            public static readonly NoOp Instance = new NoOp();

            private NoOp()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}

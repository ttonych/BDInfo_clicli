using System;
using System.IO;
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

            if (AttachConsole(ATTACH_PARENT_PROCESS))
            {
                ReinitializeConsoleStreams();
                return new ConsoleScope(detachOnDispose: false);
            }

            if (AllocConsole())
            {
                ReinitializeConsoleStreams();
                return new ConsoleScope(detachOnDispose: true);
            }

            return NoOp.Instance;
        }

        private static bool HasConsole()
        {
            return GetConsoleWindow() != IntPtr.Zero;
        }

        private static void ReinitializeConsoleStreams()
        {
            try
            {
                Stream outputStream = Console.OpenStandardOutput();
                var outputWriter = new StreamWriter(outputStream, Console.OutputEncoding)
                {
                    AutoFlush = true
                };
                Console.SetOut(outputWriter);
            }
            catch
            {
            }

            try
            {
                Stream errorStream = Console.OpenStandardError();
                var errorWriter = new StreamWriter(errorStream, Console.OutputEncoding)
                {
                    AutoFlush = true
                };
                Console.SetError(errorWriter);
            }
            catch
            {
            }

            try
            {
                Stream inputStream = Console.OpenStandardInput();
                var inputReader = new StreamReader(inputStream, Console.InputEncoding);
                Console.SetIn(inputReader);
            }
            catch
            {
            }
        }

        private sealed class ConsoleScope : IDisposable
        {
            private bool _disposed;
            private readonly bool _detachOnDispose;

            public ConsoleScope(bool detachOnDispose)
            {
                _detachOnDispose = detachOnDispose;
            }

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

                if (_detachOnDispose)
                {
                    FreeConsole();
                }
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

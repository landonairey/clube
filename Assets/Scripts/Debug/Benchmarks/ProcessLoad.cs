using System;
using System.Runtime.InteropServices;

namespace Clube.Debug
{
    /// <summary>
    /// How many processor cores the game kept busy over a stretch (GL770): the whole process and the main thread, from
    /// Windows' own accounts, so it also works in a release build. What's left is the render thread and the job workers.
    /// </summary>
    internal sealed class ProcessLoad
    {
        private const double TicksPerSecond = 1e7;

        private long processStart, mainStart, wallStart;

        /// <summary>Starts a new stretch; call it on the main thread.</summary>
        public void Clear()
        {
            processStart = ProcessTicks();
            mainStart = MainThreadTicks();
            wallStart = DateTime.UtcNow.Ticks;
        }

        /// <summary>The cores kept busy since <see cref="Clear"/>; call it on the main thread.</summary>
        public string Report()
        {
            double seconds = Math.Max(1e-3, (DateTime.UtcNow.Ticks - wallStart) / TicksPerSecond);
            double process = (ProcessTicks() - processStart) / TicksPerSecond / seconds;
            double main = (MainThreadTicks() - mainStart) / TicksPerSecond / seconds;
            return $"cores busy {process:0.00} of {Environment.ProcessorCount}: main thread {main:0.00}, render thread and job workers {process - main:0.00}";
        }

        private static long ProcessTicks()
        {
            return GetProcessTimes(GetCurrentProcess(), out _, out _, out long kernel, out long user) ? kernel + user : 0L;
        }

        private static long MainThreadTicks()
        {
            return GetThreadTimes(GetCurrentThread(), out _, out _, out long kernel, out long user) ? kernel + user : 0L;
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentThread();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessTimes(IntPtr process, out long creation, out long exit, out long kernel, out long user);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetThreadTimes(IntPtr thread, out long creation, out long exit, out long kernel, out long user);
    }
}

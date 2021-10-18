using System;
using System.Runtime.InteropServices;

namespace TestWindowsClockSync
{
    /// <summary>
    /// Set the windows multimedia timer to a specific accuracy.
    /// </summary>
    internal class TimePeriod : IDisposable
    {
        private static readonly TimeCaps time_capabilities;

        private readonly int period;

        [DllImport(@"winmm.dll", ExactSpelling = true)]
        private static extern int timeGetDevCaps(ref TimeCaps ptc, int cbtc);

        [DllImport(@"winmm.dll", ExactSpelling = true)]
        private static extern int timeBeginPeriod(int uPeriod);

        [DllImport(@"winmm.dll", ExactSpelling = true)]
        private static extern int timeEndPeriod(int uPeriod);

        internal static int MinimumPeriod => time_capabilities.wPeriodMin;
        internal static int MaximumPeriod => time_capabilities.wPeriodMax;

        private readonly bool didAdjust;

        static TimePeriod()
        {
            timeGetDevCaps(ref time_capabilities, Marshal.SizeOf(typeof(TimeCaps)));
        }

        internal TimePeriod(int period)
        {
            this.period = period;

            if (MaximumPeriod <= 0)
                return;

            didAdjust = timeBeginPeriod(Math.Clamp(period, MinimumPeriod, MaximumPeriod)) == 0;
        }

        #region IDisposable Support

        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                disposedValue = true;

                if (!didAdjust)
                    return;

                timeEndPeriod(period);
            }
        }

        ~TimePeriod()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct TimeCaps
        {
            internal readonly int wPeriodMin;
            internal readonly int wPeriodMax;
        }
    }
}

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedBass;

namespace TestWindowsClockSync
{
    static class Program
    {
        const int run_time = 120000;

        private static string log_prefix;

        static void Main()
        {
            log_prefix = $"run_{DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}_";

            run("baseline");

            using (new TimePeriod(1))
                run("timeperiod");
        }

        private static void run(string runName)
        {
            ManualResetEventSlim startEvent = new ManualResetEventSlim();
            var cts = new CancellationTokenSource(run_time);
            ClockTest[] tests =
            {
                new StopwatchClockTest(startEvent, cts.Token),
                new SystemClockTest(startEvent, cts.Token),
                new BassTest("5minutesofsilence.mp3", startEvent, cts.Token),
                new BassTest("5minutesofsilence_vbr.mp3", startEvent, cts.Token)
            };

            foreach (var t in tests)
                t.IsReady.Wait(cts.Token);

            startEvent.Set();


            while (!tests.All(t => t.RunTask.IsCompleted))
            {
                log(runName);

                for (var i = 0; i < tests.Length; i++)
                {
                    var t = tests[i];
                    log(runName, $"{i} [{t.GetType().Name}]: {t.CurrentSeconds}");
                }

                Thread.Sleep(1000);
            }
        }

        static void log(string runName, string logText = "")
        {
            Console.WriteLine(logText);
            File.AppendAllText(log_prefix + runName, $"{logText}\n");
        }
    }

    class BassTest : ClockTest
    {
        private int handle;

        private readonly string filename;

        public BassTest(string filename, ManualResetEventSlim startEvent, CancellationToken cancellationToken) : base(startEvent, cancellationToken)
        {
            this.filename = filename;
        }

        #region Overrides of ClockTest

        public override double CurrentSeconds => Bass.ChannelBytes2Seconds(handle, Bass.ChannelGetPosition(handle));

        protected override void Run()
        {
            byte[] bytes = File.ReadAllBytes(filename);

            Bass.Init(0);
            handle = Bass.CreateStream(bytes, 0, bytes.Length, BassFlags.Default);

            WaitForSyncedStart();

            Bass.ChannelPlay(handle);
        }

        #endregion
    }

    class SystemClockTest : ClockTest
    {
        private DateTimeOffset startTime;

        public SystemClockTest(ManualResetEventSlim startEvent, CancellationToken cancellationToken)
            : base(startEvent, cancellationToken)
        {
        }

        #region Overrides of ClockTest

        public override double CurrentSeconds => (DateTimeOffset.Now - startTime).TotalMilliseconds / 1000;

        protected override void Run()
        {
            WaitForSyncedStart();

            startTime = DateTimeOffset.Now;

            while (!CancellationToken.IsCancellationRequested)
                Thread.Sleep(100);
        }

        #endregion
    }

    class StopwatchClockTest : ClockTest
    {
        private Stopwatch stopwatch;

        public StopwatchClockTest(ManualResetEventSlim startEvent, CancellationToken cancellationToken)
            : base(startEvent, cancellationToken)
        {
        }

        #region Overrides of ClockTest

        public override double CurrentSeconds => (double)stopwatch.ElapsedMilliseconds / 1000;

        protected override void Run()
        {
            stopwatch = new Stopwatch();

            WaitForSyncedStart();

            stopwatch.Start();

            while (!CancellationToken.IsCancellationRequested)
                Thread.Sleep(100);
        }

        #endregion
    }

    public abstract class ClockTest
    {
        public readonly ManualResetEventSlim IsReady = new ManualResetEventSlim();

        protected readonly CancellationToken CancellationToken;

        private readonly ManualResetEventSlim startEvent;

        protected ClockTest(ManualResetEventSlim startEvent, CancellationToken cancellationToken)
        {
            this.startEvent = startEvent;
            this.CancellationToken = cancellationToken;

            RunTask = Task.Factory.StartNew(Run, TaskCreationOptions.LongRunning);
        }

        public Task RunTask { get; private set; }

        public abstract double CurrentSeconds { get; }

        protected abstract void Run();

        protected void WaitForSyncedStart()
        {
            IsReady.Set();
            startEvent.Wait(CancellationToken);
        }
    }
}

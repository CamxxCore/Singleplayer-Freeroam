using System;
using System.Diagnostics;

namespace SPFServer
{
    internal class TimeCycleManager
    {
        private ServerStopwatch svClock;

        public DateTime CurrentTime
        {
            get { return new DateTime(svClock.Elapsed.Ticks); }

            set
            {
                svClock.Elapsed = TimeSpan.FromTicks(value.Ticks);
            }
        }

        public TimeCycleManager()
        {
            svClock = new ServerStopwatch();
            svClock.Start();
        }

        public TimeCycleManager(DateTime time)
        {
            svClock = new ServerStopwatch();
            svClock.Start();
            CurrentTime = time;
        }
    }

    internal class ServerStopwatch
    {
        private Stopwatch _stopwatch = null;
        TimeSpan _offsetTimeSpan;

        public ServerStopwatch(TimeSpan offset)
        {
            _offsetTimeSpan = offset;
            _stopwatch = new Stopwatch();
        }

        public ServerStopwatch()
        {
            _offsetTimeSpan = new TimeSpan();
            _stopwatch = new Stopwatch();
        }

        public void Start()
        {
            _stopwatch.Start();
        }

        public void Stop()
        {
            _stopwatch.Stop();
        }

        public TimeSpan Elapsed
        {
            get
            {
                return _stopwatch.Elapsed + _offsetTimeSpan;
            }
            set
            {
                _offsetTimeSpan = value;
            }
        }
    }
}

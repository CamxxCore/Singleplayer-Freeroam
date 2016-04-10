using System;
using System.Diagnostics;

namespace SPFServer.Main
{
    internal class TimeCycle
    {
        private ServerClock svClock;

        public DateTime CurrentTime
        {
            get { return new DateTime(svClock.Elapsed.Ticks); }

            set
            {
                svClock.Elapsed = TimeSpan.FromTicks(value.Ticks);
            }
        }

        public TimeCycle()
        {
            svClock = new ServerClock();
            svClock.Start();
        }

        public TimeCycle(DateTime time)
        {
            svClock = new ServerClock();
            svClock.Start();
            CurrentTime = time;
        }
    }

    internal class ServerClock
    {
        private Stopwatch _stopwatch = null;
        TimeSpan _offsetTimeSpan;

        public ServerClock(TimeSpan offset)
        {
            _offsetTimeSpan = offset;
            _stopwatch = new Stopwatch();
        }

        public ServerClock()
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

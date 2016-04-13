using System;
using System.Diagnostics;

namespace SPFServer.Main
{
    public delegate void TimeChangedHandler(DateTime oldTime, DateTime newTime);

    public class ServerTime
    {
        private ServerClock svClock;

        public event TimeChangedHandler OnTimeChanged;

        public DateTime CurrentTime
        {
            get { return new DateTime(svClock.Elapsed.Ticks); }       
        }

        public void SetTime(TimeSpan time)
        {
            var lastTime = CurrentTime;
            svClock.Elapsed = time;
            OnTimeChanged?.Invoke(lastTime, CurrentTime);
        }

        public ServerTime()
        {
            svClock = new ServerClock();
            svClock.Start();
        }

        public ServerTime(DateTime time)
        {
            svClock = new ServerClock();
            svClock.Start();
            svClock.Elapsed = TimeSpan.FromTicks(time.Ticks);
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

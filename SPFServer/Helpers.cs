using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPFServer
{
    public static class Helpers
    {
        public static TimeSpan CalculateAverage(ref List<TimeSpan> samples, int minSamples = 5)
        {
            var avgHalfLat = 0d;
            var hLength = samples.Count;

            if (hLength > minSamples)
            {
                //find the median and discard anomalous samples
                samples.Sort();

                var midpoint = (int)(hLength * 0.5);
                var median = samples[midpoint].TotalMilliseconds / 1000;
                var stddev = 0d;

                foreach (var hL in samples)
                {
                    var ms = (hL.TotalMilliseconds / 1000);
                    stddev += (ms - median) * (ms - median);
                }

                stddev /= hLength;
                stddev = Math.Sqrt(stddev);

                var stdmin = median - stddev;
                var stdmax = median + stddev;

                for (int i = hLength - 1; i >= 0; i--)
                {
                    var testval = (samples[i].TotalMilliseconds / 1000);
                    if ((testval < stdmin) || (testval > stdmax))
                    {
                        samples.RemoveAt(i);
                    }
                }
            }
            if (hLength > 0)
            {
                //calculate arithmetic mean
                foreach (var hL in samples)
                {
                    avgHalfLat += (hL.TotalMilliseconds / 1000);
                }
                avgHalfLat /= hLength;
            }
            while (hLength > minSamples)
            {
                samples.RemoveAt(0);
            }

            return TimeSpan.FromMilliseconds(avgHalfLat * 1000);
        }
    }
}

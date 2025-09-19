using Godelian.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Server
{
    internal static class ProgressEstimatorService
    {
        public static ulong StartingIndex { get; private set; }
        public static DateTime StartTime { get; private set; }
        public static ulong CurrentIndex { get; private set; }
        public static TimeSpan ElapsedTime => DateTime.UtcNow - StartTime;

        public static void Init(ulong startingIndex)
        {
            StartingIndex = startingIndex;
            StartTime = DateTime.UtcNow;
            CurrentIndex = startingIndex;
        }

        public static void Reset()
        {
            StartingIndex = IPAddressEnumerator.FirstIPIndex;
            StartTime = DateTime.UtcNow;
            CurrentIndex = IPAddressEnumerator.FirstIPIndex;
        }

        public static void UpdateCurrentIndex(ulong currentIndex)
        {
            CurrentIndex = currentIndex;
        }

        public static float GetPercentageProgress()
        {
            ulong totalRange = IPAddressEnumerator.LastIPIndex - IPAddressEnumerator.FirstIPIndex;

            ulong progressed = CurrentIndex - IPAddressEnumerator.FirstIPIndex;

            double fraction = (double)progressed / totalRange;
            return (float)(fraction * 100.0);
        }

        public static TimeSpan EstimateTimeRemaining()
        {
            ulong totalRange = IPAddressEnumerator.LastIPIndex - IPAddressEnumerator.FirstIPIndex;

            ulong processedSinceStart = CurrentIndex - StartingIndex;
            ulong processedFromBeginning = CurrentIndex - IPAddressEnumerator.FirstIPIndex;

            if (processedSinceStart == 0) return TimeSpan.MaxValue;

            double avgSecondsPerUnit = ElapsedTime.TotalSeconds / processedSinceStart;
            ulong remainingUnits = totalRange - processedFromBeginning;
            double remainingSeconds = avgSecondsPerUnit * remainingUnits;

            return TimeSpan.FromSeconds(remainingSeconds);
        }
    }
}

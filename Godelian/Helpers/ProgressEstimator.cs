using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Helpers
{
    internal static class ProgressEstimator
    {
        public static uint StartingIndex { get; private set; }
        public static DateTime StartTime { get; private set; }
        public static uint CurrentIndex { get; private set; }
        public static TimeSpan ElapsedTime => DateTime.UtcNow - StartTime;

        public static void Init(uint startingIndex)
        {
            StartingIndex = startingIndex;
            StartTime = DateTime.UtcNow;
            CurrentIndex = startingIndex;
        }

        public static void UpdateCurrentIndex(uint currentIndex)
        {
            CurrentIndex = currentIndex;
        }

        public static float GetPercentageProgress()
        {
            return (float)(CurrentIndex - IPAddressEnumerator.FirstIPIndex) / (IPAddressEnumerator.LastIPIndex - IPAddressEnumerator.FirstIPIndex) * 100.0f;
        }

        public static TimeSpan EstimateTimeRemaining()
        {
            double progress = (double)(CurrentIndex - IPAddressEnumerator.FirstIPIndex) / (IPAddressEnumerator.LastIPIndex - IPAddressEnumerator.FirstIPIndex);
            double estimatedTotalSeconds = ElapsedTime.TotalSeconds / progress;
            double remainingSeconds = estimatedTotalSeconds - ElapsedTime.TotalSeconds;
            return TimeSpan.FromSeconds(remainingSeconds);
        }
    }
}

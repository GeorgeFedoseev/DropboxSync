using System;
using System.Collections.Generic;
using System.Linq;

namespace DBXSync {

    public class TransferSpeedTracker {        

        private readonly int _sampleSize;
        private readonly TimeSpan _valueDelay;

        private DateTime _lastUpdateCalculated;
        private long _previousProgressBytes;

        private double _cachedSpeed;

        private Queue<Tuple<DateTime, long>> _changes = new Queue<Tuple<DateTime, long>>();

        public TransferSpeedTracker(int sampleSize, TimeSpan valueDelay) {
            _lastUpdateCalculated = DateTime.Now;
            _sampleSize = sampleSize;
            _valueDelay = valueDelay;
        }

        public void Reset() {
            _previousProgressBytes = 0;
        }

        public void SetBytesCompleted(long bytesReceived) {            

            long diff = bytesReceived - _previousProgressBytes;
            if (diff <= 0)
                return;

            _previousProgressBytes = bytesReceived;

            _changes.Enqueue(new Tuple<DateTime, long>(DateTime.Now, diff));
            while (_changes.Count > _sampleSize)
                _changes.Dequeue();
        }

        public static string FormatBytesPerSecond(double bytesPerSecond){
            var prefix = new[] { "", "K", "M", "G"};

            int index = 0;
            while (bytesPerSecond > 1024 && index < prefix.Length - 1) {
                bytesPerSecond /= 1024;
                index++;
            }

            int intLen = ((int) bytesPerSecond).ToString().Length;
            int decimals = 3 - intLen;
            if (decimals < 0)
                decimals = 0;

            string format = String.Format("{{0:F{0}}}", decimals) + "{1}B/s";

            return String.Format(format, bytesPerSecond, prefix[index]);
        }
        

        public string GetSpeedString() {
            double speed = GetBytesPerSecond();
            return FormatBytesPerSecond(speed);
        }

        public double GetBytesPerSecond() {
            if (DateTime.Now >= _lastUpdateCalculated + _valueDelay)
            {
                _lastUpdateCalculated = DateTime.Now;
                _cachedSpeed = _GetRate();
            }

            return _cachedSpeed;
        }

        private double _GetRate() {
            if (_changes.Count == 0)
                return 0;

            TimeSpan timespan = _changes.Last().Item1 - _changes.First().Item1;
            long bytes = _changes.Sum(t => t.Item2);

            double rate = bytes / timespan.TotalSeconds;

            if (double.IsInfinity(rate) || double.IsNaN(rate))
                return 0;

            return rate;
        }
    }

}
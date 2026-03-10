using System;

namespace Garden
{
    public static class GameTime
    {
        private static DateTime _referenceRealTime = DateTime.UtcNow;
        private static DateTime _referenceGameTime = DateTime.UtcNow;
        private static float _timeScale = 1f;

        public static DateTime UtcNow
        {
            get
            {
                var realElapsed = DateTime.UtcNow - _referenceRealTime;
                var scaledTicks = (long)(realElapsed.Ticks * _timeScale);
                return _referenceGameTime + TimeSpan.FromTicks(scaledTicks);
            }
        }

        public static DateTime Now => UtcNow.ToLocalTime();

        public static float TimeScale
        {
            get => _timeScale;
            set
            {
                // Snapshot current game time before changing scale
                _referenceGameTime = UtcNow;
                _referenceRealTime = DateTime.UtcNow;
                _timeScale = value;
            }
        }

        public static void ResetTimeScale()
        {
            _referenceRealTime = DateTime.UtcNow;
            _referenceGameTime = DateTime.UtcNow;
            _timeScale = 1f;
        }
    }
}

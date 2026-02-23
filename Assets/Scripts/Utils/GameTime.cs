using System;

namespace Garden
{
    public static class GameTime
    {
        private static TimeSpan debugOffset = TimeSpan.Zero;
        public static bool IsOverridden => debugOffset != TimeSpan.Zero;

        public static DateTime UtcNow => DateTime.UtcNow + debugOffset;
        public static DateTime Now => DateTime.Now + debugOffset;

        public static void SetOverride(DateTime targetLocal)
        {
            debugOffset = targetLocal - DateTime.Now;
        }

        public static void ClearOverride()
        {
            debugOffset = TimeSpan.Zero;
        }
    }
}

using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Garden
{
    /// <summary>
    /// Centralized boot timing. Services call Mark() at key points during init.
    /// Prints a consolidated timeline when Complete() is called.
    /// </summary>
    public static class BootTimer
    {
        private static readonly Stopwatch _sw = Stopwatch.StartNew();
        private static readonly List<(long ms, string label)> _marks = new();
        private static bool _completed;

        public static void Mark(string label)
        {
            long ms = _sw.ElapsedMilliseconds;
            lock (_marks) _marks.Add((ms, label));
            Debug.Log($"[BOOT +{ms}ms] {label}");
        }

        public static void Complete()
        {
            if (_completed) return;
            _completed = true;

            long total = _sw.ElapsedMilliseconds;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[BOOT] ===== Boot timeline ({total}ms total) =====");

            lock (_marks)
            {
                long prev = 0;
                foreach (var (ms, label) in _marks)
                {
                    long delta = ms - prev;
                    sb.AppendLine($"  +{ms,5}ms (+{delta,4}ms) {label}");
                    prev = ms;
                }
            }

            sb.Append($"  +{total,5}ms        COMPLETE");
            Debug.Log(sb.ToString());
        }
    }
}

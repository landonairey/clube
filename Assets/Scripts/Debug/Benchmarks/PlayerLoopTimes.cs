using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine.LowLevel;

namespace Clube.Debug
{
    /// <summary>
    /// What each stage of the player loop took (GL770), timed by wrapping the stages themselves, so it also works in a
    /// release build, where the engine's profiler markers are switched off.
    /// </summary>
    internal sealed class PlayerLoopTimes : IDisposable
    {
        private readonly List<string> names = new List<string>();
        private readonly List<long> ticks = new List<long>();
        private PlayerLoopSystem original;
        private bool installed;
        private long started;

        /// <summary>Starts timing every stage.</summary>
        public void Install()
        {
            original = PlayerLoop.GetCurrentPlayerLoop();
            PlayerLoopSystem root = original;
            var phases = (PlayerLoopSystem[])root.subSystemList.Clone();
            for (int p = 0; p < phases.Length; p++)
            {
                if (phases[p].subSystemList == null)
                {
                    continue;
                }
                var wrapped = new List<PlayerLoopSystem>();
                foreach (PlayerLoopSystem stage in phases[p].subSystemList)
                {
                    int slot = names.Count;
                    names.Add($"{phases[p].type?.Name}.{stage.type?.Name}");
                    ticks.Add(0L);
                    wrapped.Add(new PlayerLoopSystem { type = typeof(PlayerLoopTimes), updateDelegate = () => started = Stopwatch.GetTimestamp() });
                    wrapped.Add(stage);
                    wrapped.Add(new PlayerLoopSystem { type = typeof(PlayerLoopTimes), updateDelegate = () => ticks[slot] += Stopwatch.GetTimestamp() - started });
                }
                phases[p].subSystemList = wrapped.ToArray();
            }
            root.subSystemList = phases;
            PlayerLoop.SetPlayerLoop(root);
            installed = true;
        }

        /// <summary>The costliest stages since <see cref="Clear"/>, in milliseconds a frame.</summary>
        public string Top(int count, int frames)
        {
            if (frames == 0)
            {
                return "";
            }
            double toMilliseconds = 1000.0 / Stopwatch.Frequency / frames;
            return string.Join("; ", Enumerable.Range(0, names.Count).OrderByDescending(i => ticks[i]).Take(count)
                .Select(i => $"{names[i]} {ticks[i] * toMilliseconds:0.00}"));
        }

        /// <summary>Starts a new stretch.</summary>
        public void Clear()
        {
            for (int i = 0; i < ticks.Count; i++)
            {
                ticks[i] = 0L;
            }
        }

        public void Dispose()
        {
            if (installed)
            {
                PlayerLoop.SetPlayerLoop(original);
                installed = false;
            }
        }
    }
}

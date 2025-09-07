#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace Benchmark.Platforms
{
    public class EditorPlatformServices : IPlatformServices
    {
        public string ResultsRoot => System.IO.Path.GetFullPath("./");
        public void Log(string msg) => Debug.Log($"[Editor] {msg}");

        public IMetricsSampler CreateSampler() => new EditorSampler();

        public System.Collections.IEnumerator CleanupOnce(bool forceGc, bool unloadUnusedAssets)
        {
            if (forceGc)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            if (unloadUnusedAssets)
            {
                var op = Resources.UnloadUnusedAssets();
                while (!op.isDone) yield return null;
            }

            yield return null;
        }

        private class EditorSampler : IMetricsSampler
        {
            public void Start()
            {
            }

            public MemoryPoint Read()
            {
                double MB(long b) => b / (1024.0 * 1024.0);
                return new MemoryPoint
                {
                    allocatedMB = MB(Profiler.GetTotalAllocatedMemoryLong()),
                    reservedMB = MB(Profiler.GetTotalReservedMemoryLong()),
                    monoMB = MB(Profiler.GetMonoUsedSizeLong()),
                    systemUsedMB = 0
                };
            }

            public void Dispose()
            {
            }
        }
    }
}


#endif

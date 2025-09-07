using System;
using UnityEngine;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

namespace Benchmark.Platforms
{

    public class PlayerPlatformServices : IPlatformServices
    {
        public string ResultsRoot => Application.persistentDataPath;
        public void Log(string msg) => Debug.Log(msg);

        public IMetricsSampler CreateSampler() => new PlayerSampler();

        public System.Collections.IEnumerator CleanupOnce(bool forceGc, bool unloadUnusedAssets)
        {
            if (forceGc) { GC.Collect(); GC.WaitForPendingFinalizers(); }
            if (unloadUnusedAssets) { var op = Resources.UnloadUnusedAssets(); while (!op.isDone) yield return null; }
            yield return null;
        }

        private class PlayerSampler : IMetricsSampler
        {
            ProfilerRecorder _reserved, _mono, _systemUsed, _allocated;
            public void Start()
            {
                _reserved   = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory");
                _allocated  = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
                _mono       = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
                _systemUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            }
            public MemoryPoint Read()
            {
                double Mb(long bytes) => bytes / (1024.0 * 1024.0);
                var p = new MemoryPoint
                {
                    allocatedMB   = _allocated.Valid ? Mb(_allocated.LastValue) : 0,
                    reservedMB    = _reserved.Valid ? Mb(_reserved.LastValue) : 0,
                    monoMB        = _mono.Valid ? Mb(_mono.LastValue) : 0,
                    systemUsedMB  = _systemUsed.Valid ? Mb(_systemUsed.LastValue) : 0
                };
                return p;
            }
            public void Dispose()
            {
                _reserved.Dispose(); _mono.Dispose(); _systemUsed.Dispose(); _allocated.Dispose();
            }
        }
    }

}
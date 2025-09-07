using System.Collections.Generic;
using UnityEngine;

namespace Benchmark
{
    public enum RunMode
    {
        Auto,
        Editor,
        Player
    }

    [CreateAssetMenu(menuName = "Benchmark/Config")]
    public class BenchmarkConfig : ScriptableObject
    {
        public RunMode runMode = RunMode.Auto;

        public List<string> scenes = new();
        
        [Header("Auto-Sync")]
        public bool autoSyncScenes = true;
        public bool includeDisabledScenes = false;

        [Header("Run Parameters")] 
        public int iterations = 3;
        public int sampleIntervalMs = 50;
        public int stabilizationMs = 1000;
        public bool unloadUnusedAssets = true;
        public bool forceGCBeforeRun = true;
        public bool buildLikePlayer = false;
        public bool autostartInPlayer = false;

#if UNITY_EDITOR
        private void Reset()
        {
            TryAutoSync();
        }

        private void OnValidate()
        {
            TryAutoSync();
        }

        [ContextMenu("Sync Scenes from Build Settings")]
        public void SyncScenesFromMenu()
        {
            SyncScenesFromBuildSettings(includeDisabledScenes);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private void TryAutoSync()
        {
            if (!autoSyncScenes) return;

            if (scenes == null) scenes = new List<string>();
            if (scenes.Count == 0 || !ScenesMatchBuildSettings())
                SyncScenesFromBuildSettings(includeDisabledScenes);
        }

        private bool ScenesMatchBuildSettings()
        {
            var buildScenes = GetBuildSettingsSceneNames(includeDisabledScenes);
            if (buildScenes.Count != scenes.Count) return false;
            for (int i = 0; i < buildScenes.Count; i++)
                if (buildScenes[i] != scenes[i])
                    return false;
            return true;
        }

        private void SyncScenesFromBuildSettings(bool includeDisabled)
        {
            scenes.Clear();
            var buildScenes = GetBuildSettingsSceneNames(includeDisabled);
            scenes.AddRange(buildScenes);
        }

        private static List<string> GetBuildSettingsSceneNames(bool includeDisabled)
        {
            var result = new List<string>();
            foreach (var s in UnityEditor.EditorBuildSettings.scenes)
            {
                if (!includeDisabled && !s.enabled) continue;
                var name = System.IO.Path.GetFileNameWithoutExtension(s.path);
                if (!string.IsNullOrEmpty(name)) result.Add(name);
            }

            return result;
        }
#endif
    }
}
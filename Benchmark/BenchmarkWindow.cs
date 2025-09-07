#if UNITY_EDITOR
using Benchmark.Platforms;
using UnityEditor;
using UnityEngine;

namespace Benchmark
{
    public class BenchmarkWindow : EditorWindow
    {
        [SerializeField] private BenchmarkConfig config;

        private const string PrefKeyConfigPath = "BenchmarkRunner_ConfigAssetPath";
        private const string PrefKeyPendingRun = "BenchmarkRunner_PendingRun";


        [MenuItem("Tools/Benchmark Runner")]
        public static void Open() => GetWindow<BenchmarkWindow>("Benchmark Runner");

        private void OnEnable()
        {
            var path = EditorPrefs.GetString(PrefKeyConfigPath, "");
            if (!string.IsNullOrEmpty(path))
                config = AssetDatabase.LoadAssetAtPath<BenchmarkConfig>(path);

            EditorApplication.playModeStateChanged += OnPlaymodeChanged;
        }

        private void OnDisable() => EditorApplication.playModeStateChanged -= OnPlaymodeChanged;

        private void OnGUI()
        {
            config = (BenchmarkConfig)EditorGUILayout.ObjectField("Config", config, typeof(BenchmarkConfig), false);
            if (config) EditorPrefs.SetString(PrefKeyConfigPath, AssetDatabase.GetAssetPath(config));

            GUILayout.Space(8);

            using (new EditorGUI.DisabledScope(config == null))
            {
                if (GUILayout.Button("Run Benchmark"))
                    RunFromEditorByButton();
            }
        }

        private void RunFromEditorByButton()
        {
            if (EditorApplication.isPlaying)
            {
                LaunchRunnerInPlayMode();
                return;
            }

            EditorPrefs.SetBool(PrefKeyPendingRun, true);
            EditorApplication.EnterPlaymode();
        }

        private void OnPlaymodeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;

            bool shouldRun = EditorPrefs.GetBool(PrefKeyPendingRun, false);
            if (!shouldRun) return;

            EditorPrefs.DeleteKey(PrefKeyPendingRun);

            LaunchRunnerInPlayMode();
        }

        private void LaunchRunnerInPlayMode()
        {
            if (config == null) return;

            var go = new GameObject("BenchmarkRunner(Editor)");
            var runner = go.AddComponent<BenchmarkRunner>();
            DontDestroyOnLoad(runner);
            runner.Init(new EditorPlatformServices(), config);
        }
    }
}
#endif
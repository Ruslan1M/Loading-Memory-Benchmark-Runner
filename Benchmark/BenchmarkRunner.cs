using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Benchmark.Benchmark;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Benchmark
{
    [Serializable]
    public struct ResultRow
    {
        public string scene;
        public int iteration;
        public double load90Ms;
        public double loadDoneMs;
        public double peakAllocatedMB;
        public double peakReservedMB;
        public double peakMonoMB;
        public double peakSystemMB;
        public double steadyAllocatedMB;
        public double steadyReservedMB;
        public double steadyMonoMB;
        public double steadySystemMB;
        public string graphPath;
    }

    public class BenchmarkRunner : MonoBehaviour
    {
        public BenchmarkConfig config;

        private IPlatformServices _platform;
        private string _runDir;
        private string _graphsDir;
        private StreamWriter _csv;

        private GUIStyle _headerStyle;
        private GUIStyle _cellHeader;
        private GUIStyle _smallGrey;

        private Vector2 _scroll;

        private readonly List<ResultRow> _rows = new();

        public void Init(IPlatformServices platform, BenchmarkConfig cfg)
        {
            _platform = platform;
            config = cfg;
        }

        private IEnumerator Start()
        {
            if (config == null) yield break;

            var ts = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _runDir = Path.Combine(_platform.ResultsRoot, $"BenchmarkResults/{ts}");
            Directory.CreateDirectory(_runDir);

            _graphsDir = Path.Combine(_runDir, "Graphs");
            Directory.CreateDirectory(_graphsDir);

            _csv = new StreamWriter(Path.Combine(_runDir, "metrics.csv"), false, Encoding.UTF8);
            _csv.WriteLine(
                "runId,scene,iteration,timestampMs,phase,loadMs,allocatedMB,reservedMB,monoMB,systemUsedMB,peakAllocatedMB,peakReservedMB,peakMonoMB");

            yield return _platform.CleanupOnce(config.forceGCBeforeRun, config.unloadUnusedAssets);

            for (int s = 0; s < config.scenes.Count; s++)
            {
                var sceneName = config.scenes[s];
                for (int it = 1; it <= config.iterations; it++)
                {
                    yield return RunOne(sceneName, it);
                    yield return _platform.CleanupOnce(false, config.unloadUnusedAssets);
                }
            }

            _csv.Flush();
            _csv.Close();
            _platform.Log($"Benchmark done. Results: {_runDir}");
        }

        private IEnumerator RunOne(string sceneName, int iteration)
        {
            using var sampler = _platform.CreateSampler();
            sampler.Start();

            var samples = new List<MemoryPoint>(1024);
            var sw = new Stopwatch();
            sw.Start();

            var sampleInterval = Mathf.Max(0.001f, config.sampleIntervalMs / 1000f);
            bool sampling = true;

            IEnumerator SampleLoop()
            {
                while (sampling)
                {
                    var p = sampler.Read();
                    p.timeMs = sw.Elapsed.TotalMilliseconds;
                    samples.Add(p);
                    yield return new WaitForSecondsRealtime(sampleInterval);
                }
            }

            var sampleRoutine = StartCoroutine(SampleLoop());

            var loadStart = sw.Elapsed.TotalMilliseconds;
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            op.allowSceneActivation = true;

            double tLoad90 = -1;
            while (!op.isDone)
            {
                if (tLoad90 < 0 && op.progress >= 0.9f)
                    tLoad90 = sw.Elapsed.TotalMilliseconds - loadStart;

                yield return null;
            }

            var loadDoneMs = sw.Elapsed.TotalMilliseconds - loadStart;

            var stab = 0.0;
            var stabTarget = Math.Max(0.0, config.stabilizationMs / 1000.0);
            while (stab < stabTarget)
            {
                stab += Time.unscaledDeltaTime;
                yield return null;
            }

            double peakAlloc = 0, peakRes = 0, peakMono = 0, peakSys = 0;
            foreach (var p in samples)
            {
                if (p.allocatedMB > peakAlloc) peakAlloc = p.allocatedMB;
                if (p.reservedMB > peakRes) peakRes = p.reservedMB;
                if (p.monoMB > peakMono) peakMono = p.monoMB;
                if (p.systemUsedMB > peakSys) peakSys = p.systemUsedMB;
            }

            string runId = $"{sceneName}_{iteration}";
            _csv.WriteLine($"{runId},{sceneName},{iteration},{(int)(loadStart)},Load90,{tLoad90:F1},,,,,,");
            _csv.WriteLine(
                $"{runId},{sceneName},{iteration},{(int)(loadStart + loadDoneMs)},LoadDone,{loadDoneMs:F1},,,,,,{peakAlloc:F2},{peakRes:F2},{peakMono:F2}");
            var last = samples.Count > 0 ? samples[^1] : default;
            _csv.WriteLine(
                $"{runId},{sceneName},{iteration},{(int)last.timeMs},PostActivate,,{last.allocatedMB:F2},{last.reservedMB:F2},{last.monoMB:F2},{last.systemUsedMB:F2},{peakAlloc:F2},{peakRes:F2},{peakMono:F2}");
            foreach (var p in samples)
                _csv.WriteLine(
                    $"{runId},{sceneName},{iteration},{(int)p.timeMs},Sample,,{p.allocatedMB:F2},{p.reservedMB:F2},{p.monoMB:F2},{p.systemUsedMB:F2},,,");
            _csv.Flush();

            string pngPath = Path.Combine(_graphsDir, $"{sceneName}_iter{iteration:D2}.png");
            try
            {
                GraphExporter.SaveMemoryGraph(
                    samples,
                    pngPath,
                    title: $"{sceneName} • iter {iteration} • Load: {loadDoneMs:F0} ms • PeakRes: {peakRes:F1} MB"
                );
            }
            catch (Exception e)
            {
                _platform.Log($"Graph export failed: {e.Message}");
            }

            _rows.Add(new ResultRow
            {
                scene = sceneName,
                iteration = iteration,
                load90Ms = tLoad90,
                loadDoneMs = loadDoneMs,
                peakAllocatedMB = peakAlloc,
                peakReservedMB = peakRes,
                peakMonoMB = peakMono,
                peakSystemMB = peakSys,
                steadyAllocatedMB = last.allocatedMB,
                steadyReservedMB = last.reservedMB,
                steadyMonoMB = last.monoMB,
                steadySystemMB = last.systemUsedMB,
                graphPath = pngPath
            });

            sampling = false;
            StopCoroutine(sampleRoutine);
            yield return null;
        }

        private void OnGUI()
        {
            if (_rows.Count == 0) return;

            var pad = 12f;
            var w = Screen.width - pad * 2;
            GUILayout.BeginArea(new Rect(pad, pad, w, Screen.height - pad * 2), GUI.skin.box);
            GUILayout.Label("Benchmark Results (live)", HeaderStyle());

            _scroll = GUILayout.BeginScrollView(_scroll);

            RowHeader(
                "Scene", "Iter",
                "Load90 ms", "LoadDone ms",
                "Peak Res MB", "Peak Alloc MB", "Peak Mono MB", "Peak Sys MB",
                "Steady Res", "Steady Alloc", "Steady Mono", "Steady Sys",
                "PNG"
            );

            foreach (var r in _rows)
            {
                Row(
                    r.scene, r.iteration.ToString(),
                    F(r.load90Ms), F(r.loadDoneMs),
                    F(r.peakReservedMB), F(r.peakAllocatedMB), F(r.peakMonoMB), F(r.peakSystemMB),
                    F(r.steadyReservedMB), F(r.steadyAllocatedMB), F(r.steadyMonoMB), F(r.steadySystemMB),
                    ShortPath(r.graphPath)
                );
            }

            GUILayout.EndScrollView();

            GUILayout.Space(6);
            GUILayout.Label($"Results folder: {_runDir}", SmallGrey());

            GUILayout.EndArea();
        }

        private string F(double v) => double.IsNaN(v) || double.IsInfinity(v) ? "-" : v.ToString("F1");

        private GUIStyle HeaderStyle()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 };
            }

            return _headerStyle;
        }


        private GUIStyle CellHeader()
        {
            if (_cellHeader == null)
            {
                _cellHeader = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            }

            return _cellHeader;
        }


        private GUIStyle SmallGrey()
        {
            if (_smallGrey == null)
            {
                _smallGrey = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = Color.gray } };
            }

            return _smallGrey;
        }

        private void RowHeader(params string[] cols)
        {
            using (new GUILayout.HorizontalScope())
            {
                foreach (var c in cols)
                    GUILayout.Label(c, CellHeader(), GUILayout.Width(ColumnWidth(c)));
                GUILayout.FlexibleSpace();
            }

            GUILayout.Box(GUIContent.none, GUILayout.ExpandWidth(true), GUILayout.Height(1));
        }

        private void Row(params string[] cols)
        {
            using (new GUILayout.HorizontalScope())
            {
                foreach (var c in cols)
                    GUILayout.Label(c, GUILayout.Width(ColumnWidth(c)));
                GUILayout.FlexibleSpace();
            }
        }

        private float ColumnWidth(string header)
        {
            switch (header)
            {
                case "Scene": return 140;
                case "PNG": return 220;
                case "Iter": return 44;
                default: return 96;
            }
        }

        private string ShortPath(string p)
        {
            if (string.IsNullOrEmpty(p)) return "-";
            try
            {
                return Path.GetFileName(p);
            }
            catch
            {
                return p;
            }
        }
    }
}
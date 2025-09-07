using UnityEngine;

namespace Benchmark
{
    public class BenchmarkBootstrap : MonoBehaviour
    {
        public BenchmarkConfig config;
        public bool runOnStartInPlayer = true;

        private void Start()
        {
            if (!Application.isEditor && (runOnStartInPlayer || (config && config.autostartInPlayer)))
                Launch();
        }

        public void Launch()
        {
            var go = new GameObject("BenchmarkRunner");
            var runner = go.AddComponent<BenchmarkRunner>();
            runner.Init(PlatformFactory.Create(config), config);
        }
    }

}
#if UNITY_EDITOR
using Benchmark;
using Benchmark.Platforms;

public static class PlatformFactory
{
    public static IPlatformServices Create(BenchmarkConfig cfg)
    {
#if UNITY_EDITOR
        if (cfg.runMode == RunMode.Editor || (cfg.runMode == RunMode.Auto))
            return new EditorPlatformServices();
        else
            return new PlayerPlatformServices();
#else
        return new PlayerPlatformServices();
#endif
    }
}
#endif
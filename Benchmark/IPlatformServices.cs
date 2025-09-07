namespace Benchmark
{
    public interface IPlatformServices
    {
        string ResultsRoot { get; }                 
        IMetricsSampler CreateSampler();            
        void Log(string msg);                        
        System.Collections.IEnumerator CleanupOnce(bool forceGc, bool unloadUnusedAssets);
    }
}
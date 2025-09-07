using System;

namespace Benchmark
{
    public interface IMetricsSampler : IDisposable
    {
        void Start();
        MemoryPoint Read();
    }
}
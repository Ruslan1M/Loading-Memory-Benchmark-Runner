using System;

namespace Benchmark
{
    [Serializable]
    public struct MemoryPoint
    {
        public double timeMs;
        public double allocatedMB;
        public double reservedMB;
        public double monoMB;    
        public double systemUsedMB;
    }
}
using BenchmarkDotNet.Running;

namespace Hive.Common.Benchmark;

public class Program
{
    public static void Main()
    {
        BenchmarkRunner.Run<ECSBenchmark>();
    }
}
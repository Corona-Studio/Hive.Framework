using BenchmarkDotNet.Running;

namespace Hive.Benchmark;

public class Program
{
    public static void Main()
    {
        BenchmarkRunner.Run<ECSBenchmark>();
    }
}
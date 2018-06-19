using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Benchmark.AspNetCore.WebSocketsCompression.Benchmarks;

namespace Benchmark.AspNetCore.WebSocketsCompression
{
    class Program
    {
        static void Main(string[] args)
        {
            Summary compressionWebSocketSummary = BenchmarkRunner.Run<CompressionWebSocketBenchmarks>();

            System.Console.ReadKey();
        }
    }
}

using BenchmarkDotNet.Running;
using BulkHoganBenchmark;

var summary = BenchmarkRunner.Run<CustomerServiceBenchmark>();
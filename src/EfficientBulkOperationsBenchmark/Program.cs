using BenchmarkDotNet.Running;
using EfficientBulkOperationsBenchmark;

var summary = BenchmarkRunner.Run<CustomerServiceBenchmark>();
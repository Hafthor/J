using BenchmarkDotNet.Running;
using com.hafthor.J;
using J;

JValue value = JValue.Parse(JTests.SampleDoc.AsMemory());
Console.WriteLine(value.ToString());
BenchmarkRunner.Run<JBenchmark>();
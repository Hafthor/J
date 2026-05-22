using BenchmarkDotNet.Running;
using com.hafthor.J;

JValue value = JValue.Parse(JTests.SampleDoc);
Console.WriteLine(value.ToString());
BenchmarkRunner.Run<JBenchmark>();
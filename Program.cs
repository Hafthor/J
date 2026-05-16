using BenchmarkDotNet.Running;
using com.hafthor.J;

JValue value = JValue.Parse(JBenchmark.d.AsMemory());
string s = value.ToString();
Console.WriteLine(s);
BenchmarkRunner.Run<JBenchmark>();
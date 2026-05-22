using BenchmarkDotNet.Attributes;

// ReSharper disable once CheckNamespace
namespace com.hafthor.J;

[MemoryDiagnoser(false)]
public class JBenchmark {
    private static readonly JValue J = JValue.Parse(JTests.SampleDoc);
    //private static readonly object J2 = Newtonsoft.Json.JsonConvert.DeserializeObject(JTests.SampleDoc);

    [Benchmark] // 984.9ns 4936B
    public JValue Parse() => JValue.Parse(JTests.SampleDoc);

    [Benchmark] // 147.4ns 824B
    public string Serialize() => J.ToString();

    // combined 1132.3ns 5760B

    //[Benchmark] // 1501.7ns 6664B - 1.525x time 1.350x mem
    //public object NParse() => Newtonsoft.Json.JsonConvert.DeserializeObject(JTests.SampleDoc);

    //[Benchmark] // 361.6ns 1728B - 2.453x time 2.097x mem
    //public string NSerialize() => Newtonsoft.Json.JsonConvert.SerializeObject(J2);

    // combined 1863.3ns 8392B - 1.647x time 1.456x mem / 39.28% less time 31.32% less mem
}
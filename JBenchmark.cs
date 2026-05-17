using BenchmarkDotNet.Attributes;

// ReSharper disable once CheckNamespace
namespace com.hafthor.J;

[MemoryDiagnoser(false)]
public class JBenchmark {
    public static readonly string D = """
                                      {
                                          "name": "John",
                                          "age": 30,
                                          "isStudent": false,
                                          "scores": [85, 90, 92],
                                          "address": {
                                              "street": "123 Main St",
                                              "city": "Anytown",
                                              "zip": "12345"
                                          }
                                      }
                                      """;

    private static readonly JValue J = JValue.Parse(D.AsMemory());
    //private static readonly object J2 = Newtonsoft.Json.JsonConvert.DeserializeObject(d);

    [Benchmark] // 984.9ns 4936B
    public JValue Parse() => JValue.Parse(D.AsMemory());

    [Benchmark] // 147.4ns 824B
    public string Serialize() => J.ToString();

    //[Benchmark] // 1501.7ns 6664B - 1.525x time 1.350x mem
    //public object NParse() => Newtonsoft.Json.JsonConvert.DeserializeObject(D);

    //[Benchmark] // 361.6ns 1728B - 2.453x time 2.097x mem
    //public string NSerialize() => Newtonsoft.Json.JsonConvert.SerializeObject(J2);
}
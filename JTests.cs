using com.hafthor.J;

namespace J;

[TestClass]
public sealed class JTests {
    public static readonly string SampleDoc = """
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

    [TestMethod]
    public void BasicSmokeTest() {
        JValue value = JValue.Parse(SampleDoc.AsMemory());
        Assert.IsNotNull(value);
        Assert.AreEqual(
            "{\"name\":\"John\",\"age\":30,\"isStudent\":false,\"scores\":[85,90,92],\"address\":{\"street\":\"123 Main St\",\"city\":\"Anytown\",\"zip\":\"12345\"}}",
            value.ToString());
        JObject jObj = (JObject)value;
        Assert.HasCount(5, jObj);

        JString name = (JString)jObj["name"];
        Assert.AreEqual("John", name.String().ToString());
        Assert.AreEqual(0, jObj.Offset);
        Assert.AreEqual(13 + Environment.NewLine.Length, name.Offset);

        JLiteral age = (JLiteral)jObj["age"];
        Assert.AreEqual("30", age.ToString());
        Assert.IsTrue(age.IsValidNumber);
        Assert.IsFalse(age.IsNull);
        Assert.IsFalse(age.IsTrue);
        Assert.IsFalse(age.IsFalse);

        JLiteral isStudent = (JLiteral)jObj["isStudent"];
        Assert.AreEqual("false", isStudent.ToString());
        Assert.IsTrue(isStudent.IsFalse);

        JArray scores = (JArray)jObj["scores"];
        Assert.HasCount(3, scores);
        Assert.AreEqual("85", scores[0].ToString());
        Assert.AreEqual("90", scores[1].ToString());
        Assert.AreEqual("92", scores[2].ToString());

        JObject address = (JObject)jObj["address"];
        Assert.HasCount(3, address);
        Assert.AreEqual("123 Main St", ((JString)address["street"]).String().ToString());
        Assert.AreEqual("Anytown", ((JString)address["city"]).String().ToString());
        Assert.AreEqual("12345", ((JString)address["zip"]).String().ToString());
        Assert.AreEqual(jObj, address["zip"].Root);
    }

    [TestMethod]
    public void EscapeTest() {
        string json = "{\"text\":\"Line1\\nLine2\\tTabbed\\\\Backslash\\\"Quote\\0\\u19Fa\\uAfg\\xfA\"}";
        JObject jObj = JValue.Parse(json.AsMemory()) as JObject;
        Assert.IsNotNull(jObj);
        JString text = jObj["text"] as JString;
        Assert.IsNotNull(text);
        Assert.AreEqual("Line1\nLine2\tTabbed\\Backslash\"Quote\0᧺¯gú", text.String().ToString());
    }

    [TestMethod]
    public void LiteralTests() {
        var t = JValue.Parse("true".AsMemory()) as JLiteral;
        var f = JValue.Parse("false".AsMemory()) as JLiteral;
        var n = JValue.Parse("null".AsMemory()) as JLiteral;
        var z = JValue.Parse("0".AsMemory()) as JLiteral;

        Assert.IsTrue(t.IsTrue);
        Assert.IsFalse(t.IsFalse);
        Assert.IsFalse(t.IsNull);
        Assert.IsFalse(t.IsValidNumber);

        Assert.IsFalse(f.IsTrue);
        Assert.IsTrue(f.IsFalse);
        Assert.IsFalse(f.IsNull);
        Assert.IsFalse(f.IsValidNumber);

        Assert.IsFalse(n.IsTrue);
        Assert.IsFalse(n.IsFalse);
        Assert.IsTrue(n.IsNull);
        Assert.IsFalse(n.IsValidNumber);

        Assert.IsFalse(z.IsTrue);
        Assert.IsFalse(z.IsFalse);
        Assert.IsFalse(z.IsNull);
        Assert.IsTrue(z.IsValidNumber);

        Assert.IsTrue((JValue.Parse("-123.456e+789".AsMemory()) as JLiteral)?.IsValidNumber);
        Assert.IsTrue((JValue.Parse("-.9e9".AsMemory()) as JLiteral)?.IsValidNumber);
        Assert.IsFalse((JValue.Parse("notaliteral".AsMemory()) as JLiteral)?.IsValidNumber);
        Assert.IsFalse((JValue.Parse("-.e".AsMemory()) as JLiteral)?.IsValidNumber);
        Assert.IsFalse((JValue.Parse("-".AsMemory()) as JLiteral)?.IsValidNumber);
        Assert.IsFalse((JValue.Parse(".e1".AsMemory()) as JLiteral)?.IsValidNumber);
        Assert.IsTrue((JValue.Parse("-0.e0".AsMemory()) as JLiteral)?.IsValidNumber);
    }
}
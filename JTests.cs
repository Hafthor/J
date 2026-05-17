using com.hafthor.J;

namespace J;

[TestClass]
public sealed class JTests {
    [TestMethod]
    public void BasicSmokeTest() {
        JValue value = JValue.Parse(JBenchmark.D.AsMemory());
        Assert.IsNotNull(value);
        Assert.AreEqual(
            "{\"name\":\"John\",\"age\":30,\"isStudent\":false,\"scores\":[85,90,92],\"address\":{\"street\":\"123 Main St\",\"city\":\"Anytown\",\"zip\":\"12345\"}}",
            value.ToString());
        JObject jObj = (JObject)value;
        Assert.HasCount(5, jObj);
        JString name = (JString)jObj["name"];
        Assert.AreEqual("John", name.String().ToString());
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
        Assert.AreEqual(0, jObj.Offset);
        Assert.AreEqual(14, name.Offset);
    }

    [TestMethod]
    public void EscapeTest() {
        string json = "{\"text\":\"Line1\\nLine2\\tTabbed\\\\Backslash\\\"Quote\"}";
        JObject jObj = JValue.Parse(json.AsMemory()) as JObject;
        Assert.IsNotNull(jObj);
        JString text = jObj["text"] as JString;
        Assert.IsNotNull(text);
        Assert.AreEqual("Line1\nLine2\tTabbed\\Backslash\"Quote", text.String().ToString());
    }
}
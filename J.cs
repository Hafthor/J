using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace com.hafthor.J;

public abstract class JValue(ReadOnlyMemory<char> span, JValue parent = null) {
    public ReadOnlyMemory<char> Span { get; protected set; } = span;
    public JValue Parent { get; } = parent;
    public int Length => Span.Length;

    public int Offset => GetOffset();
    private int offset = -1;

    private int GetOffset() {
        if (offset != -1) return offset;
        root ??= GetRoot();
        return offset = (int)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(root.Span.Span),
            ref MemoryMarshal.GetReference(Span.Span)) / sizeof(char);
    }

    public JValue Root => root ??= GetRoot();
    private JValue root = null;

    private JValue GetRoot() {
        JValue r = this;
        while (r.Parent != null) r = r.Parent;
        return r;
    }

    public override string ToString() => toString ??= Span.ToString();
    private string toString = null;

    public virtual StringBuilder Serialize(StringBuilder sb) => sb.Append(Span);

    public static JValue Parse(ReadOnlyMemory<char> span, JValue parent = null) {
        if (span.Length == 0) return null;
        var c = span.Span[0];
        if (c is '\n' or '\r' or '\t' or ' ') return JWhitespace.Parse(span, parent);
        if (c == '"') return JString.Parse(span, parent);
        if (c == '{') return JObject.Parse(span, parent);
        if (c == '[') return JArray.Parse(span, parent);
        if (c is >='a' and <='z' or >='A' and <='Z' or >='0' and <='9' or '-' or '.') return JLiteral.Parse(span, parent);
        return JError.Create(span, parent,
            c is < ' ' or > '~'
                ? $"Invalid value, unexpected character '\\u{(int)c:X04}' found."
                : $"Invalid value, unexpected character '{c}' found.");
    }
}

public abstract class JHolder(ReadOnlyMemory<char> span, JValue parent = null) : JValue(span, parent) {
    public ReadOnlyMemory<char> Inner => Span[1..^1];

    internal JHolder TruncateSpan(int newLength) {
        Span = Span[..newLength];
        return this;
    }
}

public class JWhitespace(ReadOnlyMemory<char> span, JValue parent = null) : JValue(span, parent) {
    public override StringBuilder Serialize(StringBuilder sb) => sb;

    public new static JValue Parse(ReadOnlyMemory<char> span, JValue parent = null) {
        int i = 0;
        while (i < span.Length && span.Span[i] is ' ' or '\n' or '\r' or '\t') i++;
        return new JWhitespace(span[..i], parent);
    }
}

public class JError(ReadOnlyMemory<char> span, JValue parent = null, string message = null) : JValue(span, parent) {
    public string Message => message;
    public string Key { get; } = $"JError:{Guid.NewGuid()}";
    public override string ToString() => $"<{Key}: {Message}>";
    public override StringBuilder Serialize(StringBuilder sb) => sb;

    public static JError Create(ReadOnlyMemory<char> span, JValue parent = null, string message = null,
        string consumeTo = " \t\r\n\"[{0123456789-.ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz") {
        int i = 0;
        while (i < span.Length && !consumeTo.Contains(span.Span[i])) i++;
        return new JError(span[..i], parent, message);
    }
}

public class JLiteral(ReadOnlyMemory<char> span, JValue parent = null) : JValue(span, parent) {
    public bool IsTrue => isTrue ??= Length is 4 && Span.Span.SequenceEqual("true".AsSpan());
    private bool? isTrue = null;
    public bool IsFalse => isFalse ??= Length is 5 && Span.Span.SequenceEqual("false".AsSpan());
    private bool? isFalse = null;
    public bool IsNull => isNull ??= Length is 4 && Span.Span.SequenceEqual("null".AsSpan());
    private bool? isNull = null;
    public bool IsValidNumber => isNumberValue ??= CheckNumberValid();
    private bool? isNumberValue = null;

    private bool CheckNumberValid() {
        ReadOnlySpan<char> span = Span.Span;
        int i = 0, len = span.Length;

        // Optional minus sign
        if (i < len && span[i] == '-') i++;

        bool hasDigits = false;
        if (i < len && span[i] is '0') {
            if (++i < len && span[i] is >= '0' and <= '9') return false; // Leading zeros are not allowed
            hasDigits = true;
        }

        // consume digits before decimal point
        while (i < Length && span[i] is >= '0' and <= '9') {
            hasDigits = true;
            i++;
        }

        // optional decimal point followed by digits
        if (i < len && span[i] is '.')
            for (i++; i < len && span[i] is >= '0' and <= '9'; i++)
                hasDigits = true;

        if (!hasDigits) return false; // must have at least one digit before or after decimal point

        // optional exponent
        if (i < len && span[i] is 'e' or 'E') {
            if (++i < len && span[i] is '-' or '+') i++; // optional exponent sign
            if (i == len || span[i] is < '0' or > '9' || span[i] is '0' && ++i < len)
                return false; // at least one digit required/no leading zeros
            while (i < len && span[i] is >= '0' and <= '9') i++;
        }

        return i == len; // all characters must be consumed for a valid number
    }

    public new static JValue Parse(ReadOnlyMemory<char> span, JValue parent = null) {
        int i = 0;
        while (i < span.Length && span.Span[i] is >='a' and <='z' or >='A' and <='Z' or >='0' and <='9' or '-' or '.') i++;
        return new JLiteral(span[..i], parent);
    }
}

public class JString(ReadOnlyMemory<char> span, JValue parent = null) : JHolder(span, parent) {
    public ReadOnlyMemory<char> String() => unescaped ??= UnescapeIfNeeded(Inner);
    private ReadOnlyMemory<char>? unescaped = null;

    private ReadOnlyMemory<char> UnescapeIfNeeded(ReadOnlyMemory<char> span) {
        int i = 0;
        for (; i < span.Length && span.Span[i] != '\\'; i++) ;
        if (i == span.Length) return span; // No escape sequences, return original span
        StringBuilder sb = new(span.Length);
        sb.Append(span.Span[..i++]);
        while (i < span.Length) {
            char c = span.Span[i++];
            sb.Append(c switch {
                'b' => '\b',
                'f' => '\f',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                'u' => (char)HexParse(span.Span[i..(i += 4)]),
                _ => c
            });
            int s = i;
            for (; i < span.Length && span.Span[i] != '\\'; i++) ;
            sb.Append(span.Span[s..i++]);
        }

        return sb.ToString().AsMemory();
    }

    private static int HexParse(ReadOnlySpan<char> hex) {
        int result = 0;
        for (int i = 0; i < hex.Length; i++) {
            char c = hex[i];
            result = c switch {
                >= '0' and <= '9' => (result << 4) + c - '0',
                >= 'A' and <= 'F' => (result << 4) + c - 'A' + 10,
                >= 'a' and <= 'f' => (result << 4) + c - 'a' + 10,
                _ => result
            };
        }

        return result;
    }

    public new static JValue Parse(ReadOnlyMemory<char> span, JValue parent = null) {
        if (span.Length == 0 || span.Span[0] != '"')
            return JError.Create(span, parent, "Invalid string, opening quote not found", "\"");
        bool needsUnescape = false;
        for (int i = 1; i < span.Length;) {
            char c = span.Span[i++];
            if (c is '"') {
                JString j = new(span[..i], parent);
                if (!needsUnescape) j.unescaped = j.Inner; // No escape sequences, set unescaped value to inner span
                return j;
            }

            if (c is '\\') {
                needsUnescape = true;
                if (span.Span[i++] == 'u') i += 4;
            }
        }

        return new JError(span, parent, "Invalid string, closing quote not found.");
    }
}

public class JArray(ReadOnlyMemory<char> span, JValue parent = null)
    : JHolder(span, parent), IList<JValue>, IReadOnlyCollection<JValue> {
    List<JValue> Items { get; } = new();
    public override string ToString() => Serialize(new()).ToString();
    public IEnumerator<JValue> GetEnumerator() => Items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public int Count => Items.Count;
    public bool IsReadOnly => false;

    public JValue this[int index] {
        get => Items[index];
        set => Items[index] = value;
    }
    
    public int IndexOf(JValue item) => Items.IndexOf(item);
    public void Insert(int index, JValue item) => Items.Insert(index, item);
    public void RemoveAt(int index) => Items.RemoveAt(index);
    public void Add(JValue item) => Items.Add(item);
    public void Clear() => Items.Clear();
    public bool Contains(JValue item) => Items.Contains(item);
    public void CopyTo(JValue[] array, int arrayIndex) => Items.CopyTo(array, arrayIndex);
    public bool Remove(JValue item) => Items.Remove(item);

    public override StringBuilder Serialize(StringBuilder sb) {
        sb.Append('[');
        int len = sb.Length;
        foreach (JValue item in Items) {
            if (sb.Length > len) sb.Append(',');
            item.Serialize(sb);
        }

        return sb.Append(']');
    }

    public new static JValue Parse(ReadOnlyMemory<char> span, JValue parent = null) {
        if (span.Length == 0 || span.Span[0] != '[')
            return JError.Create(span, parent, "Invalid array, opening bracket not found", "[");
        JArray arr = new(span, parent);
        int i = 1 + JWhitespace.Parse(span[1..], arr).Length;
        if (span.Span[i] == ']') return arr.TruncateSpan(i + 1);
        while (i < span.Length) {
            JValue val = JValue.Parse(span[i..], arr);
            i += val.Length;
            arr.Items.Add(val);
            if (i == span.Length) break;
            i += JWhitespace.Parse(span[i..], arr).Length;
            char c = span.Span[i++];
            if (c == ']') return arr.TruncateSpan(i);
            if (c != ',') {
                JError err = JError.Create(span[i..], arr, "Invalid array, expected ',' or ']' after value.", ",]");
                i += err.Length;
                if (i == span.Length) break;
                if (span.Span[i] == ']') return arr.TruncateSpan(i + 1);
            }

            i += JWhitespace.Parse(span[i..], arr).Length;
        }

        arr.Items.Add(new JError(span[i..], arr, "Invalid array, closing bracket not found."));
        return arr;
    }
}

public class JObject(ReadOnlyMemory<char> span, JValue parent = null) : JHolder(span, parent), IDictionary<ReadOnlyMemory<char>, JValue>, IReadOnlyDictionary<ReadOnlyMemory<char>, JValue> {
    Dictionary<ReadOnlyMemory<char>, JValue> Items { get; } = new(ReadOnlyMemoryComparer<char>.Default);
    public override string ToString() => Serialize(new()).ToString();

    public JValue this[ReadOnlyMemory<char> key] {
        get => Items[key];
        set => Items[key] = value;
    }

    public JValue this[string key] {
        get => Items[key.AsMemory()];
        set => Items[key.AsMemory()] = value;
    }

    public int Count => Items.Count;
    public bool IsReadOnly => false;
    ICollection<JValue> IDictionary<ReadOnlyMemory<char>, JValue>.Values => Items.Values;
    ICollection<ReadOnlyMemory<char>> IDictionary<ReadOnlyMemory<char>, JValue>.Keys => Items.Keys;
    public IEnumerable<ReadOnlyMemory<char>> Keys => Items.Keys;
    public IEnumerable<JValue> Values => Items.Values;

    public bool TryGetValue(ReadOnlyMemory<char> key, out JValue value) => Items.TryGetValue(key, out value);
    public bool TryGetValue(string key, out JValue value) => Items.TryGetValue(key.AsMemory(), out value);
    public bool TryGetValue(JString key, out JValue value) => Items.TryGetValue(key.String(), out value);

    public JValue GetValueOrDefault(ReadOnlyMemory<char> key, JValue defaultValue) =>
        Items.GetValueOrDefault(key, defaultValue);
    public JValue GetValueOrDefault(string key, JValue defaultValue) =>
        Items.GetValueOrDefault(key.AsMemory(), defaultValue);
    public JValue GetValueOrDefault(JString key, JValue defaultValue) =>
        Items.GetValueOrDefault(key.String(), defaultValue);

    public bool ContainsKey(ReadOnlyMemory<char> key) => Items.ContainsKey(key);
    public bool ContainsKey(string key) => Items.ContainsKey(key.AsMemory());
    public bool ContainsKey(JString key) => Items.ContainsKey(key.String());
    public IEnumerator<KeyValuePair<ReadOnlyMemory<char>, JValue>> GetEnumerator() => Items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override StringBuilder Serialize(StringBuilder sb) {
        sb.Append('{');
        int len = sb.Length;
        foreach (KeyValuePair<ReadOnlyMemory<char>, JValue> kvp in Items) {
            if (sb.Length > len) sb.Append(',');
            kvp.Value.Serialize(sb.Append('"').Append(kvp.Key.Span).Append('"').Append(':'));
        }

        return sb.Append('}');
    }
    
    public void Add(ReadOnlyMemory<char> key, JValue value) => Items.Add(key, value);
    public void Add(string key, JValue value) => Items.Add(key.AsMemory(), value);
    public void Add(JString key, JValue value) => Items.Add(key.String(), value);
    public bool Remove(ReadOnlyMemory<char> key) => Items.Remove(key);
    public bool Remove(string key) => Items.Remove(key.AsMemory());
    public bool Remove(JString key) => Items.Remove(key.String());
    public void Add(KeyValuePair<ReadOnlyMemory<char>, JValue> item) => Items.Add(item.Key, item.Value);
    public void Add(KeyValuePair<string, JValue> item) => Items.Add(item.Key.AsMemory(), item.Value);
    public void Add(KeyValuePair<JString, JValue> item) => Items.Add(item.Key.String(), item.Value);
    public void Clear() => Items.Clear();
    public bool Contains(KeyValuePair<ReadOnlyMemory<char>, JValue> item) => Items.TryGetValue(item.Key, out var value) && value == item.Value;
    public bool Contains(KeyValuePair<string, JValue> item) => Items.TryGetValue(item.Key.AsMemory(), out var value) && value == item.Value;
    public bool Contains(KeyValuePair<JString, JValue> item) => Items.TryGetValue(item.Key.String(), out var value) && value == item.Value;

    public void CopyTo(KeyValuePair<ReadOnlyMemory<char>, JValue>[] array, int arrayIndex) {
        foreach (KeyValuePair<ReadOnlyMemory<char>, JValue> item in Items) array[arrayIndex++] = item;
    }
    public bool Remove(KeyValuePair<ReadOnlyMemory<char>, JValue> item) => Items.TryGetValue(item.Key, out JValue value) && value == item.Value && Items.Remove(item.Key);

    public bool Remove(KeyValuePair<string, JValue> item) => Items.TryGetValue(item.Key.AsMemory(), out JValue value) &&
                                                             value == item.Value && Items.Remove(item.Key.AsMemory());

    public bool Remove(KeyValuePair<JString, JValue> item) => Items.TryGetValue(item.Key.String(), out JValue value) &&
                                                              value == item.Value && Items.Remove(item.Key.String());

    public new static JValue Parse(ReadOnlyMemory<char> span, JValue parent = null) {
        if (span.Length == 0 || span.Span[0] != '{')
            return JError.Create(span, parent, "Invalid object, opening brace not found", "{");
        JObject obj = new(span, parent);
        int i = 1 + JWhitespace.Parse(span[1..], obj).Length;
        if (span.Span[i] == '}') return obj.TruncateSpan(i + 1);
        while (i < span.Length) {
            JValue key = JString.Parse(span[i..], obj);
            i += key.Length;
            if (key is JError keyErr) {
                obj.Items.Add(keyErr.Key.AsMemory(), keyErr);
                continue;
            }

            i += JWhitespace.Parse(span[i..], obj).Length;
            if (span.Span[i++] != ':') {
                JError err = JError.Create(span[i..], obj, "Invalid object, expected ':' after key.", ":,}");
                obj.Items.Add(err.Key.AsMemory(), err);
                i += err.Length;
                if (i == span.Length) break;
                if (span.Span[i] == '}') return obj.TruncateSpan(i + 1);
                if (span.Span[i] != ':') continue;
            }

            i += JWhitespace.Parse(span[i..], obj).Length;
            JValue val = JValue.Parse(span[i..], obj);
            i += val.Length;
            obj.Items.Add(((JString)key).String(), val);
            if (i == span.Length) break;
            i += JWhitespace.Parse(span[i..], obj).Length;
            char c = span.Span[i++];
            if (c == '}') return obj.TruncateSpan(i);
            if (c != ',') {
                JError err = JError.Create(span[i..], obj, "Invalid object, expected ',' or '}' after value.", ",}");
                obj.Items.Add(err.Key.AsMemory(), err);
                i += err.Length;
                if (i == span.Length) break;
                if (span.Span[i] == '}') return obj.TruncateSpan(i + 1);
            }

            i += JWhitespace.Parse(span[i..], obj).Length;
        }

        JError closeErr = new(span[i..], obj, "Invalid object, closing brace not found.");
        obj.Items.Add(closeErr.Key.AsMemory(), closeErr);
        return obj;
    }
}
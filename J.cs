using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace com.hafthor.J;

/// <summary>
/// Represents a JSON value, which can be an object, array, string, literal, or whitespace.
/// </summary>
/// <param name="span">memory that points to the original unparsed JSON string.</param>
/// <param name="parent">the parent value of this value, or null if this is the root value.</param>
public abstract class JValue(ReadOnlyMemory<char> span, JValue parent = null) {
    /// <summary>
    /// The memory that points to the original unparsed JSON string.
    /// </summary>
    public ReadOnlyMemory<char> Span { get; protected set; } = span;

    /// <summary>
    /// The length of the original unparsed JSON string.
    /// </summary>
    public int Length => Span.Length;

    /// <summary>
    /// The offset of this value in the original root unparsed JSON string.
    /// </summary>
    public int Offset => GetOffset();

    private int offset = -1;

    private int GetOffset() {
        if (offset != -1) return offset;
        root ??= GetRoot();
        return offset = (int)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(root.Span.Span),
            ref MemoryMarshal.GetReference(Span.Span)) / sizeof(char);
    }

    /// <summary>
    /// The parent value of this value, or null if this is the root value.
    /// </summary>
    public JValue Parent { get; } = parent;

    /// <summary>
    /// The root value of this value, which is the top-level value in the JSON structure.
    /// </summary>
    public JValue Root => root ??= GetRoot();

    private JValue root = null;

    private JValue GetRoot() {
        JValue r = this;
        while (r.Parent != null) r = r.Parent;
        return r;
    }

    /// <summary>
    /// The string representation of this value.
    /// </summary>
    public override string ToString() => toString ??= Span.ToString();

    private string toString = null;

    /// <summary>
    /// Serializes this value to a JSON string.
    /// </summary>
    /// <param name="sb">String builder to append the serialized JSON to.</param>
    /// <returns>The string builder with the serialized JSON appended.</returns>
    public virtual StringBuilder Serialize(StringBuilder sb) => sb.Append(Span);

    /// <summary>
    /// Deserializes a string into a JValue.
    /// </summary>
    /// <param name="span">Pointer to the string to parse.</param>
    /// <param name="parent">Parent JValue, if any.</param>
    /// <returns>A JValue of the type that represents the string parsed.</returns>
    /// <remarks>
    /// Caller can use the returned JValue's .Length to know how many characters were consumed during parsing.
    /// </remarks>
    public static JValue Parse(ReadOnlyMemory<char> span, JValue parent = null) {
        if (span.Length == 0) return null;
        var c = span.Span[0];
        if (c is '\n' or '\r' or '\t' or ' ') return JWhitespace.Parse(span, parent);
        if (c == '"') return JString.Parse(span, parent);
        if (c == '{') return JObject.Parse(span, parent);
        if (c == '[') return JArray.Parse(span, parent);
        if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '.')
            return JLiteral.Parse(span, parent);
        return JError.Create(span, parent,
            c is < ' ' or > '~'
                ? $"Invalid value, unexpected character '\\u{(int)c:X04}' found."
                : $"Invalid value, unexpected character '{c}' found.");
    }
    
    /// <summary>
    /// Deserializes a string into a JValue.
    /// </summary>
    /// <param name="json">The string to parse.</param>
    /// <param name="parent">Parent JValue, if any.</param>
    /// <returns>A JValue of the type that represents the string parsed.</returns>
    /// <remarks>
    /// Caller can use the returned JValue's .Length to know how many characters were consumed during parsing.
    /// </remarks>
    public static JValue Parse(string json, JValue parent = null) => Parse(json.AsMemory(), parent);
}

/// <summary>
/// Represents a JValue with enclosing characters, such as an object, array, or string.
/// </summary>
/// <param name="span">Pointer to the JSON string.</param>
/// <param name="parent">Parent JValue, if any.</param>
public abstract class JHolder(ReadOnlyMemory<char> span, JValue parent = null) : JValue(span, parent) {
    /// <summary>
    /// The content of this holder, which is the part of the JSON string between the enclosing characters.
    /// </summary>
    public ReadOnlyMemory<char> Inner => Span[1..^1];

    internal JHolder TruncateSpan(int newLength) {
        Span = Span[..newLength];
        return this;
    }
}

/// <summary>
/// Represents whitespace in a JSON string.
/// </summary>
/// <param name="span">Pointer to the JSON string.</param>
/// <param name="parent">Parent JValue, if any.</param>
public class JWhitespace(ReadOnlyMemory<char> span, JValue parent = null) : JValue(span, parent) {
    /// <summary>
    /// Serializes this whitespace to a JSON string.
    /// </summary>
    /// <param name="sb">The StringBuilder to which the whitespace is appended.</param>
    /// <returns>The StringBuilder with the whitespace appended.</returns>
    public override StringBuilder Serialize(StringBuilder sb) => sb;

    /// <summary>
    /// Parses a JSON string into a JWhitespace.
    /// </summary>
    /// <param name="span">The JSON string to parse.</param>
    /// <param name="parent">Parent JValue, if any.</param>
    /// <returns>A JWhitespace instance representing the parsed whitespace.</returns>
    /// <remarks>
    /// Caller can use the returned JValue's .Length to know how many characters were consumed during parsing.
    /// </remarks>
    public new static JValue Parse(ReadOnlyMemory<char> span, JValue parent = null) {
        int i = 0;
        while (i < span.Length && span.Span[i] is ' ' or '\n' or '\r' or '\t') i++;
        return new JWhitespace(span[..i], parent);
    }
    
    /// <summary>
    /// Parses a JSON string into a JWhitespace.
    /// </summary>
    /// <param name="json">The JSON string to parse.</param>
    /// <param name="parent">Parent JValue, if any.</param>
    /// <returns>A JWhitespace instance representing the parsed whitespace.</returns>
    /// <remarks>
    /// Caller can use the returned JValue's .Length to know how many characters were consumed during parsing.
    /// </remarks>
    public new static JValue Parse(string json, JValue parent = null) => Parse(json.AsMemory(), parent);
}

/// <summary>
/// Represents an error encountered during parsing of a JSON string.
/// </summary>
/// <param name="span">Pointer to the JSON string.</param>
/// <param name="parent">Parent JValue, if any.</param>
/// <param name="message">Error message associated with the error.</param>
public class JError(ReadOnlyMemory<char> span, JValue parent = null, string message = null) : JValue(span, parent) {
    /// <summary>
    /// The error message associated with this error.
    /// </summary>
    public string Message => message;

    /// <summary>
    /// The unique identifier for this error, which can be used to distinguish it from other errors.
    /// </summary>
    /// <remarks>Useful for use as a key in a dictionary that parsed with errors.</remarks>
    public string Key { get; } = $"JError:{Guid.NewGuid()}";

    /// <summary>
    /// Serializes this error to a JSON string.
    /// </summary>
    /// <param name="sb">The StringBuilder to which the error message is appended.</param>
    /// <returns>The StringBuilder with the error message appended.</returns>
    public override StringBuilder Serialize(StringBuilder sb) => sb.Append($"<{Key}: {Message}>");

    /// <summary>
    /// Creates a new JError instance with the specified error message.
    /// </summary>
    /// <param name="span">The JSON string that caused the error.</param>
    /// <param name="parent">Parent JValue, if any.</param>
    /// <param name="message">Error message associated with the error.</param>
    /// <param name="consumeTo">Optional string of characters to consume until before creating the error.</param>
    /// <returns>A JError instance representing the parsing error.</returns>
    /// <remarks>
    /// Caller can use the returned JValue's .Length to know how many characters were consumed during parsing.
    /// </remarks>
    public static JError Create(ReadOnlyMemory<char> span, JValue parent = null, string message = null,
        string consumeTo = null) {
        int i = 0;
        if (consumeTo == null)
            while (i < span.Length && span.Span[i] is not (' ' or '\t' or '\r' or '\n' or '[' or '{' or '"'
                       or >= '0' and <= '9' or >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '-' or '.'))
                i++;
        else
            while (i < span.Length && !consumeTo.Contains(span.Span[i]))
                i++;
        return new JError(span[..i], parent, message);
    }
    
    /// <summary>
    /// Creates a new JError instance with the specified error message.
    /// </summary>
    /// <param name="json">The JSON string that caused the error.</param>
    /// <param name="parent">Parent JValue, if any.</param>
    /// <param name="message">Error message associated with the error.</param>
    /// <param name="consumeTo">Optional string of characters to consume until before creating the error.</param>
    /// <returns>A JError instance representing the parsing error.</returns>
    /// <remarks>
    /// Caller can use the returned JValue's .Length to know how many characters were consumed during parsing.
    /// </remarks>
    public static JError Create(string json, JValue parent = null, string message = null, string consumeTo = null) =>
        Create(json.AsMemory(), parent, message, consumeTo);
}

/// <summary>
/// Represents a JSON literal value, which can be a number, boolean, or null.
/// </summary>
/// <param name="span">The JSON string representing the literal value.</param>
/// <param name="parent">Parent JValue, if any.</param>
public class JLiteral(ReadOnlyMemory<char> span, JValue parent = null) : JValue(span, parent) {
    /// <summary>
    /// Returns true if this literal represents the JSON true value, false otherwise.
    /// </summary>
    public bool IsTrue => isTrue ??= Span.Span.SequenceEqual("true".AsSpan());

    private bool? isTrue = null;

    /// <summary>
    /// Returns true if this literal represents the JSON false value, false otherwise.
    /// </summary>
    public bool IsFalse => isFalse ??= Span.Span.SequenceEqual("false".AsSpan());

    private bool? isFalse = null;

    /// <summary>
    /// Returns true if this literal represents the JSON null value, false otherwise.
    /// </summary>
    public bool IsNull => isNull ??= Span.Span.SequenceEqual("null".AsSpan());

    private bool? isNull = null;

    /// <summary>
    /// Returns true if this literal represents a valid JSON number, false otherwise.
    /// </summary>
    /// <remarks>
    /// Note that this does not guarantee that the number can be parsed into a specific numeric type, only that it
    /// follows the JSON number syntax.
    /// </remarks>
    public bool IsValidNumber => isValidNumber ??= CheckIsValidNumber(Span.Span);

    private bool? isValidNumber = null;

    private static bool CheckIsValidNumber(ReadOnlySpan<char> span) {
        int i = 0, len = span.Length;

        // Optional minus sign
        if (i < len && span[i] == '-') i++;

        bool hasDigits = false;
        if (i < len && span[i] is '0') {
            if (++i < len && span[i] is >= '0' and <= '9') return false; // Leading zeros are not allowed
            hasDigits = true;
        }

        // consume digits before decimal point
        for (; i < len && span[i] is >= '0' and <= '9'; i++) hasDigits = true;

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

    /// <summary>
    /// Parses a JSON string into a JLiteral.
    /// </summary>
    /// <param name="span">The JSON string to parse.</param>
    /// <param name="parent">Parent JValue, if any.</param>
    /// <returns>A JLiteral representing the parsed JSON string.</returns>
    /// <remarks>
    /// Caller can use the returned JValue's .Length to know how many characters were consumed during parsing.
    /// </remarks>
    public new static JValue Parse(ReadOnlyMemory<char> span, JValue parent = null) {
        int i = 0;
        while (i < span.Length &&
               span.Span[i] is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '.' or '+') i++;
        return new JLiteral(span[..i], parent);
    }
}

/// <summary>
/// Represents a JSON string value.
/// </summary>
/// <param name="span">The JSON string to parse.</param>
/// <param name="parent">Parent JValue, if any.</param>
public class JString(ReadOnlyMemory<char> span, JValue parent = null) : JHolder(span, parent) {
    /// <summary>
    /// Parses a JSON string into a normal string value, unescaping any escape sequences in the process.
    /// </summary>
    public ReadOnlyMemory<char> String() => unescaped ??= UnescapeIfNeeded(Inner);

    private ReadOnlyMemory<char>? unescaped = null;

    private ReadOnlyMemory<char> UnescapeIfNeeded(ReadOnlyMemory<char> span) {
        int i = 0;
        for (; i < span.Length && span.Span[i] is not '\\'; i++) ;
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
                '0' => '\0',
                'v' => '\v',
                'u' => (char)HexParse(span.Span, ref i, 4),
                'x' => (char)HexParse(span.Span, ref i, 2),
                _ => c
            });
            int s = i;
            for (; i < span.Length && span.Span[i] != '\\'; i++) ;
            sb.Append(span.Span[s..i++]);
        }

        return sb.ToString().AsMemory();
    }

    private static int HexParse(ReadOnlySpan<char> hex, ref int start, int length) {
        int result = 0, end = Math.Min(start + length, hex.Length);
        for (; start < end; start++) {
            char c = hex[start];
            if (c is >= '0' and <= '9')
                result = (result << 4) + (c & 15);
            else if (c is >= 'a' and <= 'f' or >= 'A' and <= 'F')
                result = (result << 4) + (c & 15) + 9;
            else
                break;
        }

        return result;
    }

    /// <summary>
    /// Parses a JSON string value from the input span, handling escape sequences and ensuring proper string
    /// termination.
    /// </summary>
    /// <param name="span">The JSON string to parse.</param>
    /// <param name="parent">Parent JValue, if any.</param>
    /// <returns>A JString representing the parsed JSON string.</returns>
    /// <remarks>
    /// Caller can use the returned JValue's .Length to know how many characters were consumed during parsing.
    /// </remarks>
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
                if ((c = span.Span[i++]) is 'u' or 'x')
                    for (int e = Math.Min(i + (c == 'u' ? 4 : 2), span.Length);
                         i < e && span.Span[i] is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
                         i++)
                        ;
            }
        }

        return new JError(span, parent, "Invalid string, closing quote not found.");
    }
    
    /// <summary>
    /// Parses a JSON string value from the input span, handling escape sequences and ensuring proper string
    /// termination.
    /// </summary>
    /// <param name="json">The JSON string to parse.</param>
    /// <param name="parent">Parent JValue, if any.</param>
    /// <returns>A JString representing the parsed JSON string.</returns>
    /// <remarks>
    /// Caller can use the returned JValue's .Length to know how many characters were consumed during parsing.
    /// </remarks>
    public new static JValue Parse(string json, JValue parent = null) => Parse(json.AsMemory(), parent);
}

/// <summary>
/// Represents a JSON array value.
/// </summary>
/// <param name="span">The JSON array to parse.</param>
/// <param name="parent">Parent JValue, if any.</param>
public class JArray(ReadOnlyMemory<char> span, JValue parent = null)
    : JHolder(span, parent), IList<JValue>, IReadOnlyCollection<JValue> {
    /// <summary>
    /// The list of items in this JSON array.
    /// </summary>
    List<JValue> Items { get; } = [];

    /// <summary>
    /// Serializes the JSON array back into a JSON string, including brackets and commas.
    /// </summary>
    public override string ToString() => Serialize(new()).ToString();

    /// <summary>
    /// Get enumerator for iterating over items in the JSON array.
    /// </summary>
    public IEnumerator<JValue> GetEnumerator() => Items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Gets the number of items in the JSON array.
    /// </summary>
    public int Count => Items.Count;

    /// <summary>
    /// Returns false since JSON arrays are mutable.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Indexer for accessing items in the JSON array by index.
    /// </summary>
    /// <param name="index">item index</param>
    public JValue this[int index] {
        get => Items[index];
        set => Items[index] = value;
    }

    /// <summary>
    /// Finds the index of the first occurrence of a specific item in the JSON array.
    /// </summary>
    /// <param name="item">The item to find.</param>
    /// <returns>The index of the item, or -1 if not found.</returns>
    public int IndexOf(JValue item) => Items.IndexOf(item);

    /// <summary>
    /// Inserts an item into the JSON array at the specified index.
    /// </summary>
    /// <param name="index">The index at which to insert the item.</param>
    /// <param name="item">The item to insert.</param>
    public void Insert(int index, JValue item) => Items.Insert(index, item);

    /// <summary>
    /// Removes the item at the specified index from the JSON array.
    /// </summary>
    /// <param name="index">The index of the item to remove.</param>
    public void RemoveAt(int index) => Items.RemoveAt(index);

    /// <summary>
    /// Adds an item to the end of the JSON array.
    /// </summary>
    /// <param name="item">The item to add.</param>
    public void Add(JValue item) => Items.Add(item);

    /// <summary>
    /// Removes all items from the JSON array.
    /// </summary>
    public void Clear() => Items.Clear();

    /// <summary>
    /// Determines whether the JSON array contains a specific item.
    /// </summary>
    /// <param name="item">The item to check for.</param>
    /// <returns>True if the item is found, otherwise false.</returns>
    public bool Contains(JValue item) => Items.Contains(item);

    /// <summary>
    /// Copies the elements of the JSON array to an array, starting at a particular array index.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The starting index in the destination array.</param>
    public void CopyTo(JValue[] array, int arrayIndex) => Items.CopyTo(array, arrayIndex);

    /// <summary>
    /// Removes the first occurrence of a specific item from the JSON array.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    /// <returns>True if the item was found and removed, otherwise false.</returns>
    public bool Remove(JValue item) => Items.Remove(item);

    /// <summary>
    /// Serializes the JSON array into a JSON string, including brackets and commas. Recursively serializes nested
    /// items.
    /// </summary>
    /// <param name="sb">The StringBuilder to serialize the JSON array into.</param>
    /// <returns>The StringBuilder with the serialized JSON array.</returns>
    public override StringBuilder Serialize(StringBuilder sb) {
        sb.Append('[');
        int len = sb.Length;
        foreach (JValue item in Items) {
            if (sb.Length > len) sb.Append(',');
            item.Serialize(sb);
        }

        return sb.Append(']');
    }

    /// <summary>
    /// Parses a JSON array from the input span, handling nested structures and ensuring proper array termination.
    /// </summary>
    /// <param name="span">The span containing the JSON array to parse.</param>
    /// <param name="parent">The parent JSON value, if any.</param>
    /// <returns>The parsed JArray instance.</returns>
    /// <remarks>
    /// Caller can use the returned JValue's .Length to know how many characters were consumed during parsing.
    /// </remarks>
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

    /// <summary>
    /// Parses a JSON array from the input span, handling nested structures and ensuring proper array termination.
    /// </summary>
    /// <param name="json">The string containing the JSON array to parse.</param>
    /// <param name="parent">The parent JSON value, if any.</param>
    /// <returns>The parsed JArray instance.</returns>
    /// <remarks>
    /// Caller can use the returned JValue's .Length to know how many characters were consumed during parsing.
    /// </remarks>
    public new static JValue Parse(string json, JValue parent = null) => Parse(json.AsMemory(), parent);
}


/// <summary>
/// Represents a JSON object value.
/// </summary>
/// <param name="span">The span containing the JSON object to parse.</param>
/// <param name="parent">The parent JSON value, if any.</param>
public class JObject(ReadOnlyMemory<char> span, JValue parent = null) : JHolder(span, parent),
    IDictionary<ReadOnlyMemory<char>, JValue>, IReadOnlyDictionary<ReadOnlyMemory<char>, JValue> {
    /// <summary>
    /// The dictionary of key-value pairs in this JSON object.
    /// </summary>
    Dictionary<ReadOnlyMemory<char>, JValue> Items { get; } = new(ReadOnlyMemoryComparer<char>.Default);

    /// <summary>
    /// Serializes the JSON object back into a JSON string, including braces, quotes, colons, and commas.
    /// </summary>
    public override string ToString() => Serialize(new()).ToString();

    /// <summary>
    /// Indexer for accessing values in the JSON object by key.
    /// </summary>
    /// <param name="key">Key to access in the JSON object.</param>
    public JValue this[ReadOnlyMemory<char> key] {
        get => Items[key];
        set => Items[key] = value;
    }

    /// <summary>
    /// Indexer for accessing values in the JSON object by key.
    /// </summary>
    /// <param name="key">Key to access in the JSON object.</param>
    public JValue this[string key] {
        get => Items[key.AsMemory()];
        set => Items[key.AsMemory()] = value;
    }

    /// <summary>
    /// Indexer for accessing values in the JSON object by key.
    /// </summary>
    /// <param name="key">Key to access in the JSON object.</param>
    public JValue this[JString key] {
        get => Items[key.String()];
        set => Items[key.String()] = value;
    }

    /// <summary>
    /// Number of key-value pairs in the JSON object.
    /// </summary>
    public int Count => Items.Count;

    /// <summary>
    /// Returns false since JSON objects are mutable.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Returns an enumerable collection of the keys in the JSON object.
    /// </summary>
    public IEnumerable<ReadOnlyMemory<char>> Keys => Items.Keys;

    ICollection<ReadOnlyMemory<char>> IDictionary<ReadOnlyMemory<char>, JValue>.Keys => Items.Keys;

    /// <summary>
    /// Returns an enumerable collection of the values in the JSON object.
    /// </summary>
    public IEnumerable<JValue> Values => Items.Values;

    ICollection<JValue> IDictionary<ReadOnlyMemory<char>, JValue>.Values => Items.Values;

    /// <summary>
    /// Attempts to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">Key to retrieve the value for.</param>
    /// <param name="value">The value associated with the key, if found.</param>
    /// <returns>True if the key was found, otherwise false.</returns>
    public bool TryGetValue(ReadOnlyMemory<char> key, out JValue value) => Items.TryGetValue(key, out value);

    /// <summary>
    /// Attempts to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">Key to retrieve the value for.</param>
    /// <param name="value">The value associated with the key, if found.</param>
    /// <returns>True if the key was found, otherwise false.</returns>
    public bool TryGetValue(string key, out JValue value) => Items.TryGetValue(key.AsMemory(), out value);

    /// <summary>
    /// Attempts to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">Key to retrieve the value for.</param>
    /// <param name="value">The value associated with the key, if found.</param>
    /// <returns>True if the key was found, otherwise false.</returns>
    public bool TryGetValue(JString key, out JValue value) => Items.TryGetValue(key.String(), out value);

    /// <summary>
    /// Attempts to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">Key to retrieve the value for.</param>
    /// <param name="defaultValue">Default value to return if the key is not found.</param>
    /// <returns>The value associated with the key, or the default value if not found.</returns>
    public JValue GetValueOrDefault(ReadOnlyMemory<char> key, JValue defaultValue) =>
        Items.GetValueOrDefault(key, defaultValue);

    /// <summary>
    /// Attempts to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">Key to retrieve the value for.</param>
    /// <param name="defaultValue">Default value to return if the key is not found.</param>
    /// <returns>The value associated with the key, or the default value if not found.</returns>
    public JValue GetValueOrDefault(string key, JValue defaultValue) =>
        Items.GetValueOrDefault(key.AsMemory(), defaultValue);

    /// <summary>
    /// Attempts to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">Key to retrieve the value for.</param>
    /// <param name="defaultValue">Default value to return if the key is not found.</param>
    /// <returns>The value associated with the key, or the default value if not found.</returns>
    public JValue GetValueOrDefault(JString key, JValue defaultValue) =>
        Items.GetValueOrDefault(key.String(), defaultValue);

    /// <summary>
    /// Determines whether the JSON object contains the specified key.
    /// </summary>
    /// <param name="key">Key to check for existence.</param>
    /// <returns>True if the key exists in the JSON object, otherwise false.</returns>
    public bool ContainsKey(ReadOnlyMemory<char> key) => Items.ContainsKey(key);

    /// <summary>
    /// Determines whether the JSON object contains the specified key.
    /// </summary>
    /// <param name="key">Key to check for existence.</param>
    /// <returns>True if the key exists in the JSON object, otherwise false.</returns>
    public bool ContainsKey(string key) => Items.ContainsKey(key.AsMemory());

    /// <summary>
    /// Determines whether the JSON object contains the specified key.
    /// </summary>
    /// <param name="key">Key to check for existence.</param>
    /// <returns>True if the key exists in the JSON object, otherwise false.</returns>
    public bool ContainsKey(JString key) => Items.ContainsKey(key.String());

    /// <summary>
    /// Gets the enumerator for iterating through the key-value pairs in the JSON object.
    /// </summary>
    /// <returns></returns>
    public IEnumerator<KeyValuePair<ReadOnlyMemory<char>, JValue>> GetEnumerator() => Items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Serializes the JSON object into a JSON string, including braces, quotes, colons, and commas. Recursively
    /// serializes nested values.
    /// </summary>
    /// <param name="sb">StringBuilder to append the serialized JSON string to.</param>
    /// <returns>StringBuilder with the serialized JSON string.</returns>
    public override StringBuilder Serialize(StringBuilder sb) {
        sb.Append('{');
        int len = sb.Length;
        foreach (KeyValuePair<ReadOnlyMemory<char>, JValue> kvp in Items) {
            if (sb.Length > len) sb.Append(',');
            kvp.Value.Serialize(sb.Append('"').Append(kvp.Key.Span).Append('"').Append(':'));
        }

        return sb.Append('}');
    }

    /// <summary>
    /// Adds a key-value pair to the JSON object.
    /// </summary>
    /// <param name="key">Key to add to the JSON object.</param>
    /// <param name="value">Value to associate with the key.</param>
    public void Add(ReadOnlyMemory<char> key, JValue value) => Items.Add(key, value);

    /// <summary>
    /// Adds a key-value pair to the JSON object.
    /// </summary>
    /// <param name="key">Key to add to the JSON object.</param>
    /// <param name="value">Value to associate with the key.</param>
    public void Add(string key, JValue value) => Items.Add(key.AsMemory(), value);

    /// <summary>
    /// Adds a key-value pair to the JSON object.
    /// </summary>
    /// <param name="key">Key to add to the JSON object.</param>
    /// <param name="value">Value to associate with the key.</param>
    public void Add(JString key, JValue value) => Items.Add(key.String(), value);

    /// <summary>
    /// Removes the value with the specified key from the JSON object.
    /// </summary>
    /// <param name="key">Key to remove from the JSON object.</param>
    /// <returns>True if the key was found and removed, false otherwise.</returns>
    public bool Remove(ReadOnlyMemory<char> key) => Items.Remove(key);

    /// <summary>
    /// Removes the value with the specified key from the JSON object.
    /// </summary>
    /// <param name="key">Key to remove from the JSON object.</param>
    /// <returns>True if the key was found and removed, false otherwise.</returns>
    public bool Remove(string key) => Items.Remove(key.AsMemory());

    /// <summary>
    /// Removes the value with the specified key from the JSON object.
    /// </summary>
    /// <param name="key">Key to remove from the JSON object.</param>
    /// <returns>True if the key was found and removed, false otherwise.</returns>
    public bool Remove(JString key) => Items.Remove(key.String());

    /// <summary>
    /// Adds a key-value pair to the JSON object.
    /// </summary>
    /// <param name="item">Key-value pair to add to the JSON object.</param>
    public void Add(KeyValuePair<ReadOnlyMemory<char>, JValue> item) => Items.Add(item.Key, item.Value);

    /// <summary>
    /// Adds a key-value pair to the JSON object.
    /// </summary>
    /// <param name="item">Key-value pair to add to the JSON object.</param>
    public void Add(KeyValuePair<string, JValue> item) => Items.Add(item.Key.AsMemory(), item.Value);

    /// <summary>
    /// Adds a key-value pair to the JSON object.
    /// </summary>
    /// <param name="item">Key-value pair to add to the JSON object.</param>
    public void Add(KeyValuePair<JString, JValue> item) => Items.Add(item.Key.String(), item.Value);

    /// <summary>
    /// Removes all key-value pairs from the JSON object.
    /// </summary>
    public void Clear() => Items.Clear();

    /// <summary>
    /// Determines whether the JSON object contains a specific key-value pair.
    /// </summary>
    /// <param name="item">Key-value pair to check for in the JSON object.</param>
    /// <returns>True if the key-value pair is found, false otherwise.</returns>
    public bool Contains(KeyValuePair<ReadOnlyMemory<char>, JValue> item) =>
        Items.TryGetValue(item.Key, out var value) && value == item.Value;

    /// <summary>
    /// Determines whether the JSON object contains a specific key-value pair.
    /// </summary>
    /// <param name="item">Key-value pair to check for in the JSON object.</param>
    /// <returns>True if the key-value pair is found, false otherwise.</returns>
    public bool Contains(KeyValuePair<string, JValue> item) =>
        Items.TryGetValue(item.Key.AsMemory(), out var value) && value == item.Value;

    /// <summary>
    /// Determines whether the JSON object contains a specific key-value pair.
    /// </summary>
    /// <param name="item">Key-value pair to check for in the JSON object.</param>
    /// <returns>True if the key-value pair is found, false otherwise.</returns>
    public bool Contains(KeyValuePair<JString, JValue> item) =>
        Items.TryGetValue(item.Key.String(), out var value) && value == item.Value;

    /// <summary>
    /// Copies the key-value pairs of the JSON object to an array, starting at a particular array index.
    /// </summary>
    /// <param name="array">Array to copy the key-value pairs to.</param>
    /// <param name="arrayIndex">Starting index in the array to copy the key-value pairs to.</param>
    public void CopyTo(KeyValuePair<ReadOnlyMemory<char>, JValue>[] array, int arrayIndex) {
        foreach (KeyValuePair<ReadOnlyMemory<char>, JValue> item in Items) array[arrayIndex++] = item;
    }

    /// <summary>
    /// Removes a specific key-value pair from the JSON object.
    /// </summary>
    /// <param name="item">Key-value pair to remove from the JSON object.</param>
    /// <returns>True if the key-value pair was successfully removed, false otherwise.</returns>
    public bool Remove(KeyValuePair<ReadOnlyMemory<char>, JValue> item) =>
        Items.TryGetValue(item.Key, out JValue value) && value == item.Value && Items.Remove(item.Key);

    /// <summary>
    /// Removes a specific key-value pair from the JSON object.
    /// </summary>
    /// <param name="item">Key-value pair to remove from the JSON object.</param>
    /// <returns>True if the key-value pair was successfully removed, false otherwise.</returns>
    public bool Remove(KeyValuePair<string, JValue> item) => Items.TryGetValue(item.Key.AsMemory(), out JValue value) &&
                                                             value == item.Value && Items.Remove(item.Key.AsMemory());

    /// <summary>
    /// Removes a specific key-value pair from the JSON object.
    /// </summary>
    /// <param name="item">Key-value pair to remove from the JSON object.</param>
    /// <returns>True if the key-value pair was successfully removed, false otherwise.</returns>
    public bool Remove(KeyValuePair<JString, JValue> item) => Items.TryGetValue(item.Key.String(), out JValue value) &&
                                                              value == item.Value && Items.Remove(item.Key.String());

    /// <summary>
    /// Parses a JSON string into a JObject.
    /// </summary>
    /// <param name="span">The JSON string to parse.</param>
    /// <param name="parent">The parent JSON value, if any.</param>
    /// <returns>The parsed JObject.</returns>
    /// <remarks>
    /// Caller can use returned JValue's .Length property to determine how many characters were consumed during parsing.
    /// </remarks>
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
    
    /// <summary>
    /// Parses a JSON string into a JObject.
    /// </summary>
    /// <param name="json">The JSON string to parse.</param>
    /// <param name="parent">The parent JSON value, if any.</param>
    /// <returns>The parsed JObject.</returns>
    /// <remarks>
    /// Caller can use returned JValue's .Length property to determine how many characters were consumed during parsing.
    /// </remarks>
    public new static JValue Parse(string json, JValue parent = null) => Parse(json.AsMemory(), parent);
}
namespace com.hafthor.J;

/// <summary>
/// Compares two ReadOnlyMemory&lt;T&gt; instances for equality.
/// </summary>
/// <typeparam name="T">type of items in the memory</typeparam>
public class ReadOnlyMemoryComparer<T> : IEqualityComparer<ReadOnlyMemory<T>> where T : IEquatable<T> {
    /// <summary>
    /// Compares two ReadOnlyMemory&lt;T&gt; instances for equality.
    /// </summary>
    public static ReadOnlyMemoryComparer<T> Default { get; } = new();

    /// <summary>
    /// Compares two ReadOnlyMemory&lt;T&gt; instances for equality.
    /// </summary>
    /// <param name="x">memory to compare</param>
    /// <param name="y">memory to compare</param>
    /// <returns>true if memories are equal, false otherwise</returns>
    public bool Equals(ReadOnlyMemory<T> x, ReadOnlyMemory<T> y) =>
        x.Span.SequenceEqual(y.Span);

    /// <summary>
    /// Gets a hash code for a ReadOnlyMemory&lt;T&gt; instance.
    /// </summary>
    /// <param name="obj">memory to compute hash code for</param>
    /// <returns>hash code for the memory</returns>
    public int GetHashCode(ReadOnlyMemory<T> obj) {
        HashCode hash = new();
        foreach (T item in obj.Span)
            hash.Add(item);
        return hash.ToHashCode();
    }
}
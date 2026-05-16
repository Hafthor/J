namespace com.hafthor.J;

public class ReadOnlyMemoryComparer<T> : IEqualityComparer<ReadOnlyMemory<T>> where T : IEquatable<T> {
    public static ReadOnlyMemoryComparer<T> Default { get; } = new();

    public bool Equals(ReadOnlyMemory<T> x, ReadOnlyMemory<T> y) =>
        x.Span.SequenceEqual(y.Span);

    public int GetHashCode(ReadOnlyMemory<T> obj) {
        HashCode hash = new();
        foreach (T item in obj.Span)
            hash.Add(item);
        return hash.ToHashCode();
    }
}
using System.Collections;

namespace SylverInk.Interop;

/// <summary>
/// A simple collection class with a <seealso cref="List"/> as its backing, which supports stack-like functions such as push-pop behavior.
/// </summary>
public class LinkedStack<T> : IEnumerable<T>
{
    private readonly List<T> _collection;

    public LinkedStack()
    {
        _collection = [];
    }

    public void Clear() => _collection.Clear();

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)_collection).GetEnumerator();
    }

    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)_collection).GetEnumerator();
    }

    public T? Peek(int depth = 0)
    {
        int index = _collection.Count - 1 - depth;
        if (index < 0)
            return default;

        T item = _collection[index];
        return item;
    }

    public T Pop()
    {
        if (_collection.Count == 0)
            throw new InvalidOperationException(Strings.AttemptedPopEmptyStack);

        int index = _collection.Count - 1;
        T item = _collection[index];
        _collection.RemoveAt(index);
        return item;
    }

    public void Push(T item) => _collection.Add(item);

    public void Remove(T item) => _collection.Remove(item);

    public void RemoveAt(int index) => _collection.RemoveAt(index);

    public T? Seek(int depth = 0)
    {
        T item = _collection[depth];
        return item;
    }
}

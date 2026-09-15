using System.Collections;

namespace SylverInk.Interop;

/// <summary>
/// A simple collection class with a List as its backing, which supports stack-like functions such as push-pop behavior.
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

    public T Pop()
    {
        int index = _collection.Count - 1;
        T item = _collection[index];
        _collection.RemoveAt(index);
        return item;
    }

    public void Push(T item) => _collection.Add(item);

    public void Remove(T item) => _collection.Remove(item);

    public void RemoveAt(int index) => _collection.RemoveAt(index);
}

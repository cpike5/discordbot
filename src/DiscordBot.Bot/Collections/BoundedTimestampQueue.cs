namespace DiscordBot.Bot.Collections;

/// <summary>
/// Interface for items that have a timestamp for time-based filtering.
/// </summary>
public interface ITimestamped
{
    /// <summary>
    /// Gets the timestamp of this item.
    /// </summary>
    DateTime Timestamp { get; }
}

/// <summary>
/// Thread-safe bounded queue optimized for time-based records.
/// Uses a circular buffer to prevent memory growth beyond the specified capacity.
/// </summary>
/// <remarks>
/// This collection is designed for high-throughput scenarios where memory bounds
/// must be enforced. When capacity is exceeded, the oldest items are automatically
/// overwritten (FIFO eviction).
/// </remarks>
/// <typeparam name="T">Record type that must implement <see cref="ITimestamped"/>.</typeparam>
public class BoundedTimestampQueue<T> where T : ITimestamped
{
    private readonly T[] _buffer;
    private readonly object _lock = new();
    private int _head;      // Next write position
    private int _count;     // Current item count
    private readonly int _capacity;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoundedTimestampQueue{T}"/> class.
    /// </summary>
    /// <param name="capacity">The maximum number of items the queue can hold.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when capacity is less than or equal to zero.</exception>
    public BoundedTimestampQueue(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        }

        _capacity = capacity;
        _buffer = new T[capacity];
    }

    /// <summary>
    /// Gets the current number of items in the queue.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _count;
            }
        }
    }

    /// <summary>
    /// Gets the maximum capacity of the queue.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Gets a value indicating whether the queue is at capacity.
    /// </summary>
    public bool IsAtCapacity
    {
        get
        {
            lock (_lock)
            {
                return _count >= _capacity;
            }
        }
    }

    /// <summary>
    /// Adds an item to the queue. If the queue is at capacity, the oldest item is overwritten.
    /// </summary>
    /// <param name="item">The item to add.</param>
    public void Enqueue(T item)
    {
        lock (_lock)
        {
            _buffer[_head] = item;
            _head = (_head + 1) % _capacity;

            if (_count < _capacity)
            {
                _count++;
            }
        }
    }

    /// <summary>
    /// Gets all items with a timestamp on or after the specified cutoff time.
    /// </summary>
    /// <param name="cutoff">The minimum timestamp (inclusive).</param>
    /// <returns>A read-only list of items matching the time criteria, in chronological order.</returns>
    public IReadOnlyList<T> GetItemsAfter(DateTime cutoff)
    {
        lock (_lock)
        {
            var result = new List<T>(_count);
            var start = (_head - _count + _capacity) % _capacity;

            for (int i = 0; i < _count; i++)
            {
                var index = (start + i) % _capacity;
                if (_buffer[index].Timestamp >= cutoff)
                {
                    result.Add(_buffer[index]);
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Counts items with a timestamp on or after the specified cutoff time.
    /// </summary>
    /// <param name="cutoff">The minimum timestamp (inclusive).</param>
    /// <returns>The number of items matching the time criteria.</returns>
    public int CountAfter(DateTime cutoff)
    {
        lock (_lock)
        {
            var count = 0;
            var start = (_head - _count + _capacity) % _capacity;

            for (int i = 0; i < _count; i++)
            {
                var index = (start + i) % _capacity;
                if (_buffer[index].Timestamp >= cutoff)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>
    /// Counts items matching both the time cutoff and a custom predicate.
    /// </summary>
    /// <param name="cutoff">The minimum timestamp (inclusive).</param>
    /// <param name="predicate">Additional filter criteria.</param>
    /// <returns>The number of items matching both criteria.</returns>
    public int CountAfterWithPredicate(DateTime cutoff, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        lock (_lock)
        {
            var count = 0;
            var start = (_head - _count + _capacity) % _capacity;

            for (int i = 0; i < _count; i++)
            {
                var index = (start + i) % _capacity;
                var item = _buffer[index];
                if (item.Timestamp >= cutoff && predicate(item))
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>
    /// Calculates the estimated memory usage of this queue in bytes.
    /// </summary>
    /// <param name="bytesPerItem">The estimated size of each item in bytes.</param>
    /// <returns>The estimated total memory usage in bytes.</returns>
    public long EstimatedBytes(int bytesPerItem)
    {
        // Buffer capacity * item size + object overhead (lock, fields, array reference)
        return (_capacity * bytesPerItem) + 64;
    }

    /// <summary>
    /// Clears all items from the queue.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_buffer, 0, _capacity);
            _head = 0;
            _count = 0;
        }
    }
}

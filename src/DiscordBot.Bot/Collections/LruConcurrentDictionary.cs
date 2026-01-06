namespace DiscordBot.Bot.Collections;

/// <summary>
/// Thread-safe dictionary with LRU (Least Recently Used) eviction when capacity is exceeded.
/// </summary>
/// <remarks>
/// This collection is designed for scenarios where you need bounded memory usage
/// with automatic eviction of the least recently accessed items. Access order is
/// tracked and updated on both reads and writes.
/// </remarks>
/// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
public class LruConcurrentDictionary<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _cache;
    private readonly LinkedList<(TKey Key, TValue Value)> _lruList;
    private readonly int _capacity;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="LruConcurrentDictionary{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="capacity">The maximum number of items the dictionary can hold before eviction occurs.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when capacity is less than or equal to zero.</exception>
    public LruConcurrentDictionary(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        }

        _capacity = capacity;
        _cache = new Dictionary<TKey, LinkedListNode<(TKey, TValue)>>(capacity);
        _lruList = new LinkedList<(TKey, TValue)>();
    }

    /// <summary>
    /// Gets the current number of items in the dictionary.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _cache.Count;
            }
        }
    }

    /// <summary>
    /// Gets the maximum capacity of the dictionary.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Gets or adds a value to the dictionary. If the key exists, moves it to the front (most recently used).
    /// If adding a new key would exceed capacity, the least recently used item is evicted.
    /// </summary>
    /// <param name="key">The key to look up or add.</param>
    /// <param name="valueFactory">A factory function to create the value if the key is not present.</param>
    /// <returns>The existing or newly created value.</returns>
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
    {
        ArgumentNullException.ThrowIfNull(valueFactory);

        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var existingNode))
            {
                // Move to front (most recently used)
                _lruList.Remove(existingNode);
                _lruList.AddFirst(existingNode);
                return existingNode.Value.Value;
            }

            // Create new entry
            var value = valueFactory(key);
            var newNode = _lruList.AddFirst((key, value));
            _cache[key] = newNode;

            // Evict LRU entries if over capacity
            while (_cache.Count > _capacity)
            {
                var lruNode = _lruList.Last!;
                _lruList.RemoveLast();
                _cache.Remove(lruNode.Value.Key);
            }

            return value;
        }
    }

    /// <summary>
    /// Attempts to get the value associated with the specified key.
    /// If found, the item is moved to the front (most recently used).
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">When this method returns, contains the value if found; otherwise, the default value.</param>
    /// <returns>true if the key was found; otherwise, false.</returns>
    public bool TryGetValue(TKey key, out TValue? value)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                // Move to front (most recently used)
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                value = node.Value.Value;
                return true;
            }

            value = default;
            return false;
        }
    }

    /// <summary>
    /// Adds or updates a value in the dictionary.
    /// If the key exists, updates the value and moves to front.
    /// If adding a new key would exceed capacity, the least recently used item is evicted.
    /// </summary>
    /// <param name="key">The key to add or update.</param>
    /// <param name="value">The value to set.</param>
    public void AddOrUpdate(TKey key, TValue value)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var existingNode))
            {
                // Remove old node
                _lruList.Remove(existingNode);
            }

            // Add new node at front
            var newNode = _lruList.AddFirst((key, value));
            _cache[key] = newNode;

            // Evict LRU entries if over capacity
            while (_cache.Count > _capacity)
            {
                var lruNode = _lruList.Last!;
                _lruList.RemoveLast();
                _cache.Remove(lruNode.Value.Key);
            }
        }
    }

    /// <summary>
    /// Removes the item with the specified key from the dictionary.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns>true if the item was found and removed; otherwise, false.</returns>
    public bool Remove(TKey key)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                _lruList.Remove(node);
                _cache.Remove(key);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Checks if the dictionary contains the specified key.
    /// Note: This does NOT update the LRU order.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>true if the key exists; otherwise, false.</returns>
    public bool ContainsKey(TKey key)
    {
        lock (_lock)
        {
            return _cache.ContainsKey(key);
        }
    }

    /// <summary>
    /// Gets all entries in the dictionary in LRU order (most recently used first).
    /// </summary>
    /// <returns>A list of key-value pairs in LRU order.</returns>
    public IReadOnlyList<KeyValuePair<TKey, TValue>> GetAll()
    {
        lock (_lock)
        {
            return _lruList
                .Select(x => new KeyValuePair<TKey, TValue>(x.Key, x.Value))
                .ToList();
        }
    }

    /// <summary>
    /// Gets all keys in the dictionary.
    /// </summary>
    /// <returns>A collection of all keys.</returns>
    public IReadOnlyCollection<TKey> Keys
    {
        get
        {
            lock (_lock)
            {
                return _cache.Keys.ToList();
            }
        }
    }

    /// <summary>
    /// Clears all items from the dictionary.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _lruList.Clear();
        }
    }

    /// <summary>
    /// Calculates the estimated memory usage of this dictionary in bytes.
    /// </summary>
    /// <param name="bytesPerKey">The estimated size of each key in bytes.</param>
    /// <param name="bytesPerValue">The estimated size of each value in bytes.</param>
    /// <returns>The estimated total memory usage in bytes.</returns>
    public long EstimatedBytes(int bytesPerKey, int bytesPerValue)
    {
        lock (_lock)
        {
            // LinkedListNode overhead: ~40 bytes per node (prev, next, list references + value tuple)
            // Dictionary entry overhead: ~24 bytes per entry (key hash, bucket index, value reference)
            const int nodeOverhead = 40;
            const int dictEntryOverhead = 24;

            var perEntryBytes = bytesPerKey + bytesPerValue + nodeOverhead + dictEntryOverhead;
            return (_cache.Count * perEntryBytes) + 64; // +64 for object overhead
        }
    }
}

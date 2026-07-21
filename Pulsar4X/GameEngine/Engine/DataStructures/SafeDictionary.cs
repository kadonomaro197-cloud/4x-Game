using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Pulsar4X.Engine;
using Pulsar4X.Industry;

namespace Pulsar4X.DataStructures
{

    public interface ISafeDictionary
    {
        object this[int index] { get; set; }
    }

    [JsonConverter(typeof(SafeDictionaryConverter))]
    public class SafeDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>, IEquatable<SafeDictionary<TKey, TValue>>, ISafeDictionary
    {
        object ISafeDictionary.this[int index]
        {
            get
            {
                TKey key = (TKey)(object)index;
                if (this.TryGetValue(key, out TValue? value))
                {
                    return value;
                }
                else
                {
                    throw new KeyNotFoundException($"Key {index} not found in the dictionary.");
                }
            }
            set => this[(TKey)(object)index] = (TValue)value;
        }

        private readonly Dictionary<TKey, TValue> _innerDictionary = new Dictionary<TKey, TValue>();
        private readonly object _lock = new object();
        public delegate void DictionaryChangedHandler(TKey key, TValue value);

        public event DictionaryChangedHandler ItemAdded;
        public event DictionaryChangedHandler ItemRemoved;
        public event DictionaryChangedHandler OnChange;

        public int Count
        {
            get
            {
                lock(_lock) return _innerDictionary.Count;
            }
        }

        public Dictionary<TKey, TValue>.KeyCollection Keys
        {
            get
            {
                lock(_lock) return _innerDictionary.Keys;
            }
        }

        public Dictionary<TKey, TValue>.ValueCollection Values
        {
            get
            {
                lock(_lock) return _innerDictionary.Values;
            }
        }

        /// <summary>
        /// A point-in-time COPY of the values, taken under the lock. Unlike <see cref="Values"/> — which returns the
        /// live <see cref="Dictionary{TKey,TValue}.ValueCollection"/> and releases the lock BEFORE the caller
        /// enumerates it — this list is safe to enumerate while another thread mutates the dictionary (no
        /// "collection was modified" throw). Prefer this anywhere the result is enumerated off the writer's thread
        /// (e.g. the parallel sim loop reading entity/blob stores). The copy is a cheap List ctor over the backing
        /// collection.
        /// </summary>
        public List<TValue> ValuesSnapshot
        {
            get
            {
                lock(_lock) return new List<TValue>(_innerDictionary.Values);
            }
        }

        /// <summary>
        /// A point-in-time COPY of the keys, taken under the lock. Same rationale as <see cref="ValuesSnapshot"/>.
        /// </summary>
        public List<TKey> KeysSnapshot
        {
            get
            {
                lock(_lock) return new List<TKey>(_innerDictionary.Keys);
            }
        }

        public SafeDictionary() { }
        public SafeDictionary(IDictionary<TKey, TValue> dictionary)
        {
            foreach(var (key, value) in dictionary)
            {
                _innerDictionary.Add(key, value);
            }
        }

        public SafeDictionary(SafeDictionary<TKey, TValue> dictionary)
        {
            foreach(var (key, value) in dictionary)
            {
                _innerDictionary.Add(key, value);
            }
        }

        public TValue this[TKey key]
        {
            get
            {
                lock(_lock) return _innerDictionary[key];
            }
            set
            {
                // Copy-then-notify: mutate under the lock, capture the handler under the lock, but INVOKE the handler
                // after releasing it. Raising events while holding _lock let arbitrary subscriber code run under the
                // lock — a re-entrant/cross-thread deadlock and contention hazard on the parallel sim threads.
                DictionaryChangedHandler? onChange;
                lock(_lock)
                {
                    _innerDictionary[key] = value;
                    onChange = OnChange;
                }
                onChange?.Invoke(key, value);
            }
        }

        public void Add(TKey key, TValue value)
        {
            // Copy-then-notify (see the indexer setter): raise events AFTER the lock is released.
            DictionaryChangedHandler? itemAdded;
            DictionaryChangedHandler? onChange;
            lock(_lock)
            {
                _innerDictionary.Add(key, value);
                itemAdded = ItemAdded;
                onChange = OnChange;
            }
            itemAdded?.Invoke(key, value);
            onChange?.Invoke(key, value);
        }

        public bool Remove(TKey key)
        {
            // Copy-then-notify (see the indexer setter): raise events AFTER the lock is released.
            DictionaryChangedHandler? itemRemoved;
            DictionaryChangedHandler? onChange;
            TValue? value;
            lock(_lock)
            {
                if(!_innerDictionary.TryGetValue(key, out value))
                {
                    return false;
                }
                _innerDictionary.Remove(key);
                itemRemoved = ItemRemoved;
                onChange = OnChange;
            }
            itemRemoved?.Invoke(key, value);
            onChange?.Invoke(key, value);
            return true;
        }

        public bool ContainsKey(TKey key)
        {
            lock(_lock) return _innerDictionary.ContainsKey(key);
        }

        public void Clear()
        {
            lock(_lock) _innerDictionary.Clear();
        }

        public bool TryGetValue(TKey key, [NotNullWhen(true)] out TValue? value)
        {
            lock(_lock)
            {
                if(_innerDictionary.ContainsKey(key))
                {
                    value = _innerDictionary[key];
                    if (value is null)
                    {
                        throw new InvalidOperationException("Unexpected null value in the dictionary.");
                    }
                    return true;
                }
                value = default(TValue);
                return false;
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            List<KeyValuePair<TKey, TValue>> snapshot;
            lock(_lock)
            {
                snapshot = _innerDictionary.ToList();
            }
            return snapshot.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Equals(SafeDictionary<TKey, TValue>? other)
        {
            if(other is null) return false;
            if(ReferenceEquals(this, other)) return true;

            lock(_lock)
            {
                lock(other._lock)
                {
                    if(_innerDictionary.Count != other._innerDictionary.Count)
                    {
                        return false;
                    }

                    foreach(var kvp in _innerDictionary)
                    {
                        if (!other._innerDictionary.TryGetValue(kvp.Key, out var value))
                            return false;

                        if (!EqualityComparer<TValue>.Default.Equals(value, kvp.Value))
                            return false;
                    }

                    return true;
                }
            }
        }

        /// <summary>
        /// Needed for serialization
        /// </summary>
        [JsonIgnore]
        internal Dictionary<TKey, TValue> InnerDictionary
        {
            get
            {
                lock(_lock)
                {
                    return new Dictionary<TKey, TValue>(_innerDictionary);
                }
            }
        }
    }

    public class SafeDictionaryConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(SafeDictionary<,>);
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            Type keyType = objectType.GetGenericArguments()[0];
            Type baseValueType = objectType.GetGenericArguments()[1];

            var baseDictType = typeof(Dictionary<,>).MakeGenericType(keyType, baseValueType);
            var innerDict = serializer.Deserialize(reader, baseDictType) as IDictionary;
            var result = Activator.CreateInstance(objectType, innerDict);
            return result;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            var objectType = value.GetType();
            var innerDictionaryProperty = objectType.GetProperty("InnerDictionary", BindingFlags.NonPublic | BindingFlags.Instance);
            var innerDictionaryValue = innerDictionaryProperty.GetValue(value);
            serializer.Serialize(writer, innerDictionaryValue);
        }

        private Type GetDerivedType(Type baseType)
        {
            if(baseType == typeof(EntityManager))
            {
                return typeof(StarSystem);
            }
            return baseType;
        }
    }

}
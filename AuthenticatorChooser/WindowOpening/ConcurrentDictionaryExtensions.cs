using System.Collections.Concurrent;

namespace AuthenticatorChooser.WindowOpening;

public static class ConcurrentDictionaryExtensions {

    public static ConcurrentDictionary<K, ValueHolder<V>> createConcurrentDictionary<K, V>() where K: notnull {
        return new ConcurrentDictionary<K, ValueHolder<V>>();
    }

    public static V exchangeEnum<K, V>(this ConcurrentDictionary<K, ValueHolder<int>> dictionary, K key, V newValue) where V: Enum where K: notnull {
        int newValueNumeric = (int) (object) newValue;
        return (V) Enum.ToObject(typeof(V), Interlocked.Exchange(ref dictionary.GetOrAdd(key, new ValueHolder<int>(newValueNumeric)).value, newValueNumeric));
    }

    public static V exchangeEnum<K, V>(this ConcurrentDictionary<K, ValueHolder<uint>> dictionary, K key, V newValue) where V: Enum where K: notnull {
        uint newValueNumeric = (uint) (object) newValue;
        return (V) Enum.ToObject(typeof(V), Interlocked.Exchange(ref dictionary.GetOrAdd(key, new ValueHolder<uint>(newValueNumeric)).value, newValueNumeric));
    }

    public static V exchangeEnum<K, V>(this ConcurrentDictionary<K, ValueHolder<long>> dictionary, K key, V newValue) where V: Enum where K: notnull {
        long newValueNumeric = (long) (object) newValue;
        return (V) Enum.ToObject(typeof(V), Interlocked.Exchange(ref dictionary.GetOrAdd(key, new ValueHolder<long>(newValueNumeric)).value, newValueNumeric));
    }

    public static V exchangeEnum<K, V>(this ConcurrentDictionary<K, ValueHolder<ulong>> dictionary, K key, V newValue) where V: Enum where K: notnull {
        ulong newValueNumeric = (ulong) (object) newValue;
        return (V) Enum.ToObject(typeof(V), Interlocked.Exchange(ref dictionary.GetOrAdd(key, new ValueHolder<ulong>(newValueNumeric)).value, newValueNumeric));
    }

    public static V exchange<K, V>(this ConcurrentDictionary<K, ValueHolder<V>> dictionary, K key, V newValue) where V: class where K: notnull {
        return Interlocked.Exchange(ref dictionary.GetOrAdd(key, new ValueHolder<V>(newValue)).value, newValue);
    }

    public static long exchange<K>(this ConcurrentDictionary<K, ValueHolder<long>> dictionary, K key, long newValue) where K: notnull {
        return Interlocked.Exchange(ref dictionary.GetOrAdd(key, new ValueHolder<long>(newValue)).value, newValue);
    }

    public static int exchange<K>(this ConcurrentDictionary<K, ValueHolder<int>> dictionary, K key, int newValue) where K: notnull {
        return Interlocked.Exchange(ref dictionary.GetOrAdd(key, new ValueHolder<int>(newValue)).value, newValue);
    }

    public static double exchange<K>(this ConcurrentDictionary<K, ValueHolder<double>> dictionary, K key, double newValue) where K: notnull {
        return Interlocked.Exchange(ref dictionary.GetOrAdd(key, new ValueHolder<double>(newValue)).value, newValue);
    }

}

public class ValueHolder<T>(T value) {

    public T value = value;

}
using System.Collections;

namespace ServerPractic;

public static class Extensions
{
    extension(IDictionary dictionary)
    {
        public T? TryGetValue<T>(string key, T? defaultValue = default)
        {
            return dictionary.Contains(key) ? (T?)dictionary[key] : defaultValue;
        }

        public bool TryGetValue<T>(string key, out T value)
            where T : notnull
        {
            try
            {
                if (dictionary.Contains(key))
                {
                    value = (T)(dictionary[key] ?? throw new KeyNotFoundException($"Key {key} not found"));
                    return true;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            value = default;
            return false;
        }
    }
}
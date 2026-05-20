using System.Collections;

namespace ServerPractice2026Spring.Tools;

public static class Extensions
{
    public static T? TryGetValue<T>(this IDictionary dictionary, string key, T? defaultValue = default)
    {
        return dictionary.Contains(key) ? (T?)dictionary[key] : defaultValue;
    }
    
    public static bool TryGetValue<T>(this IDictionary dictionary, string key, out T value)
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
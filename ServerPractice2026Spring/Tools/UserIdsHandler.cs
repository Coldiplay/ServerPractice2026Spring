using System.Collections.Concurrent;

namespace ServerPractice2026Spring.Tools;

public class UserIdsHandler
{
    private readonly ConcurrentDictionary<string, List<string>> _userIds = [];

    public bool Add(string userLogin, string connectionId)
    {
        if (_userIds.ContainsKey(userLogin))
        {
            _userIds.TryGetValue(userLogin, out var connectionIds);
            connectionIds?.Add(connectionId);
        }
        else
        {
            _userIds.TryAdd(userLogin, [connectionId]);
        }
        
        return true;
    }

    public bool Remove(string? connectionId)
    {
        if (connectionId is null) return false;
        var pair = _userIds.FirstOrDefault(x => x.Value.Contains(connectionId));
        if (pair.Key is null) return true;
        pair.Value.Remove(connectionId);
        if (pair.Value.Count == 0)
            _userIds.TryRemove(pair.Key, out _);
        return true;
    }

    public string? GetLogin(string? connectionId)
    {
        return connectionId is null 
            ? null 
            : _userIds.FirstOrDefault(x => x.Value.Contains(connectionId)).Key;
    }

    public List<string>? GetConnectionIds(string? login)
    {
        return string.IsNullOrEmpty(login) 
            ? null 
            : _userIds.GetValueOrDefault(login);
    }
}
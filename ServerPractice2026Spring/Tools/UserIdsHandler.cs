using System.Collections.Concurrent;

namespace ServerPractice2026Spring.Tools;

public class UserIdsHandler
{
    private readonly ConcurrentDictionary<string, List<Guid>> _userIds = [];

    public bool Add(string userLogin, Guid id)
    {
        if (_userIds.ContainsKey(userLogin))
        {
            _userIds.TryGetValue(userLogin, out var connectionIds);
            connectionIds?.Add(id);
        }
        else
        {
            _userIds.TryAdd(userLogin, [id]);
        }
        
        return true;
    }

    public bool Remove(Guid? connectionId)
    {
        if (connectionId is null) return false;
        var pair = _userIds.FirstOrDefault(x => x.Value.Contains((Guid)connectionId));
        if (pair.Key is null) return true;
        pair.Value.Remove((Guid)connectionId);
        if (pair.Value.Count == 0)
            _userIds.TryRemove(pair.Key, out _);
        return true;
    }

    public string? GetLogin(Guid? id)
    {
        return id is null 
            ? null 
            : _userIds.FirstOrDefault(x => x.Value.Contains((Guid)id)).Key;
    }

    public List<Guid>? GetConnectionIds(string? login)
    {
        return string.IsNullOrEmpty(login) 
            ? null 
            : _userIds.GetValueOrDefault(login);
    }
}
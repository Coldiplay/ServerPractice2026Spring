namespace ServerPractic.Model;

public class UserIdsHandler
{
    private readonly Dictionary<string, Guid> _userIds = new Dictionary<string, Guid>();

    public bool Add(string userLogin, Guid id)
    {
        _userIds.Add(userLogin, id);
        return true;
    }

    public bool Remove(string userLogin)
    {
        return _userIds.Remove(userLogin);
    }

    public string? GetLogin(Guid id)
    {
        return _userIds.ContainsValue(id) 
            ? _userIds.First(x => x.Value == id).Key 
            : null;
    }

    public Guid? GetUserId(string login)
    {
        if (_userIds.TryGetValue(login, out var userId)) return userId;
        
        return null;
    }
}
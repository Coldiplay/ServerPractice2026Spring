namespace ServerPractice2026Spring.Tools;

internal static class Options
{
    static Options()
    {
        var data = Environment.GetEnvironmentVariables();
        if (!data.TryGetValue("MOBILE_SIGNALR_ISSUER", out Issuer))
            Issuer = "Issuer";
        if (!data.TryGetValue("MOBILE_SIGNALR_AUDIENCE", out Audience))
            Audience = "Audience";

        if (data.TryGetValue("MOBILE_SIGNALR_SECRET", out RSA)) return;
        
        try
        {
            RSA = File.ReadAllText("private.key");
        }
        catch (Exception e)
        {
            RSA = System.Security.Cryptography.RSA.Create(2048).ToXmlString(true);
        }
    }

    internal static readonly string RSA;
    internal static readonly string Issuer;
    internal static readonly string Audience;

    internal const string HubName = "chatHub";
    internal const string HubPath = "/chatHub";
}
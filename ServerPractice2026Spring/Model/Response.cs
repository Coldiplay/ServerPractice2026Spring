using System.Net;

namespace ServerPractice2026Spring.Model;

public class Response
{
    public object? Data { get; set; }
    public string Message { get; set; }
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.NotFound;
}
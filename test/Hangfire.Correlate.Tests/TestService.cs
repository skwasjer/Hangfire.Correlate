namespace Hangfire.Correlate;

public class TestService
{
    private readonly HttpClient _client;

    public TestService(HttpClient client)
    {
        _client = client;
    }

    public Task<string> CallApi()
    {
        return _client.GetStringAsync("");
    }
}

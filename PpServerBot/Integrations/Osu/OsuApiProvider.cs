using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace PpServerBot.Integrations.Osu;

public class OsuApiProvider
{
    private readonly HttpClient _httpClient;

    public OsuApiProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<OsuUser?> GetUser(string token)
    {
        var requestMessage = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri("https://osu.ppy.sh/api/v2/me"),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) }
        };

        var response = await _httpClient.SendAsync(requestMessage);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<OsuUser>();
    }
}

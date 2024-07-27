using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace PpServerBot;

public class OsuUser
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public string Playmode { get; set; } = null!;

    [JsonPropertyName("avatar_url")]
    public string AvatarUrl { get; set; } = null!;

    [JsonPropertyName("statistics_rulesets")]
    public RulesetStatistics Statistics { get; set; } = null!;

    public class RulesetStatistics
    {
        public UserStatistics Osu { get; set; } = null!;
        public UserStatistics Taiko { get; set; } = null!;
        public UserStatistics Fruits { get; set; } = null!;
        public UserStatistics Mania { get; set; } = null!;
    }

    public class UserStatistics
    {
        [JsonPropertyName("play_count")]
        public uint Playcount { get; set; }

        [JsonPropertyName("pp")]
        public double Pp { get; set; }

        [JsonPropertyName("global_rank")]
        public uint GlobalRank { get; set; }

        [JsonPropertyName("is_ranked")] 
        public bool HasRank { get; set; }
    }
}

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

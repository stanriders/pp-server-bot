using System.Text.Json.Serialization;

namespace PpServerBot.Integrations.Osu;

public class OsuUser
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public string Playmode { get; set; } = null!;

    [JsonPropertyName("avatar_url")]
    public string AvatarUrl { get; set; } = null!;

    [JsonPropertyName("statistics_rulesets")]
    public RulesetStatistics? Statistics { get; set; } = null!;

    public class RulesetStatistics
    {
        public UserStatistics? Osu { get; set; } = null!;
        public UserStatistics? Taiko { get; set; } = null!;
        public UserStatistics? Fruits { get; set; } = null!;
        public UserStatistics? Mania { get; set; } = null!;
    }

    public class UserStatistics
    {
        [JsonPropertyName("play_count")]
        public uint Playcount { get; set; }

        [JsonPropertyName("pp")]
        public double Pp { get; set; }

        [JsonPropertyName("global_rank")]
        public uint? GlobalRank { get; set; }

        [JsonPropertyName("is_ranked")]
        public bool HasRank { get; set; }
    }
}

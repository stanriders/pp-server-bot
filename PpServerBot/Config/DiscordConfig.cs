namespace PpServerBot.Config
{
    public class DiscordConfig
    {
        public string Token { get; set; } = null!;

        public ulong GuildId { get; set; }
        public ulong ApplicationChannelId { get; set; }
        public ulong VerifiedChannelId { get; set; }
        public ulong OnionVerifiedChannelId { get; set; }

        public string VerifyMessage { get; set; } = null!;
        public bool DisableOnionApplication { get; set; }

        public Roles Roles { get; set; } = null!;
    }

    public class Roles
    {
        public ulong Verified { get; set; }
        public ulong Onion { get; set; }

        public ulong[] Osu { get; set; } = null!;
        public ulong[] Taiko { get; set; } = null!;
        public ulong[] Catch { get; set; } = null!;
        public ulong[] Mania { get; set; } = null!;
    }
}

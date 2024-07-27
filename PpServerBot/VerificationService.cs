using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using System.Text;
using static PpServerBot.OsuUser;

namespace PpServerBot
{
    public class Verification
    {
        public Guid Id { get; set; }
        public ulong DiscordId { get; set; }

        public bool Onion { get; set; }
        public string? OnionApplication { get; set; }
    }

    public class VerificationService
    {
        private readonly List<Verification> _verifications = new();

        private readonly ILogger<VerificationService> _logger;
        private readonly DiscordSocketClient _discordSocketClient;
        private readonly HuisApiProvider _huisApiProvider;
        private readonly OsuApiProvider _osuApiProvider;

        private readonly DiscordConfig _discordConfig;

        public VerificationService(DiscordSocketClient discordSocketClient, 
            ILogger<VerificationService> logger, 
            HuisApiProvider huisApiProvider, 
            OsuApiProvider osuApiProvider,
            IOptions<DiscordConfig> configuration)
        {
            _discordSocketClient = discordSocketClient;
            _logger = logger;
            _huisApiProvider = huisApiProvider;
            _osuApiProvider = osuApiProvider;

            _discordConfig = configuration.Value;
        }

        public Guid Start(ulong discordId, bool onion, string? onionApplication = null)
        {
            var id = Guid.NewGuid();

            _verifications.Add(new Verification
            {
                Id = id,
                Onion = onion,
                DiscordId = discordId,
                OnionApplication = onionApplication
            });

            return id;
        }

        public async Task<bool> Finish(Guid id, string osuAccessToken)
        {
            OsuUser? user;
            try
            {
                user = await _osuApiProvider.GetUser(osuAccessToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "osu!api provider error!");
                return false;
            }

            if (user == null)
            {
                _logger.LogWarning("Failed to log in user {Id} - osu API query failed!", id);
                return false;
            }

            var verification = _verifications.FirstOrDefault(x=> x.Id == id);
            if (verification == null)
            {
                _logger.LogWarning("Failed to log in user {Id} - unknown verification id!", id);
                return false;
            }

            var guild = _discordSocketClient.GetGuild(_discordConfig.GuildId);
            if (guild == null)
            {
                _logger.LogError("Guild {GuildId} not found!", _discordConfig.GuildId);
                return false;
            }

            var discordUser = guild.GetUser(verification.DiscordId);
            if (discordUser == null)
            {
                _logger.LogError("User {DiscordId} not found!", verification.DiscordId);
                return false;
            }

            var addeRoleIds = await AddRoles(user, discordUser);
            var addedRoles = guild.Roles.Where(x => addeRoleIds.Contains(x.Id)).ToList();

            await RenameUser(user, discordUser);

            discordUser = guild.GetUser(verification.DiscordId);

            if (verification.Onion)
            {
                var onionChannel = guild.GetTextChannel(_discordConfig.OnionVerifiedChannelId);
                if (onionChannel == null)
                {
                    _logger.LogError("Onion application channel {Channel} not found!", onionChannel);
                    return false;
                }

                var components = new ComponentBuilder()
                    .WithButton("Add onion", $"add-onion-{user.Id}-{discordUser.Id}", ButtonStyle.Success)
                    .Build();

                await onionChannel.SendMessageAsync(
                    embed: BuildApplicationEmbed(user, discordUser, verification, addedRoles), components: components);

                _logger.LogInformation("Sent onion application for user {DiscordId} (osu id: {OsuId})", discordUser.Id, user.Id);
            }
            else
            {
                var verifiedChannel = guild.GetTextChannel(_discordConfig.VerifiedChannelId);
                if (verifiedChannel == null)
                {
                    _logger.LogError("Verified list channel {Channel} not found!", verifiedChannel);
                    return false;
                }

                await verifiedChannel.SendMessageAsync(
                    embed: BuildApplicationEmbed(user, discordUser, verification, addedRoles));

                _logger.LogInformation("Sent verification application for user {DiscordId} (osu id: {OsuId})", discordUser.Id, user.Id);
            }

            _verifications.Remove(verification);

            _logger.LogInformation("User {User} (id: {Id}) verified!", user.Id, id);
            return true;
        }

        public async Task ApplyOnion(int osuId, ulong discordId)
        {
            var guild = _discordSocketClient.GetGuild(_discordConfig.GuildId);
            if (guild == null)
            {
                _logger.LogError("Guild {GuildId} not found!", _discordConfig.GuildId);
                return;
            }

            var discordUser = guild.GetUser(discordId);

            _logger.LogInformation("Adding onion to user {DiscordId} (osu id: {OsuId})...", discordId, osuId);

            await _huisApiProvider.AddOnion(osuId, discordId);
            await discordUser.AddRoleAsync(_discordConfig.Roles.Onion);
        }

        public async Task RemoveOnion(ulong discordId)
        {
            var guild = _discordSocketClient.GetGuild(_discordConfig.GuildId);
            if (guild == null)
            {
                _logger.LogError("Guild {GuildId} not found!", _discordConfig.GuildId);
                return;
            }

            var discordUser = guild.GetUser(discordId);

            _logger.LogInformation("Removing onion from user {DiscordId} (osu id: ?XD)...", discordId);

            await _huisApiProvider.RemoveOnion(discordId);
            await discordUser.RemoveRoleAsync(_discordConfig.Roles.Onion);
        }

        private async Task<List<ulong>> AddRoles(OsuUser osuUser, SocketGuildUser discordUser)
        {
            var addedRoleIds = new List<ulong> { _discordConfig.Roles.Verified };

            try
            {
                await discordUser.AddRoleAsync(_discordConfig.Roles.Verified);

                addedRoleIds.Add(await AddRulesetRole(osuUser.Statistics.Osu, _discordConfig.Roles.Osu, discordUser));
                addedRoleIds.Add(await AddRulesetRole(osuUser.Statistics.Taiko, _discordConfig.Roles.Taiko, discordUser));
                addedRoleIds.Add(await AddRulesetRole(osuUser.Statistics.Fruits, _discordConfig.Roles.Catch, discordUser));
                addedRoleIds.Add(await AddRulesetRole(osuUser.Statistics.Mania, _discordConfig.Roles.Mania, discordUser));

                _logger.LogInformation("Added {Count} roles to user {DiscordId} (osu id: {OsuId})", addedRoleIds.Count, discordUser.Id, osuUser.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add roles to user {DiscordId} (osu id: {OsuId})", discordUser.Id, osuUser.Id);
                return new List<ulong>();
            }

            return addedRoleIds;
        }

        private async Task RenameUser(OsuUser osuUser, SocketGuildUser discordUser)
        {
            if (discordUser.DisplayName != osuUser.Username)
            {
                try
                {
                    await discordUser.ModifyAsync(properties => properties.Nickname = osuUser.Username);
                    _logger.LogInformation("Renamed user {DiscordId} (osu id: {OsuId})", discordUser.Id, osuUser.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to modify user {DiscordId} (osu id: {OsuId})", discordUser.Id,
                        osuUser.Id);
                }
            }
        }

        private Embed BuildApplicationEmbed(OsuUser osuUser, SocketGuildUser discordUser, Verification verification, List<SocketRole> addedRoles)
        {
            var osuLink = $"https://osu.ppy.sh/users/{osuUser.Id}";

            var descriptionBuilder = new StringBuilder();
            descriptionBuilder.Append($"**Discord**: {discordUser.Mention}\n\n");

            if (verification.Onion)
            {
                descriptionBuilder.Append($"**Onion application**: ```{verification.OnionApplication}```\n");
            }

            descriptionBuilder.Append($"<:osu:1266724120490541150> {BuildRulesetApplicationLine(osuUser.Statistics.Osu)}\n");
            descriptionBuilder.Append($"<:taiko:1266724145484529705> {BuildRulesetApplicationLine(osuUser.Statistics.Taiko)}\n");
            descriptionBuilder.Append($"<:mania:1266724133337698324> {BuildRulesetApplicationLine(osuUser.Statistics.Mania)}\n");
            descriptionBuilder.Append($"<:catch:1266724102274682951> {BuildRulesetApplicationLine(osuUser.Statistics.Fruits)}\n\n");
            
            if (addedRoles.Count != 0)
                descriptionBuilder.Append($"**Added roles**: {string.Join(' ', addedRoles.Select(x=> x.Mention))}");
            else
                descriptionBuilder.Append("No new roles added!");

            var builder = new EmbedBuilder()
                .WithAuthor($"{osuUser.Username} (⭐ {osuUser.Playmode})", osuUser.AvatarUrl, osuLink)
                .WithTitle($"✅ {discordUser.Username} has been verified!")
                .WithUrl(osuLink)
                .WithDescription(descriptionBuilder.ToString())
                .WithThumbnailUrl(discordUser.GetAvatarUrl())
                .WithColor(Color.Blue);

            return builder.Build();
        }

        private async Task<ulong> AddRulesetRole(UserStatistics statistics, ulong[] roles, SocketGuildUser discordUser)
        {
            ulong roleId;

            if (statistics.GlobalRank < 10)
            {
                roleId = roles[0];
            } 
            else if (statistics.GlobalRank < 100)
            {
                roleId = roles[1];
            }
            else if (statistics.GlobalRank < 1000)
            {
                roleId = roles[2];
            }
            else if (statistics.GlobalRank < 10000)
            {
                roleId = roles[3];
            }
            else
            {
                roleId = roles[4];
            }

            await discordUser.AddRoleAsync(roleId);
            return roleId;
        }

        private string BuildRulesetApplicationLine(OsuUser.UserStatistics statistics)
        {
            return $"#{(statistics.HasRank && statistics.GlobalRank != 0 ? statistics.GlobalRank : "—")}\t({statistics.Pp:N0}pp, \t{statistics.Playcount} playcount)";
        }
    }
}

﻿using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace PpServerBot
{
    public class DiscordService : BackgroundService
    {
        private readonly DiscordSocketClient _client;
        private readonly VerificationService _verificationService;
        private readonly ILogger<DiscordService> _logger;

        private readonly DiscordConfig _discordConfig;

        public DiscordService(IOptions<DiscordConfig> configuration, ILogger<DiscordService> logger, 
            DiscordSocketClient client, VerificationService verificationService)
        {
            _logger = logger;
            _client = client;
            _verificationService = verificationService;

            _discordConfig = configuration.Value;
            
            _client.Log += Log;
            _client.Ready += Ready;
            _client.InteractionCreated += InteractionCreated;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var _ = _logger.BeginScope("Discord");

            _logger.LogInformation("Starting discord service...");

            await _client.LoginAsync(TokenType.Bot, _discordConfig.Token);
            await _client.StartAsync();

            while (!stoppingToken.IsCancellationRequested)
            {
                // do stuff?
                await Task.Delay(1000, stoppingToken);
            }

            await _client.StopAsync();
        }

        private async Task Ready()
        {
            var guild = _client.GetGuild(_discordConfig.GuildId);
            if (guild == null)
            {
                throw new Exception($"Guild {_discordConfig.GuildId} not found!");
            }

            var applicationChannel = guild.GetTextChannel(_discordConfig.ApplicationChannelId);
            if (applicationChannel == null)
            {
                throw new Exception($"Application channel {_discordConfig.ApplicationChannelId} not found!");
            }

            var messages = await applicationChannel.GetMessagesAsync().FirstAsync();
            if (!messages.Any(x => x.Author.Id == _client.CurrentUser.Id && x.Components.Count > 0))
            {
                _logger.LogInformation("Verify message wasn't found, sending one...");

                var embed = new EmbedBuilder()
                    .WithTitle("Verification")
                    .WithDescription(_discordConfig.VerifyMessage)
                    .WithColor(new Color(183, 15, 117))
                    .Build();

                var components = new ComponentBuilder()
                    .WithButton("Verify", "verify", ButtonStyle.Success)
                    .WithButton("Verify and apply for Onion", "verify-apply-onion")
                    .Build();

                await applicationChannel.SendMessageAsync(embed: embed, components: components, flags: MessageFlags.SuppressNotification);
            }
        }

        private async Task InteractionCreated(SocketInteraction interaction)
        {
            var guild = _client.GetGuild(_discordConfig.GuildId);
            if (guild == null)
            {
                throw new Exception($"Guild {_discordConfig.GuildId} not found!");
            }

            var discordUser = guild.GetUser(interaction.User.Id);

            if (interaction.Type == InteractionType.MessageComponent && interaction is SocketMessageComponent componentInteraction)
            {
                _logger.LogInformation("Processing {Id} MessageComponent interaction...", componentInteraction.Data.CustomId);

                switch (componentInteraction.Data.CustomId)
                {
                    case "verify-apply-onion":
                    {
                        if (discordUser.Roles.Any(x => x.Id == _discordConfig.Roles.Onion))
                        {
                            await interaction.RespondAsync("You are already onion! If you think you need to reapply anyway - ping any of the @mod's", ephemeral: true);
                            return;
                        }

                        await SendOnionModal(interaction);
                        return;
                    }
                    case "verify":
                    {
                        if (discordUser.Roles.Any(x => x.Id == _discordConfig.Roles.Verified))
                        {
                            await interaction.RespondAsync("You are already verified!", ephemeral: true);
                            return;
                        }
                        
                        await SendVerifyMessage(interaction, _verificationService.Start(interaction.User.Id, false));
                        return;
                    }
                    case { } id when id.StartsWith("add-onion-"):
                    {
                        await interaction.DeferAsync();

                        var split = id["add-onion-".Length..].Split('-');
                        await _verificationService.ApplyOnion(int.Parse(split[0]), ulong.Parse(split[1]));

                        var components = new ComponentBuilder()
                            .WithButton("Remove onion", $"remove-onion-{split[0]}-{split[1]}", ButtonStyle.Danger)
                            .Build();

                        var embed = new EmbedBuilder()
                            .WithAuthor(interaction.User)
                            .WithDescription("Added onion")
                            .WithColor(Color.Green)
                            .WithTimestamp(DateTimeOffset.UtcNow)
                            .Build();

                        await interaction.ModifyOriginalResponseAsync(properties =>
                        {
                            properties.Components = components;
                            properties.Embeds = componentInteraction.Message.Embeds.Append(embed).ToArray();
                        });
                        return;
                    }
                    case { } id when id.StartsWith("remove-onion-"):
                    {
                        await interaction.DeferAsync();

                        var split = id["remove-onion-".Length..].Split('-');
                        await _verificationService.RemoveOnion(ulong.Parse(split[1]));

                        var components = new ComponentBuilder()
                            .WithButton("Add onion", $"add-onion-{split[0]}-{split[1]}", ButtonStyle.Success)
                            .Build();

                        var embed = new EmbedBuilder()
                            .WithAuthor(interaction.User)
                            .WithDescription("Removed onion")
                            .WithColor(Color.Red)
                            .WithTimestamp(DateTimeOffset.UtcNow)
                            .Build();

                        await interaction.ModifyOriginalResponseAsync(properties =>
                        {
                            properties.Components = components;
                            properties.Embeds = componentInteraction.Message.Embeds.Append(embed).ToArray();
                        });
                        return;
                    }
                    default:
                        _logger.LogInformation("Unknown MessageComponent interaction {Id}!", componentInteraction.Data.CustomId);
                        return;
                }
            }

            if (interaction.Type == InteractionType.ModalSubmit && interaction is SocketModal modalInteraction)
            {
                _logger.LogInformation("Processing {Id} Modal interaction...", modalInteraction.Data.CustomId);

                if (modalInteraction.Data.CustomId == "onion-application-modal")
                {
                    var text = modalInteraction.Data.Components.FirstOrDefault(x => x.CustomId == "onion-application-modal-text");
                    if (text == null)
                    {
                        _logger.LogError("Onion application doesn't have text!");
                        return;
                    }

                    await SendVerifyMessage(interaction, _verificationService.Start(interaction.User.Id, true, text.Value));
                    return;
                }

                _logger.LogInformation("Unknown Modal interaction {Id}!", modalInteraction.Data.CustomId);
                return;
            }

            _logger.LogInformation("Unknown interaction {InteractionType}!", interaction.Type);
        }

        private async Task SendVerifyMessage(SocketInteraction interaction, Guid verificationId)
        {
            await interaction.DeferAsync(true);

            var url = "https://pp-verification.stanr.info";
#if DEBUG
            url = "http://localhost:3001";
#endif

            var embed = new EmbedBuilder()
                .WithTitle("Click here to verify your osu! account!")
                .WithUrl($"{url}/start/{verificationId}")
                .WithColor(Color.Blue)
                .Build();

            await interaction.FollowupAsync(embed: embed, ephemeral: true);
        }

        private async Task SendOnionModal(SocketInteraction interaction)
        {
            var modalText = new TextInputBuilder()
                .WithCustomId("onion-application-modal-text")
                .WithLabel("What you'd be most interested in")
                .WithPlaceholder("speed / jump aim / tech / idk")
                .WithRequired(true)
                .WithStyle(TextInputStyle.Paragraph);

            var modal = new ModalBuilder()
                .WithCustomId("onion-application-modal")
                .WithTitle("Onion application")
                .AddTextInput(modalText)
                .Build();

            await interaction.RespondWithModalAsync(modal);
        }

        private Task Log(LogMessage message)
        {
            var severity = message.Severity switch
            {
                LogSeverity.Critical => LogLevel.Critical,
                LogSeverity.Error => LogLevel.Error,
                LogSeverity.Warning => LogLevel.Warning,
                LogSeverity.Info => LogLevel.Information,
                LogSeverity.Verbose => LogLevel.Debug,
                LogSeverity.Debug => LogLevel.Trace,
                _ => throw new ArgumentException()
            };

            _logger.Log(severity, message.Exception, "[{Source}] {Message}", message.Source, message.Message);

            return Task.CompletedTask;
        }
    }
}
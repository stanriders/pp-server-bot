using Discord.WebSocket;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using PpServerBot;
using Serilog;
using Serilog.Settings.Configuration;
using SerilogTracing;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration, new ConfigurationReaderOptions() { SectionName = "Logging" })
    .ReadFrom.Services(services));

using var tracer = new ActivityListenerConfiguration()
    .Instrument.AspNetCoreRequests()
    .TraceToSharedLogger();

var osuConfig = builder.Configuration.GetSection("osuApi");

builder.Services.AddAuthentication("ExternalCookies")
    .AddCookie("ExternalCookies")
    .AddOAuth("osu", options =>
    {
        options.SignInScheme = "ExternalCookies";

        options.TokenEndpoint = "https://osu.ppy.sh/oauth/token";
        options.AuthorizationEndpoint = "https://osu.ppy.sh/oauth/authorize";
        options.ClientId = osuConfig["ClientID"]!;
        options.ClientSecret = osuConfig["ClientSecret"]!;
        options.CallbackPath = osuConfig["CallbackUrl"];
        options.Scope.Add("public");
        
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;

        options.SaveTokens = true;

        options.Validate();
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<OsuApiProvider>();

builder.Services.AddHttpClient(nameof(HuisApiProvider), client =>
{
    client.BaseAddress = new Uri("https://api.pp.huismetbenen.nl");
    client.DefaultRequestHeaders.Add("x-discord-bot-auth-key", builder.Configuration["HuisToken"]);
    client.Timeout = TimeSpan.FromSeconds(3); // this is set to the same timeout discord requires
});
builder.Services.AddSingleton<HuisApiProvider>();

builder.Services.AddSingleton<VerificationService>();

builder.Services.AddOptions<DiscordConfig>()
    .BindConfiguration(nameof(DiscordConfig))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHostedService<DiscordService>();

var config = new DiscordSocketConfig();
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<DiscordSocketClient>();

builder.Services.AddRazorPages();

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.All });
app.UseSerilogRequestLogging();

if (app.Environment.IsStaging() || app.Environment.IsProduction())
{
    app.UseExceptionHandler("/Error");

    app.Use((context, next) =>
    {
        context.Request.Scheme = "https";

        return next(context);
    });

    app.UseCookiePolicy(new CookiePolicyOptions
    {
        Secure = CookieSecurePolicy.SameAsRequest,
        MinimumSameSitePolicy = SameSiteMode.Lax
    });
}

app.UseRouting();
app.UseAuthorization();
app.UseAuthentication();

app.MapRazorPages();
app.MapGet("/", () => Results.Redirect("https://www.youtube.com/watch?v=CLMCkDZpSLQ"));
app.MapGet("/start/{id:guid}", (Guid id) => Results.Challenge(new AuthenticationProperties { RedirectUri = $"/complete?id={id}" }, new[] { "osu" }));
app.MapGet("/robots.txt", () => Results.Ok("User-agent: *\r\nDisallow: /"));

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

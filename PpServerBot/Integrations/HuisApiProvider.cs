namespace PpServerBot.Integrations
{
    public class HuisApiProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public HuisApiProvider(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task AddOnion(int userId, ulong discordId)
        {
            var client = _httpClientFactory.CreateClient(nameof(HuisApiProvider));

            var response = await client.PostAsJsonAsync("/oauth/add-onion",
                new
                {
                    osu_id = userId,
                    discord_id = discordId
                });

            response.EnsureSuccessStatusCode();

            // TODO: retry queue?
        }

        public async Task RemoveOnion(ulong discordId)
        {
            var client = _httpClientFactory.CreateClient(nameof(HuisApiProvider));

            var response = await client.DeleteAsync($"/oauth/remove-onion/{discordId}");

            response.EnsureSuccessStatusCode();

            // TODO: retry queue?
        }
    }
}

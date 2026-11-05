using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MediaRequests.Data;

namespace Jellyfin.Plugin.MediaRequests.Services
{
    /// <summary>Recherche films + séries sur TMDB (la clé reste côté serveur).</summary>
    public class TmdbClient
    {
        private const string BaseUrl = "https://api.themoviedb.org/3";
        private readonly IHttpClientFactory _httpClientFactory;

        public TmdbClient(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        private static Configuration.PluginConfiguration Config => Plugin.Instance!.Configuration;

        public static bool IsConfigured => !string.IsNullOrWhiteSpace(Config.TmdbApiKey);

        public async Task<List<SearchResultDto>> SearchAsync(string query, CancellationToken ct)
        {
            var key = Config.TmdbApiKey.Trim();
            var lang = string.IsNullOrWhiteSpace(Config.Language) ? "fr-FR" : Config.Language.Trim();
            var url = $"{BaseUrl}/search/multi?query={Uri.EscapeDataString(query)}&language={Uri.EscapeDataString(lang)}&include_adult=false&page=1";

            // Jeton v4 (JWT) => header Bearer ; clé v3 => paramètre api_key
            var isBearer = key.StartsWith("eyJ", StringComparison.Ordinal);
            if (!isBearer)
            {
                url += $"&api_key={Uri.EscapeDataString(key)}";
            }

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (isBearer)
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            }

            using var client = _httpClientFactory.CreateClient();
            using var res = await client.SendAsync(req, ct).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();

            await using var stream = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

            var results = new List<SearchResultDto>();
            if (!doc.RootElement.TryGetProperty("results", out var arr))
            {
                return results;
            }

            foreach (var item in arr.EnumerateArray())
            {
                var type = Str(item, "media_type");
                if (type is not ("movie" or "tv"))
                {
                    continue; // on ignore les personnes
                }

                var title = type == "movie" ? Str(item, "title") : Str(item, "name");
                var date = type == "movie" ? Str(item, "release_date") : Str(item, "first_air_date");

                results.Add(new SearchResultDto
                {
                    MediaType = type,
                    TmdbId = item.GetProperty("id").GetInt32(),
                    Title = title ?? "?",
                    Year = date is { Length: >= 4 } ? date[..4] : null,
                    PosterPath = Str(item, "poster_path"),
                    Overview = Str(item, "overview")
                });
            }

            return results;
        }

        private static string? Str(JsonElement el, string name) =>
            el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}

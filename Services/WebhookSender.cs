using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MediaRequests.Data;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaRequests.Services
{
    /// <summary>
    /// Envoie les événements des demandes à un service externe (ex. le pont WhatsApp / Jellyseerr).
    /// Corps JSON signé en HMAC-SHA256 avec le secret partagé (en-tête X-MediaRequests-Signature: sha256=...).
    /// </summary>
    public class WebhookSender
    {
        public const string Created = "request.created";
        public const string Approved = "request.approved";
        public const string Denied = "request.denied";
        public const string Test = "test";

        private const string TmdbImageBase = "https://image.tmdb.org/t/p/w342";
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WebhookSender> _logger;

        public WebhookSender(IHttpClientFactory httpClientFactory, ILogger<WebhookSender> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        private static Configuration.PluginConfiguration Config => Plugin.Instance!.Configuration;

        public static bool IsConfigured => !string.IsNullOrWhiteSpace(Config.WebhookUrl);

        /// <summary>Envoie l'événement en arrière-plan ; les erreurs sont seulement journalisées.</summary>
        public void Fire(string eventName, MediaRequest request)
        {
            if (!IsConfigured)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await SendAsync(eventName, request, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[MediaRequests] Webhook {Event} non envoyé", eventName);
                }
            });
        }

        /// <summary>Envoie l'événement et lève une exception si le service répond une erreur.</summary>
        public async Task SendAsync(string eventName, MediaRequest? request, CancellationToken ct)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("L'URL du webhook n'est pas configurée.");
            }

            var payload = new Dictionary<string, object?>
            {
                ["event"] = eventName,
                ["sentAt"] = DateTime.UtcNow,
                ["adminUrl"] = Notifier.AdminPageUrl,
                ["request"] = request is null ? null : new
                {
                    id = request.Id,
                    mediaType = request.MediaType,
                    tmdbId = request.TmdbId,
                    title = request.Title,
                    year = request.Year,
                    note = request.Note,
                    userName = request.UserName,
                    status = request.Status.ToString(),
                    createdAt = request.CreatedAt,
                    posterUrl = request.PosterPath is null ? null : TmdbImageBase + request.PosterPath
                }
            };

            var body = JsonSerializer.Serialize(payload, JsonOptions);
            using var req = new HttpRequestMessage(HttpMethod.Post, Config.WebhookUrl.Trim())
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("X-MediaRequests-Event", eventName);
            if (!string.IsNullOrWhiteSpace(Config.WebhookSecret))
            {
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Config.WebhookSecret.Trim()));
                var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
                req.Headers.Add("X-MediaRequests-Signature", "sha256=" + signature);
            }

            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            using var res = await client.SendAsync(req, ct).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Le webhook a répondu {(int)res.StatusCode}.");
            }
        }
    }
}

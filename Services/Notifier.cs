using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MediaRequests.Data;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaRequests.Services
{
    /// <summary>
    /// Prévient l'admin d'une nouvelle demande :
    /// popup dans Jellyfin sur les sessions admin ouvertes + ntfy (optionnel).
    /// </summary>
    public class Notifier
    {
        private const string TmdbImageBase = "https://image.tmdb.org/t/p/w342";
        private readonly ISessionManager _sessionManager;
        private readonly IUserManager _userManager;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<Notifier> _logger;

        public Notifier(
            ISessionManager sessionManager,
            IUserManager userManager,
            IHttpClientFactory httpClientFactory,
            ILogger<Notifier> logger)
        {
            _sessionManager = sessionManager;
            _userManager = userManager;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        private static Configuration.PluginConfiguration Config => Plugin.Instance!.Configuration;

        private static string? PublicUrl =>
            string.IsNullOrWhiteSpace(Config.PublicServerUrl) ? null : Config.PublicServerUrl.TrimEnd('/');

        public async Task NotifyNewRequestAsync(MediaRequest r)
        {
            var kind = r.MediaType == "tv" ? "Série" : "Film";
            var title = $"{r.Title}{(r.Year is null ? string.Empty : $" ({r.Year})")}";
            var text = $"{kind} demandé par {r.UserName}" + (string.IsNullOrWhiteSpace(r.Note) ? string.Empty : $" : « {r.Note} »");

            if (Config.NotifyAdminSessions)
            {
                await Safe(() => NotifyAdminSessionsAsync($"Nouvelle demande : {title}", text), "Jellyfin").ConfigureAwait(false);
            }

            await Safe(() => SendNtfyAsync(r, $"🎬 {title}", text), "ntfy").ConfigureAwait(false);
        }

        /// <summary>Affiche un message dans les clients Jellyfin des admins actuellement connectés.</summary>
        private async Task NotifyAdminSessionsAsync(string header, string text)
        {
            var command = new MessageCommand { Header = header, Text = text, TimeoutMs = 15000 };
            var adminCache = new Dictionary<Guid, bool>();

            foreach (var session in _sessionManager.Sessions.ToList())
            {
                if (session.UserId == Guid.Empty)
                {
                    continue;
                }

                if (!adminCache.TryGetValue(session.UserId, out var isAdmin))
                {
                    var user = _userManager.GetUserById(session.UserId);
                    isAdmin = user is not null && _userManager.GetUserDto(user).Policy?.IsAdministrator == true;
                    adminCache[session.UserId] = isAdmin;
                }

                if (!isAdmin)
                {
                    continue;
                }

                try
                {
                    await _sessionManager.SendMessageCommand(session.Id, session.Id, command, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Certains clients (TV, DLNA...) ne supportent pas les messages.
                    _logger.LogDebug(ex, "[MediaRequests] Message non affiché sur la session {Session}", session.Id);
                }
            }
        }

        private async Task SendNtfyAsync(MediaRequest r, string title, string message)
        {
            if (string.IsNullOrWhiteSpace(Config.NtfyTopic))
            {
                return;
            }

            var actions = new List<object>();
            if (PublicUrl is not null)
            {
                string ActionUrl(string decision) =>
                    $"{PublicUrl}/MediaRequests/Action/{r.Id}?decision={decision}&token={r.ActionToken}";

                actions.Add(new { action = "http", label = "✅ Accepter", url = ActionUrl("approve"), method = "POST", clear = true });
                actions.Add(new { action = "http", label = "❌ Refuser", url = ActionUrl("deny"), method = "POST", clear = true });
            }

            var payload = new Dictionary<string, object?>
            {
                ["topic"] = Config.NtfyTopic.Trim(),
                ["title"] = title,
                ["message"] = message,
                ["tags"] = new[] { "movie_camera" },
                ["actions"] = actions
            };
            if (r.PosterPath is not null)
            {
                payload["attach"] = TmdbImageBase + r.PosterPath;
            }

            if (PublicUrl is not null)
            {
                payload["click"] = $"{PublicUrl}/web/#/configurationpage?name=MediaRequests";
            }

            var server = string.IsNullOrWhiteSpace(Config.NtfyServer) ? "https://ntfy.sh" : Config.NtfyServer.TrimEnd('/');
            using var req = new HttpRequestMessage(HttpMethod.Post, server) { Content = JsonContent.Create(payload) };
            if (!string.IsNullOrWhiteSpace(Config.NtfyToken))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Config.NtfyToken.Trim());
            }

            using var client = _httpClientFactory.CreateClient();
            using var res = await client.SendAsync(req).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();
        }

        private async Task Safe(Func<Task> action, string channel)
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[MediaRequests] Échec de la notification {Channel}", channel);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MediaRequests.Data;
using Jellyfin.Plugin.MediaRequests.Services;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaRequests.Api
{
    [ApiController]
    [Route("MediaRequests")]
    public class MediaRequestsController : ControllerBase
    {
        private readonly RequestStore _store;
        private readonly RequestService _requests;
        private readonly TmdbClient _tmdb;
        private readonly LibraryIndex _library;
        private readonly ILogger<MediaRequestsController> _logger;

        public MediaRequestsController(
            RequestStore store,
            RequestService requests,
            TmdbClient tmdb,
            LibraryIndex library,
            ILogger<MediaRequestsController> logger)
        {
            _store = store;
            _requests = requests;
            _tmdb = tmdb;
            _library = library;
            _logger = logger;
        }

        // ---------- Page publique ----------

        /// <summary>La page de recherche/demande. Accessible sans être connecté (la page a son propre login Jellyfin).</summary>
        [HttpGet("Page")]
        [AllowAnonymous]
        [Produces(MediaTypeNames.Text.Html)]
        public ActionResult GetPage()
        {
            var stream = GetType().Assembly.GetManifestResourceStream("Jellyfin.Plugin.MediaRequests.Configuration.request.html");
            if (stream is null)
            {
                return NotFound();
            }

            return File(stream, "text/html; charset=utf-8");
        }

        // ---------- Utilisateurs connectés ----------

        [HttpGet("Search")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<SearchResultDto>>> Search([FromQuery] string query, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            {
                return Ok(Array.Empty<SearchResultDto>());
            }

            if (!TmdbClient.IsConfigured)
            {
                return Problem("La clé TMDB n'est pas configurée dans le plugin.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            List<SearchResultDto> results;
            try
            {
                results = await _tmdb.SearchAsync(query.Trim(), ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "[MediaRequests] Recherche TMDB échouée");
                return Problem("TMDB ne répond pas ou la clé est invalide.", statusCode: StatusCodes.Status502BadGateway);
            }

            var requests = await _store.GetAllAsync().ConfigureAwait(false);
            var byKey = requests
                .Where(r => r.Status != RequestStatus.Denied)
                .GroupBy(r => r.Key)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.CreatedAt).First());

            foreach (var r in results)
            {
                r.JellyfinId = _library.Find(r.MediaType, r.TmdbId);
                if (r.JellyfinId is null && byKey.TryGetValue($"{r.MediaType}:{r.TmdbId}", out var existing))
                {
                    r.RequestStatus = existing.Status.ToString();
                }
            }

            return Ok(results);
        }

        [HttpGet("Mine")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<MediaRequestDto>>> Mine()
        {
            var userId = CurrentUserId();
            var all = await _store.GetAllAsync().ConfigureAwait(false);
            return Ok(all.Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(_requests.ToDto));
        }

        [HttpPost("Request")]
        [Authorize]
        public async Task<ActionResult<MediaRequestDto>> Create([FromBody] CreateRequestBody body)
        {
            var (request, error) = await _requests.CreateAsync(body, CurrentUserId(), CurrentUserName()).ConfigureAwait(false);
            if (error is not null)
            {
                return BadRequest(new { message = error });
            }

            return Ok(_requests.ToDto(request!));
        }

        [HttpDelete("Mine/{id}")]
        [Authorize]
        public async Task<ActionResult> CancelMine([FromRoute] Guid id)
        {
            var userId = CurrentUserId();
            var removed = await _store.UpdateAsync(list =>
                list.RemoveAll(r => r.Id == id && r.UserId == userId && r.Status == RequestStatus.Pending) > 0)
                .ConfigureAwait(false);
            if (!removed)
            {
                return NotFound();
            }

            return NoContent();
        }

        // ---------- Admin ----------

        [HttpGet("All")]
        [Authorize(Policy = Policies.RequiresElevation)]
        public async Task<ActionResult<IEnumerable<MediaRequestDto>>> All()
        {
            var all = await _store.GetAllAsync().ConfigureAwait(false);
            return Ok(all.OrderBy(r => r.Status == RequestStatus.Pending ? 0 : 1)
                .ThenByDescending(r => r.CreatedAt)
                .Select(_requests.ToDto));
        }

        [HttpPost("{id}/Approve")]
        [Authorize(Policy = Policies.RequiresElevation)]
        public Task<ActionResult<MediaRequestDto>> Approve([FromRoute] Guid id) => SetStatus(id, RequestStatus.Approved);

        [HttpPost("{id}/Deny")]
        [Authorize(Policy = Policies.RequiresElevation)]
        public Task<ActionResult<MediaRequestDto>> Deny([FromRoute] Guid id) => SetStatus(id, RequestStatus.Denied);

        [HttpDelete("{id}")]
        [Authorize(Policy = Policies.RequiresElevation)]
        public async Task<ActionResult> Delete([FromRoute] Guid id)
        {
            if (!await _requests.DeleteAsync(id).ConfigureAwait(false))
            {
                return NotFound();
            }

            return NoContent();
        }

        /// <summary>Boutons Accepter/Refuser de la notif ntfy (protégés par un token secret propre à chaque demande).</summary>
        [HttpPost("Action/{id}")]
        [AllowAnonymous]
        public async Task<ActionResult> NtfyAction([FromRoute] Guid id, [FromQuery] string decision, [FromQuery] string token)
        {
            var request = (await _store.GetAllAsync().ConfigureAwait(false)).FirstOrDefault(r => r.Id == id);
            if (request is null || string.IsNullOrEmpty(token)
                || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(request.ActionToken)))
            {
                return NotFound();
            }

            var status = decision == "approve" ? RequestStatus.Approved : RequestStatus.Denied;
            await _requests.SetStatusAsync(id, status).ConfigureAwait(false);
            return Ok(new { status = status.ToString() });
        }

        // ---------- Helpers ----------

        private async Task<ActionResult<MediaRequestDto>> SetStatus(Guid id, RequestStatus status)
        {
            var r = await _requests.SetStatusAsync(id, status).ConfigureAwait(false);
            if (r is null)
            {
                return NotFound();
            }

            return Ok(_requests.ToDto(r));
        }

        private Guid CurrentUserId()
        {
            var value = User.FindFirst("Jellyfin-UserId")?.Value;
            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }

        private string CurrentUserName() => User.FindFirst(ClaimTypes.Name)?.Value ?? "Inconnu";
    }
}

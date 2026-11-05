using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.MediaRequests.Data;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaRequests.Services
{
    public class RequestService
    {
        private readonly RequestStore _store;
        private readonly LibraryIndex _library;
        private readonly Notifier _notifier;
        private readonly ILogger<RequestService> _logger;

        public RequestService(RequestStore store, LibraryIndex library, Notifier notifier, ILogger<RequestService> logger)
        {
            _store = store;
            _library = library;
            _notifier = notifier;
            _logger = logger;
        }

        public MediaRequestDto ToDto(MediaRequest r)
        {
            var jellyfinId = _library.Find(r.MediaType, r.TmdbId);
            return new MediaRequestDto
            {
                Id = r.Id,
                MediaType = r.MediaType,
                TmdbId = r.TmdbId,
                Title = r.Title,
                Year = r.Year,
                PosterPath = r.PosterPath,
                Note = r.Note,
                UserName = r.UserName,
                CreatedAt = r.CreatedAt,
                // Dès que le titre arrive dans Jellyfin, la demande passe automatiquement en "Available".
                Status = jellyfinId is not null && r.Status != RequestStatus.Denied ? "Available" : r.Status.ToString(),
                DecidedAt = r.DecidedAt,
                JellyfinId = jellyfinId
            };
        }

        public async Task<(MediaRequest? Request, string? Error)> CreateAsync(CreateRequestBody body, Guid userId, string userName)
        {
            var mediaType = body.MediaType == "tv" ? "tv" : "movie";
            if (body.TmdbId <= 0 || string.IsNullOrWhiteSpace(body.Title))
            {
                return (null, "Titre invalide.");
            }

            if (_library.Find(mediaType, body.TmdbId) is not null)
            {
                return (null, "Ce titre est déjà disponible sur Jellyfin.");
            }

            var max = Math.Max(1, Plugin.Instance!.Configuration.MaxPendingPerUser);
            var note = body.Note?.Trim();
            if (note is { Length: > 300 })
            {
                note = note[..300];
            }

            var (request, error, isNew) = await _store.UpdateAsync<(MediaRequest? Request, string? Error, bool IsNew)>(list =>
            {
                var existing = list.FirstOrDefault(r => r.MediaType == mediaType && r.TmdbId == body.TmdbId
                    && r.Status is RequestStatus.Pending or RequestStatus.Approved);
                if (existing is not null)
                {
                    return (existing, "Ce titre a déjà été demandé.", false);
                }

                if (list.Count(r => r.UserId == userId && r.Status == RequestStatus.Pending) >= max)
                {
                    return (null, $"Tu as déjà {max} demandes en attente.", false);
                }

                var created = new MediaRequest
                {
                    MediaType = mediaType,
                    TmdbId = body.TmdbId,
                    Title = body.Title.Trim(),
                    Year = body.Year,
                    PosterPath = body.PosterPath,
                    Note = string.IsNullOrWhiteSpace(note) ? null : note,
                    UserId = userId,
                    UserName = userName
                };
                list.Add(created);
                return (created, null, true);
            }).ConfigureAwait(false);

            if (isNew && request is not null)
            {
                _logger.LogInformation("[MediaRequests] {User} a demandé {Title} ({Type} {TmdbId})",
                    userName, request.Title, request.MediaType, request.TmdbId);
                await _notifier.NotifyNewRequestAsync(request).ConfigureAwait(false);
            }

            return (request, error);
        }

        public Task<MediaRequest?> SetStatusAsync(Guid id, RequestStatus status) =>
            _store.UpdateAsync(list =>
            {
                var r = list.FirstOrDefault(x => x.Id == id);
                if (r is not null && r.Status != status)
                {
                    r.Status = status;
                    r.DecidedAt = DateTime.UtcNow;
                }

                return r;
            });

        public Task<bool> DeleteAsync(Guid id) =>
            _store.UpdateAsync(list => list.RemoveAll(r => r.Id == id) > 0);
    }
}

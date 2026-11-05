using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MediaRequests.Data
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RequestStatus
    {
        Pending,
        Approved,
        Denied
    }

    /// <summary>Demande stockée sur disque.</summary>
    public class MediaRequest
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>"movie" ou "tv".</summary>
        public string MediaType { get; set; } = "movie";

        public int TmdbId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Year { get; set; }

        public string? PosterPath { get; set; }

        public string? Note { get; set; }

        public Guid UserId { get; set; }

        public string UserName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public RequestStatus Status { get; set; } = RequestStatus.Pending;

        public DateTime? DecidedAt { get; set; }

        /// <summary>Secret pour les boutons Accepter/Refuser de la notif ntfy.</summary>
        public string ActionToken { get; set; } =
            Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));

        public string Key => $"{MediaType}:{TmdbId}";
    }

    /// <summary>Demande renvoyée par l'API (sans le token).</summary>
    public class MediaRequestDto
    {
        [JsonPropertyName("id")] public Guid Id { get; set; }
        [JsonPropertyName("mediaType")] public string MediaType { get; set; } = "movie";
        [JsonPropertyName("tmdbId")] public int TmdbId { get; set; }
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("year")] public string? Year { get; set; }
        [JsonPropertyName("posterPath")] public string? PosterPath { get; set; }
        [JsonPropertyName("note")] public string? Note { get; set; }
        [JsonPropertyName("userName")] public string UserName { get; set; } = string.Empty;
        [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; set; }
        /// <summary>Pending / Approved / Denied / Available</summary>
        [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
        [JsonPropertyName("decidedAt")] public DateTime? DecidedAt { get; set; }
        /// <summary>Id de l'élément Jellyfin si déjà dans la bibliothèque.</summary>
        [JsonPropertyName("jellyfinId")] public string? JellyfinId { get; set; }
    }

    public class SearchResultDto
    {
        [JsonPropertyName("mediaType")] public string MediaType { get; set; } = "movie";
        [JsonPropertyName("tmdbId")] public int TmdbId { get; set; }
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("year")] public string? Year { get; set; }
        [JsonPropertyName("posterPath")] public string? PosterPath { get; set; }
        [JsonPropertyName("overview")] public string? Overview { get; set; }
        /// <summary>Id Jellyfin si le titre est déjà dans la bibliothèque.</summary>
        [JsonPropertyName("jellyfinId")] public string? JellyfinId { get; set; }
        /// <summary>Statut d'une demande existante (null si aucune).</summary>
        [JsonPropertyName("requestStatus")] public string? RequestStatus { get; set; }
    }

    public class CreateRequestBody
    {
        public string MediaType { get; set; } = "movie";
        public int TmdbId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Year { get; set; }
        public string? PosterPath { get; set; }
        public string? Note { get; set; }
    }
}

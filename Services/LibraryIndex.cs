using System;
using System.Collections.Generic;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.MediaRequests.Services
{
    /// <summary>
    /// Index "movie:TMDBID" / "tv:TMDBID" -> Id Jellyfin, pour savoir si un titre est déjà dans la bibliothèque.
    /// Reconstruit au maximum toutes les 2 minutes.
    /// </summary>
    public class LibraryIndex
    {
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);
        private readonly ILibraryManager _libraryManager;
        private readonly object _sync = new();
        private Dictionary<string, string> _index = new();
        private DateTime _builtAt = DateTime.MinValue;

        public LibraryIndex(ILibraryManager libraryManager)
        {
            _libraryManager = libraryManager;
        }

        public string? Find(string mediaType, int tmdbId)
        {
            var index = GetIndex();
            return index.TryGetValue($"{mediaType}:{tmdbId}", out var id) ? id : null;
        }

        private Dictionary<string, string> GetIndex()
        {
            lock (_sync)
            {
                if (DateTime.UtcNow - _builtAt < Ttl)
                {
                    return _index;
                }

                var items = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Series },
                    Recursive = true,
                    IsVirtualItem = false
                });

                var index = new Dictionary<string, string>();
                foreach (var item in items)
                {
                    var tmdb = item.GetProviderId(MetadataProvider.Tmdb);
                    if (string.IsNullOrEmpty(tmdb))
                    {
                        continue;
                    }

                    var type = item is MediaBrowser.Controller.Entities.TV.Series ? "tv" : "movie";
                    index[$"{type}:{tmdb}"] = item.Id.ToString("N");
                }

                _index = index;
                _builtAt = DateTime.UtcNow;
                return _index;
            }
        }
    }
}

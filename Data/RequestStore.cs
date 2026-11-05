using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.MediaRequests.Data
{
    /// <summary>Stockage des demandes dans un fichier JSON du dossier data du plugin.</summary>
    public class RequestStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
        private readonly SemaphoreSlim _lock = new(1, 1);
        private List<MediaRequest>? _cache;

        private static string FilePath
        {
            get
            {
                var dir = Plugin.Instance?.DataFolderPath
                    ?? throw new InvalidOperationException("Plugin non initialisé");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "requests.json");
            }
        }

        private async Task<List<MediaRequest>> LoadAsync()
        {
            if (_cache is not null)
            {
                return _cache;
            }

            if (!File.Exists(FilePath))
            {
                _cache = new List<MediaRequest>();
                return _cache;
            }

            await using var stream = File.OpenRead(FilePath);
            _cache = await JsonSerializer.DeserializeAsync<List<MediaRequest>>(stream, JsonOptions).ConfigureAwait(false)
                     ?? new List<MediaRequest>();
            return _cache;
        }

        private static async Task SaveAsync(List<MediaRequest> list)
        {
            var tmp = FilePath + ".tmp";
            await using (var stream = File.Create(tmp))
            {
                await JsonSerializer.SerializeAsync(stream, list, JsonOptions).ConfigureAwait(false);
            }

            File.Move(tmp, FilePath, overwrite: true);
        }

        public async Task<List<MediaRequest>> GetAllAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                return (await LoadAsync().ConfigureAwait(false)).ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>Modification atomique de la liste, puis sauvegarde.</summary>
        public async Task<T> UpdateAsync<T>(Func<List<MediaRequest>, T> mutate)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var list = await LoadAsync().ConfigureAwait(false);
                var result = mutate(list);
                await SaveAsync(list).ConfigureAwait(false);
                return result;
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}

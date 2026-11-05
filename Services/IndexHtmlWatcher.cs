using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaRequests.Services
{
    /// <summary>
    /// Keeps the requests.js script tag in index.html.
    /// Other plugins (e.g. MediaBarEnhanced) rewrite index.html during startup from a copy read earlier,
    /// which can drop our tag. So we check again once the server has started, then whenever the file changes.
    /// </summary>
    public sealed class IndexHtmlWatcher : IHostedService, IDisposable
    {
        private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(2);

        private readonly IHostApplicationLifetime _lifetime;
        private readonly IApplicationPaths _applicationPaths;
        private readonly ILogger<IndexHtmlWatcher> _logger;
        private readonly Timer _timer;
        private FileSystemWatcher? _watcher;
        private CancellationTokenRegistration _startedRegistration;

        public IndexHtmlWatcher(IHostApplicationLifetime lifetime, IApplicationPaths applicationPaths, ILogger<IndexHtmlWatcher> logger)
        {
            _lifetime = lifetime;
            _applicationPaths = applicationPaths;
            _logger = logger;
            _timer = new Timer(_ => Plugin.Instance?.EnsureScript(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _startedRegistration = _lifetime.ApplicationStarted.Register(OnStarted);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (_watcher is not null)
            {
                _watcher.EnableRaisingEvents = false;
            }

            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _startedRegistration.Dispose();
            _watcher?.Dispose();
            _timer.Dispose();
        }

        private void OnStarted()
        {
            Plugin.Instance?.EnsureScript();

            var webPath = _applicationPaths.WebPath;
            if (string.IsNullOrWhiteSpace(webPath) || !Directory.Exists(webPath))
            {
                return;
            }

            try
            {
                _watcher = new FileSystemWatcher(webPath, "index.html")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
                };
                _watcher.Changed += OnIndexChanged;
                _watcher.Created += OnIndexChanged;
                _watcher.Renamed += OnIndexChanged;
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or PlatformNotSupportedException)
            {
                _logger.LogWarning(ex, "Could not watch {WebPath}/index.html", webPath);
            }
        }

        // Wait for the writer to finish before checking (several events fire for a single write).
        private void OnIndexChanged(object sender, FileSystemEventArgs e) => _timer.Change(Debounce, Timeout.InfiniteTimeSpan);
    }
}

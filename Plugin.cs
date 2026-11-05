using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.MediaRequests.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaRequests
{
    /// <summary>
    /// "Media Requests" : page de recherche TMDB (films + séries) pour les utilisateurs.
    /// Si un titre n'est pas dans Jellyfin, ils peuvent le demander à l'admin, qui est notifié.
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        // Relative src so it resolves against /web/ and works behind a configured base URL.
        private const string ScriptElement = "<script plugin=\"MediaRequests\" src=\"ConfigurationPage?name=requests.js\" defer></script>";
        private static readonly Regex _scriptRegex = new("<script plugin=\"MediaRequests\".*?</script>", RegexOptions.Singleline | RegexOptions.Compiled);

        private readonly IApplicationPaths _applicationPaths;
        private readonly ILogger<Plugin> _logger;

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogger<Plugin> logger)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
            _applicationPaths = applicationPaths;
            _logger = logger;

            UpdateIndexHtml(inject: true);
        }

        public static Plugin? Instance { get; private set; }

        public override string Name => "Media Requests";

        public override Guid Id => Guid.Parse("3c9a7e52-6f1d-4b8e-9a2f-5d7c1e8b4a60");

        public override string Description => "Les utilisateurs cherchent un film ou une série et en font la demande à l'admin.";

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "MediaRequests",
                    DisplayName = "Demandes",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html",
                    EnableInMainMenu = true,
                    MenuSection = "server",
                    MenuIcon = "movie_filter"
                },
                new PluginPageInfo
                {
                    Name = "requests.js",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.requests.js"
                }
            };
        }

        public override void OnUninstalling()
        {
            UpdateIndexHtml(inject: false);
            base.OnUninstalling();
        }

        /// <summary>
        /// Adds (or removes) the requests.js script tag in jellyfin-web's index.html.
        /// </summary>
        private void UpdateIndexHtml(bool inject)
        {
            if (string.IsNullOrWhiteSpace(_applicationPaths.WebPath))
            {
                return;
            }

            var indexFile = Path.Combine(_applicationPaths.WebPath, "index.html");
            if (!File.Exists(indexFile))
            {
                _logger.LogWarning("Could not find {IndexFile}, media requests button will not load", indexFile);
                return;
            }

            try
            {
                var original = File.ReadAllText(indexFile);
                if (inject && original.Contains(ScriptElement, StringComparison.Ordinal))
                {
                    return;
                }

                var contents = _scriptRegex.Replace(original, string.Empty);
                if (inject)
                {
                    var bodyClosing = contents.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
                    if (bodyClosing == -1)
                    {
                        _logger.LogWarning("No </body> in {IndexFile}, media requests button will not load", indexFile);
                        return;
                    }

                    contents = contents.Insert(bodyClosing, ScriptElement);
                }

                if (contents != original)
                {
                    File.WriteAllText(indexFile, contents);
                    _logger.LogInformation("{Action} Media Requests script in {IndexFile}", inject ? "Injected" : "Removed", indexFile);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Could not update {IndexFile}. Make sure Jellyfin can write to its web directory", indexFile);
            }
        }
    }
}

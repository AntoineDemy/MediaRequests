using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.MediaRequests.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>Clé API TMDB (v3) ou jeton de lecture (v4, commence par "eyJ"). Gratuit sur themoviedb.org.</summary>
        public string TmdbApiKey { get; set; } = string.Empty;

        /// <summary>Langue des résultats TMDB.</summary>
        public string Language { get; set; } = "fr-FR";

        /// <summary>Nombre max de demandes en attente par utilisateur.</summary>
        public int MaxPendingPerUser { get; set; } = 5;

        /// <summary>Popup dans Jellyfin sur les sessions admin ouvertes à chaque nouvelle demande.</summary>
        public bool NotifyAdminSessions { get; set; } = true;

        /// <summary>URL publique de Jellyfin (optionnel, pour les liens et boutons dans ntfy).</summary>
        public string PublicServerUrl { get; set; } = string.Empty;

        /// <summary>Serveur ntfy (optionnel, notif téléphone).</summary>
        public string NtfyServer { get; set; } = "https://ntfy.sh";

        /// <summary>Topic ntfy. Vide = désactivé.</summary>
        public string NtfyTopic { get; set; } = string.Empty;

        /// <summary>Token ntfy (optionnel).</summary>
        public string NtfyToken { get; set; } = string.Empty;
    }
}

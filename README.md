<p align="center">
  <img src="icon.png" alt="Media Requests" width="128">
</p>

<p align="center">
  <a href="https://opensource.org/licenses/MIT"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="License: MIT"></a>
  <a href="https://jellyfin.org/"><img src="https://img.shields.io/badge/Jellyfin-10.9.0%2B-AA5CC3?logo=jellyfin&logoColor=white" alt="Jellyfin 10.9.0+"></a>
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 8">
  <img src="https://img.shields.io/badge/client-web%20UI-00A4DC" alt="Web UI">
</p>

# Media Requests

Une page où tes utilisateurs cherchent un film ou une série (base TMDB).
- Si c'est déjà sur Jellyfin : bouton **▶ Regarder**.
- Sinon : bouton **Demander** (avec un petit mot optionnel).
- Toi, tu reçois une **notification dans Jellyfin** (popup sur tes sessions admin ouvertes) et, en option, sur ton téléphone via **ntfy**.
- Tu acceptes ou refuses dans **Tableau de bord > Demandes**. Quand le titre arrive dans ta bibliothèque, la demande passe toute seule en **Disponible** chez l'utilisateur.

Les utilisateurs n'ont accès à rien d'autre (pas de Jellyseerr, pas de Radarr). Ils se connectent avec leur compte Jellyfin.

**Installation rapide :** ajoute ce dépôt dans **Tableau de bord → Plugins → Dépôts** :

```text
https://raw.githubusercontent.com/AntoineDemy/MediaRequests/main/manifest.json
```

> **Note :** le bouton dans l'interface n'apparaît que sur la **version web** (navigateur, ordinateur ou mobile). Les applis natives (Android TV, Swiftfin, …) ne chargent pas les scripts des plugins : pour elles, utilise le [lien direct](#le-lien-direct).

---

## Installation

### Depuis le dépôt de plugins

1. **Tableau de bord → Plugins → Dépôts → +**
2. Colle l'URL ci-dessus
3. **Catalogue** → **Media Requests** → **Installer**
4. Redémarre Jellyfin

### Manuelle

1. Compile le plugin (voir [Compiler](#compiler)) ou télécharge un zip dans `releases/`
2. Copie `Jellyfin.Plugin.MediaRequests.dll` dans `<config-jellyfin>/plugins/MediaRequests/`
   (`/var/lib/jellyfin/plugins/` en LXC/Debian, `/config/plugins/` en Docker)
3. Redémarre Jellyfin

Au démarrage, le plugin ajoute sa balise `<script>` dans le `index.html` de jellyfin-web (et la retire à la désinstallation). Jellyfin doit donc pouvoir écrire dans son dossier web. Pense à vider le cache du navigateur (Ctrl+F5) après l'installation.

## Configuration

**Tableau de bord > Demandes** (menu de gauche) :

| Réglage | Défaut | Effet |
|---|---|---|
| Clé API TMDB | — | Obligatoire. Gratuite sur themoviedb.org (Paramètres → API). Clé v3 ou jeton v4. |
| Langue | `fr-FR` | Langue des résultats TMDB |
| Demandes en attente max | 5 | Par utilisateur |
| Notification Jellyfin | activée | Popup sur les sessions admin ouvertes |
| URL publique | — | Pour les liens et les boutons **Accepter / Refuser** dans ntfy |
| ntfy (serveur, topic, token) | `https://ntfy.sh` | Notif téléphone. Topic vide = désactivé. Choisis un topic difficile à deviner. |

## Dans l'interface Jellyfin

Tous les utilisateurs connectés sur la version web ont :
- une icône 🎬 dans la barre du haut, à côté de la recherche ;
- une entrée « Demander un film ou une série » dans le menu latéral (Jellyfin 10.x ; la nouvelle interface de Jellyfin 12 n'a plus ce menu, seule l'icône du haut est ajoutée).

Les deux ouvrent la page de demande par-dessus Jellyfin, déjà connectée (pas de mot de passe à retaper).
Le bouton « Regarder » ferme la fenêtre et ouvre directement la fiche du film.

## Le lien direct

```
https://ton-jellyfin/MediaRequests/Page
```

Il est affiché (avec un bouton Copier) en haut de la page Demandes du tableau de bord.

## Notes

- La popup Jellyfin n'apparaît que si tu as une session ouverte (web, appli…) au moment de la demande.
  Sinon, la demande t'attend dans la page Demandes. Active ntfy pour être prévenu à coup sûr.
- La détection « déjà sur Jellyfin » se base sur l'ID TMDB de tes films et séries (fourni par défaut par le fournisseur de métadonnées TheMovieDb).
- Les demandes sont stockées dans `requests.json`, dans le dossier data du plugin.
- Un utilisateur ne peut pas demander un titre déjà demandé ou déjà présent.

## API

| Méthode | Route | Accès |
|---|---|---|
| `GET` | `/MediaRequests/Page` | public (la page a son propre login) |
| `GET` | `/MediaRequests/Search?query=` | utilisateur connecté |
| `GET` | `/MediaRequests/Mine` | utilisateur connecté |
| `POST` | `/MediaRequests/Request` | utilisateur connecté |
| `DELETE` | `/MediaRequests/Mine/{id}` | utilisateur (ses demandes en attente) |
| `GET` | `/MediaRequests/All` | admin |
| `POST` | `/MediaRequests/{id}/Approve` · `/Deny` | admin |
| `DELETE` | `/MediaRequests/{id}` | admin |
| `POST` | `/MediaRequests/Action/{id}?decision=&token=` | boutons ntfy (token secret par demande) |

## Compiler

L'API des plugins Jellyfin change d'une version à l'autre, donc le plugin est compilé une fois par version (10.9, 10.10, 10.11, 12.x) avec le paquet `Jellyfin.Controller` correspondant. La compilation tourne dans Docker :

```bash
./build.sh            # toutes les versions
./build.sh 10.11.0    # une seule
```

`build.sh` minifie d'abord `requests.js` avec esbuild via `scripts/minify.mjs` ; un simple `dotnet build` embarque la version lisible.

Chaque build produit `releases/media-requests_<version>_jellyfin-<jellyfin>.zip` et affiche son MD5. Ajoute une entrée par zip dans `manifest.json` (le `targetAbi` le plus récent en premier).

```
Api/MediaRequestsController.cs  routes REST
Data/                           modèles + stockage JSON (singleton)
Services/                       TMDB, index de la bibliothèque, notifications
Configuration/requests.js       bouton + fenêtre dans l'interface web
Configuration/request.html      page de recherche / demande
Configuration/configPage.html   page Demandes du tableau de bord
scripts/minify.mjs              minification de requests.js
assets/icon.svg                 source de l'icône
```

Pour régénérer l'icône après modification du SVG :

```bash
rsvg-convert -w 512 assets/icon.svg -o icon.png
```

## Licence

MIT. Voir [LICENSE](LICENSE).

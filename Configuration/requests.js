/* Media Requests : chargé dans l'interface web Jellyfin via la balise <script> ajoutée à index.html. */
(function () {
  'use strict';
  if (window.__mediaRequestsLoaded) return;
  window.__mediaRequestsLoaded = true;

  var LABEL = 'Demander un film ou une série';
  var scheduled = false;
  // Couleurs du thème Jellyfin actif (variables définies par les thèmes de jellyfin-web 10.10+).
  var BG = 'var(--jf-palette-background-default, #101010)';
  var FG = 'var(--jf-palette-text-primary, #fff)';

  function api() { return window.ApiClient; }

  function loggedIn() {
    try { return !!(api() && api().accessToken()); } catch (e) { return false; }
  }

  function pageUrl() {
    try { return api().getUrl('MediaRequests/Page') + '?embedded=1'; } catch (e) { return null; }
  }

  // ---------- Fenêtre par-dessus Jellyfin ----------

  function overlay() {
    var ov = document.getElementById('mrOverlay');
    if (ov) return ov;

    ov = document.createElement('div');
    ov.id = 'mrOverlay';
    ov.style.cssText = 'position:fixed;inset:0;z-index:100000;background:' + BG + ';display:none;flex-direction:column;';

    var bar = document.createElement('div');
    bar.style.cssText = 'display:flex;align-items:center;justify-content:space-between;padding:6px 10px;' +
      'padding-top:max(6px, env(safe-area-inset-top));background:' + BG + ';color:' + FG + ';font-weight:600;';
    bar.innerHTML = '<span style="padding-left:6px">' + LABEL + '</span>';

    var close = document.createElement('button');
    close.type = 'button';
    close.title = 'Fermer';
    close.setAttribute('aria-label', 'Fermer');
    close.style.cssText = 'background:none;border:0;color:inherit;cursor:pointer;padding:8px;display:flex;';
    close.innerHTML = '<span class="material-icons close" aria-hidden="true"></span>';
    close.addEventListener('click', hide);
    bar.appendChild(close);

    var frame = document.createElement('iframe');
    frame.id = 'mrFrame';
    frame.title = LABEL;
    frame.style.cssText = 'flex:1;border:0;width:100%;background:' + BG + ';';

    ov.appendChild(bar);
    ov.appendChild(frame);
    document.body.appendChild(ov);
    return ov;
  }

  function show() {
    var url = pageUrl();
    if (!url) return;
    var ov = overlay();
    document.getElementById('mrFrame').src = url;
    ov.style.display = 'flex';
    document.documentElement.style.overflow = 'hidden';
  }

  function hide() {
    var ov = document.getElementById('mrOverlay');
    if (!ov) return;
    ov.style.display = 'none';
    document.getElementById('mrFrame').src = 'about:blank';
    document.documentElement.style.overflow = '';
  }

  // Utilisé par la page embarquée (bouton "Regarder").
  window.__mediaRequestsClose = hide;

  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') hide();
  });
  window.addEventListener('hashchange', hide);

  // ---------- Bouton dans la barre du haut ----------

  function addHeaderButton() {
    var right = document.querySelector('.skinHeader .headerRight');
    if (!right || right.querySelector('.headerMediaRequests')) return;

    var b = document.createElement('button');
    b.type = 'button';
    b.title = LABEL;
    b.setAttribute('aria-label', LABEL);
    b.className = 'headerButton headerButtonRight paper-icon-button-light headerMediaRequests';
    b.innerHTML = '<span class="material-icons movie_filter" aria-hidden="true"></span>';
    b.addEventListener('click', show);

    var search = right.querySelector('.headerSearchButton');
    right.insertBefore(b, search || right.firstChild);
  }

  // ---------- Bouton dans la nouvelle barre du haut (Jellyfin 12+, React/MUI) ----------
  // L'ancienne .skinHeader reste dans le DOM mais masquée : on ajoute aussi le bouton dans la barre MUI.

  var ICON_PATH = 'm18 4 2 3h-3l-2-3h-2l2 3h-3l-2-3H8l2 3H7L5 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V4z' +
    'm-6.75 11.25L10 18l-1.25-2.75L6 14l2.75-1.25L10 10l1.25 2.75L14 14zm5.69-3.31L16 14l-.94-2.06L13 11l2.06-.94L16 8l.94 2.06L19 11z';

  function addAppBarButton() {
    var search = document.querySelector('header.MuiAppBar-root a[href="#/search"]');
    if (!search || !search.parentNode || search.parentNode.querySelector('.mrAppBarButton')) return;

    var b = document.createElement('button');
    b.type = 'button';
    b.title = LABEL;
    b.setAttribute('aria-label', LABEL);
    // Mêmes classes que le bouton Rechercher pour avoir le même style.
    b.className = search.className + ' mrAppBarButton';
    b.innerHTML = '<svg class="' + (search.querySelector('svg') ? search.querySelector('svg').getAttribute('class') : '') +
      '" focusable="false" aria-hidden="true" viewBox="0 0 24 24"><path d="' + ICON_PATH + '"></path></svg>';
    b.addEventListener('click', show);

    search.parentNode.insertBefore(b, search);
  }

  // ---------- Entrée dans le menu latéral ----------

  function addDrawerLink() {
    var container = document.querySelector('.mainDrawer-scrollContainer');
    if (!container || container.querySelector('.navMenuOption-mediaRequests')) return;

    var a = document.createElement('a');
    a.href = '#';
    a.className = 'navMenuOption emby-button navMenuOption-mediaRequests';
    a.innerHTML = '<span class="material-icons navMenuOptionIcon movie_filter" aria-hidden="true"></span>' +
      '<span class="navMenuOptionText">' + LABEL + '</span>';
    a.addEventListener('click', function (e) {
      e.preventDefault();
      show();
    });

    // Juste après "Accueil" si on le trouve, sinon en haut du menu.
    var home = container.querySelector('.lnkMediaFolder[href*="home"], a[href="#/home"], a[href="#/home.html"]');
    if (home && home.parentNode) {
      home.parentNode.insertBefore(a, home.nextSibling);
    } else {
      container.insertBefore(a, container.firstChild);
    }
  }

  // ---------- Rappel admin : demandes en attente ----------
  // Badge sur l'icône + message une fois par session. Ne concerne que les admins (l'API renvoie 403 aux autres).

  var ADMIN_PAGE = '#/configurationpage?name=MediaRequests';
  var REFRESH_MS = 2 * 60 * 1000;
  var pending = 0;
  var notAdminToken = null;
  var lastFetch = 0;

  function refreshPending(force) {
    var token;
    try { token = api().accessToken(); } catch (e) { return; }
    if (!token || token === notAdminToken) return;
    if (!force && Date.now() - lastFetch < 15000) return;
    lastFetch = Date.now();

    fetch(api().getUrl('MediaRequests/PendingCount'), { headers: { 'X-Emby-Token': token } })
      .then(function (res) {
        if (res.status === 401 || res.status === 403) {
          notAdminToken = token;
          return null;
        }
        return res.ok ? res.json() : null;
      })
      .then(function (data) {
        if (!data) return;
        pending = data.count || 0;
        renderBadges();
        if (pending > 0) showReminder(token);
      })
      .catch(function () { /* serveur injoignable : on réessaiera */ });
  }

  function renderBadges() {
    var targets = document.querySelectorAll('.headerMediaRequests, .mrAppBarButton, .navMenuOption-mediaRequests');
    for (var i = 0; i < targets.length; i++) {
      var t = targets[i];
      var badge = t.querySelector('.mrBadge');
      if (!pending) {
        if (badge) badge.remove();
        continue;
      }

      if (!badge) {
        badge = document.createElement('span');
        badge.className = 'mrBadge';
        var inline = t.classList.contains('navMenuOption-mediaRequests');
        badge.style.cssText = (inline
          ? 'margin-left:auto;'
          : 'position:absolute;top:0;right:0;transform:translate(30%,-20%);' +
            'box-shadow:0 0 0 2px var(--jf-palette-background-default, #101010);') +
          'min-width:16px;height:16px;padding:0 4px;border-radius:8px;font-size:10px;font-weight:700;line-height:16px;' +
          'text-align:center;pointer-events:none;color:#fff;background:var(--jf-palette-primary-main, #00a4dc);';
        if (!inline) t.style.position = 'relative';
        t.appendChild(badge);
      }

      var text = pending > 99 ? '99+' : String(pending);
      if (badge.textContent !== text) badge.textContent = text;
      t.title = LABEL + ' · ' + pending + ' demande' + (pending > 1 ? 's' : '') + ' en attente';
    }
  }

  function showReminder(token) {
    var key = 'mediaRequests.reminder';
    try {
      if (sessionStorage.getItem(key) === token) return;
      sessionStorage.setItem(key, token);
    } catch (e) { /* stockage indisponible : on affiche quand même */ }

    var old = document.getElementById('mrReminder');
    if (old) old.remove();

    var box = document.createElement('div');
    box.id = 'mrReminder';
    box.setAttribute('role', 'status');
    box.style.cssText = 'position:fixed;left:50%;bottom:24px;transform:translateX(-50%);z-index:99999;display:flex;' +
      'align-items:center;gap:14px;padding:12px 14px 12px 18px;border-radius:12px;max-width:92vw;' +
      'box-shadow:0 8px 30px rgba(0,0,0,.45);font-weight:600;' +
      'background:var(--jf-palette-background-paper, #262628);color:var(--jf-palette-text-primary, #fff);';

    var text = document.createElement('span');
    text.textContent = '🎬 ' + pending + ' demande' + (pending > 1 ? 's' : '') + ' en attente';

    var view = document.createElement('button');
    view.type = 'button';
    view.textContent = 'Voir';
    view.style.cssText = 'border:0;border-radius:8px;padding:7px 14px;font:inherit;cursor:pointer;color:#fff;' +
      'background:var(--jf-palette-primary-main, #00a4dc);';
    view.addEventListener('click', function () {
      box.remove();
      location.hash = ADMIN_PAGE;
    });

    var close = document.createElement('button');
    close.type = 'button';
    close.setAttribute('aria-label', 'Fermer');
    close.textContent = '✕';
    close.style.cssText = 'border:0;background:none;color:inherit;opacity:.7;cursor:pointer;font:inherit;padding:4px;';
    close.addEventListener('click', function () { box.remove(); });

    box.appendChild(text);
    box.appendChild(view);
    box.appendChild(close);
    document.body.appendChild(box);
    setTimeout(function () { box.remove(); }, 12000);
  }

  // Après une décision sur la page Demandes, le compteur est rafraîchi au changement de page.
  window.addEventListener('hashchange', function () { refreshPending(true); });
  setInterval(function () { refreshPending(true); }, REFRESH_MS);

  function tick() {
    scheduled = false;
    if (!loggedIn()) return;
    addHeaderButton();
    addAppBarButton();
    addDrawerLink();
    renderBadges();
    refreshPending(false);
  }

  // L'interface Jellyfin redessine souvent le header et le menu : on ré-ajoute si besoin.
  new MutationObserver(function () {
    if (scheduled) return;
    scheduled = true;
    requestAnimationFrame(tick);
  }).observe(document.documentElement, { childList: true, subtree: true });

  tick();
})();

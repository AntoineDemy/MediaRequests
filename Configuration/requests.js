/* Media Requests : chargé dans l'interface web Jellyfin via la balise <script> ajoutée à index.html. */
(function () {
  'use strict';
  if (window.__mediaRequestsLoaded) return;
  window.__mediaRequestsLoaded = true;

  var LABEL = 'Demander un film ou une série';
  var scheduled = false;

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
    ov.style.cssText = 'position:fixed;inset:0;z-index:100000;background:#101010;display:none;flex-direction:column;';

    var bar = document.createElement('div');
    bar.style.cssText = 'display:flex;align-items:center;justify-content:space-between;padding:6px 10px;' +
      'padding-top:max(6px, env(safe-area-inset-top));background:#101010;color:#fff;font-weight:600;';
    bar.innerHTML = '<span style="padding-left:6px">' + LABEL + '</span>';

    var close = document.createElement('button');
    close.type = 'button';
    close.title = 'Fermer';
    close.setAttribute('aria-label', 'Fermer');
    close.style.cssText = 'background:none;border:0;color:#fff;cursor:pointer;padding:8px;display:flex;';
    close.innerHTML = '<span class="material-icons close" aria-hidden="true"></span>';
    close.addEventListener('click', hide);
    bar.appendChild(close);

    var frame = document.createElement('iframe');
    frame.id = 'mrFrame';
    frame.title = LABEL;
    frame.style.cssText = 'flex:1;border:0;width:100%;background:#101010;';

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

  function tick() {
    scheduled = false;
    if (!loggedIn()) return;
    addHeaderButton();
    addDrawerLink();
  }

  // L'interface Jellyfin redessine souvent le header et le menu : on ré-ajoute si besoin.
  new MutationObserver(function () {
    if (scheduled) return;
    scheduled = true;
    requestAnimationFrame(tick);
  }).observe(document.documentElement, { childList: true, subtree: true });

  tick();
})();

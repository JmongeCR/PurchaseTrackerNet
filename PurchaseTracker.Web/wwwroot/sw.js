/**
 * PurchaseTracker Service Worker v2
 *
 * Strategy:
 *  - Static assets (css/js/img/font): Cache-first, update in background
 *  - Navigation (HTML pages):         Network-first, fall back to cache
 *  - Offline:                         Show stale cached page with offline banner
 *                                     (never redirect to login — show cached data)
 *  - Offline mutations:               Queue in IDB, replay when online (background sync)
 */

const CACHE_VERSION   = 'pt-v2';
const STATIC_CACHE    = `${CACHE_VERSION}-static`;
const PAGES_CACHE     = `${CACHE_VERSION}-pages`;
const SYNC_TAG        = 'pt-sync-transactions';

const STATIC_PRECACHE = [
  '/css/site.css',
  '/manifest.json',
  '/icons/icon-192.png',
  '/icons/icon-512.png',
  'https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css',
  'https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js',
];

const STATIC_EXTS = new Set([
  'css','js','png','jpg','jpeg','webp','svg','ico','woff','woff2','ttf','otf'
]);

/* ── Install ──────────────────────────────────────────────── */
self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(STATIC_CACHE)
      .then(cache => cache.addAll(STATIC_PRECACHE))
      .then(() => self.skipWaiting())
  );
});

/* ── Activate — prune old caches ─────────────────────────── */
self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys().then(keys =>
      Promise.all(
        keys
          .filter(k => k !== STATIC_CACHE && k !== PAGES_CACHE)
          .map(k => caches.delete(k))
      )
    ).then(() => self.clients.claim())
  );
});

/* ── Fetch ────────────────────────────────────────────────── */
self.addEventListener('fetch', event => {
  const { request } = event;
  const url = new URL(request.url);

  // Only handle GET same-origin (+ CDN statics)
  if (request.method !== 'GET') return;
  if (url.origin !== self.location.origin && !url.hostname.includes('jsdelivr.net')) return;

  // Auth pages: always network-first, no stale fallback to avoid stuck login
  if (url.pathname.startsWith('/auth/')) {
    event.respondWith(networkOnly(request));
    return;
  }

  // Static assets: cache-first, update in background
  if (isStaticAsset(url)) {
    event.respondWith(cacheFirstRevalidate(request, STATIC_CACHE));
    return;
  }

  // Navigation / HTML: network-first, fall back to stale cache
  if (request.mode === 'navigate' || request.headers.get('accept')?.includes('text/html')) {
    event.respondWith(networkFirstWithStale(request));
    return;
  }
});

/* ── Background Sync (offline transaction queue) ─────────── */
self.addEventListener('sync', event => {
  if (event.tag === SYNC_TAG) {
    event.waitUntil(replaySyncQueue());
  }
});

/* ── Message bus (from app) ───────────────────────────────── */
self.addEventListener('message', event => {
  if (event.data?.type === 'SKIP_WAITING') self.skipWaiting();
  if (event.data?.type === 'CACHE_PAGE')   cachePage(event.data.url);
});

/* ══════════════════════════════════════════════════════════ */
/* Strategies                                                 */
/* ══════════════════════════════════════════════════════════ */

async function networkOnly(request) {
  return fetch(request);
}

async function cacheFirstRevalidate(request, cacheName) {
  const cache  = await caches.open(cacheName);
  const cached = await cache.match(request);

  // Always revalidate in background
  const fetchPromise = fetch(request).then(response => {
    if (response.ok) cache.put(request, response.clone());
    return response;
  }).catch(() => null);

  return cached ?? (await fetchPromise) ?? offlineAssetResponse();
}

async function networkFirstWithStale(request) {
  const cache = await caches.open(PAGES_CACHE);

  try {
    const response = await fetch(request);
    if (response.ok) cache.put(request, response.clone());
    return response;
  } catch {
    // Offline: return cached page + inject offline banner via headers
    const cached = await cache.match(request);
    if (cached) {
      // Clone the response and add a custom header so the app can
      // detect it's serving stale content and show the offline banner
      const headers = new Headers(cached.headers);
      headers.set('X-PT-Offline', '1');
      const body = await cached.arrayBuffer();
      return new Response(body, {
        status: cached.status,
        statusText: cached.statusText,
        headers,
      });
    }

    // No cache at all: generic offline shell
    return offlineShell();
  }
}

async function cachePage(url) {
  try {
    const response = await fetch(url);
    if (response.ok) {
      const cache = await caches.open(PAGES_CACHE);
      cache.put(url, response);
    }
  } catch { /* silent */ }
}

/* ── Offline transaction sync queue ─────────────────────── */
async function replaySyncQueue() {
  const db      = await openSyncDB();
  const pending = await getAllPending(db);

  for (const item of pending) {
    try {
      const res = await fetch(item.url, {
        method:  item.method,
        headers: item.headers,
        body:    item.body,
      });
      if (res.ok) await deletePending(db, item.id);
    } catch {
      // Will retry on next sync
    }
  }
}

/* ── IndexedDB helpers ───────────────────────────────────── */
function openSyncDB() {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open('pt-sync', 1);
    req.onupgradeneeded = e => {
      const db = e.target.result;
      if (!db.objectStoreNames.contains('queue')) {
        db.createObjectStore('queue', { keyPath: 'id', autoIncrement: true });
      }
    };
    req.onsuccess = e => resolve(e.target.result);
    req.onerror   = e => reject(e.target.error);
  });
}
function getAllPending(db) {
  return new Promise((resolve, reject) => {
    const tx  = db.transaction('queue', 'readonly');
    const req = tx.objectStore('queue').getAll();
    req.onsuccess = e => resolve(e.target.result);
    req.onerror   = e => reject(e.target.error);
  });
}
function deletePending(db, id) {
  return new Promise((resolve, reject) => {
    const tx  = db.transaction('queue', 'readwrite');
    const req = tx.objectStore('queue').delete(id);
    req.onsuccess = () => resolve();
    req.onerror   = e => reject(e.target.error);
  });
}

/* ── Fallbacks ───────────────────────────────────────────── */
function offlineAssetResponse() {
  return new Response('', { status: 503, statusText: 'Offline' });
}

function offlineShell() {
  return new Response(
    `<!doctype html>
<html lang="es">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <title>PurchaseTracker — Sin conexión</title>
  <style>
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      font-family: 'Inter', system-ui, sans-serif;
      background: #f8fafc; color: #0f172a;
      min-height: 100dvh;
      display: flex; align-items: center; justify-content: center;
      text-align: center; padding: 24px;
    }
    .icon {
      font-size: 52px; margin-bottom: 20px;
    }
    h1 { font-size: 1.5rem; font-weight: 800; margin-bottom: 8px; }
    p  { font-size: 0.9rem; color: #64748b; line-height: 1.6; max-width: 280px; margin: 0 auto 24px; }
    button {
      background: #2563eb; color: white;
      border: none; border-radius: 12px;
      padding: 12px 28px; font-size: 0.875rem; font-weight: 600;
      cursor: pointer; font-family: inherit;
    }
  </style>
</head>
<body>
  <div>
    <div class="icon">📶</div>
    <h1>Sin conexión</h1>
    <p>No hay internet en este momento. Vuelve cuando tengas conexión para ver tus movimientos.</p>
    <button onclick="location.reload()">Reintentar</button>
  </div>
</body>
</html>`,
    { headers: { 'Content-Type': 'text/html; charset=utf-8' } }
  );
}

/* ── Helpers ─────────────────────────────────────────────── */
function isStaticAsset(url) {
  const ext = url.pathname.split('.').pop().toLowerCase();
  if (STATIC_EXTS.has(ext)) return true;
  if (url.hostname.includes('jsdelivr.net')) return true;
  return false;
}

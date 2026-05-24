/**
 * PurchaseTracker — Service Worker
 * Strategy: Network-first for navigation/API, Cache-first for static assets.
 * Provides a basic offline fallback for the shell pages.
 */

const CACHE_NAME   = 'pt-cache-v1';
const OFFLINE_URL  = '/auth/login';  // fallback page shown when fully offline

// Static assets to pre-cache on install
const PRECACHE_ASSETS = [
  '/css/site.css',
  '/manifest.json',
  '/icons/icon-192.png',
  '/icons/icon-512.png',
  'https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css',
  'https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js',
];

// ── Install ─────────────────────────────────────────────────────────────────
self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(CACHE_NAME).then(cache => cache.addAll(PRECACHE_ASSETS))
  );
  self.skipWaiting();
});

// ── Activate (clean up old caches) ──────────────────────────────────────────
self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys().then(keys =>
      Promise.all(
        keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k))
      )
    )
  );
  self.clients.claim();
});

// ── Fetch ────────────────────────────────────────────────────────────────────
self.addEventListener('fetch', event => {
  const { request } = event;
  const url = new URL(request.url);

  // Only intercept same-origin GET requests (ignore POST, API calls, etc.)
  if (request.method !== 'GET') return;

  // Cache-first for static assets (css, js, images, fonts)
  if (isStaticAsset(url)) {
    event.respondWith(cacheFirst(request));
    return;
  }

  // Network-first for navigation and HTML
  if (request.mode === 'navigate') {
    event.respondWith(networkFirstWithFallback(request));
    return;
  }
});

// ── Strategies ───────────────────────────────────────────────────────────────

async function cacheFirst(request) {
  const cached = await caches.match(request);
  if (cached) return cached;

  try {
    const response = await fetch(request);
    if (response.ok) {
      const cache = await caches.open(CACHE_NAME);
      cache.put(request, response.clone());
    }
    return response;
  } catch {
    return new Response('Asset not available offline', { status: 503 });
  }
}

async function networkFirstWithFallback(request) {
  try {
    const response = await fetch(request);
    // Cache successful navigation responses
    if (response.ok) {
      const cache = await caches.open(CACHE_NAME);
      cache.put(request, response.clone());
    }
    return response;
  } catch {
    // Offline: try cache, then generic fallback
    const cached = await caches.match(request);
    if (cached) return cached;

    const fallback = await caches.match(OFFLINE_URL);
    return fallback ?? new Response(
      '<html><body style="font-family:sans-serif;text-align:center;padding:40px">'
      + '<h2>💳 PurchaseTracker</h2><p>Sin conexión — vuelve pronto.</p></body></html>',
      { headers: { 'Content-Type': 'text/html' } }
    );
  }
}

// ── Helpers ──────────────────────────────────────────────────────────────────

function isStaticAsset(url) {
  const ext = url.pathname.split('.').pop().toLowerCase();
  const staticExts = ['css', 'js', 'png', 'jpg', 'jpeg', 'svg', 'ico', 'woff', 'woff2', 'ttf'];
  if (staticExts.includes(ext)) return true;
  if (url.hostname.includes('jsdelivr.net')) return true;
  return false;
}

const CACHE = "nptel-pwa-shell-v11";
const ASSETS = ["/","/index.html","/styles.css?v=nptel-20261015-r2","/app.js?v=nptel-20261015-r2","/manifest.webmanifest?v=nptel-20261015-r2","/icon-192.svg?v=nptel-20261015-r2","/icon-512.svg?v=nptel-20261015-r2"];

self.addEventListener("install", event => {
  event.waitUntil(caches.open(CACHE).then(cache => cache.addAll(ASSETS)));
  self.skipWaiting();
});

self.addEventListener("activate", event => {
  event.waitUntil(
    caches.keys().then(keys => Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k))))
  );
  self.clients.claim();
});

self.addEventListener("fetch", event => {
  const url = new URL(event.request.url);
  if (url.origin !== self.location.origin) return;
  if (url.pathname.startsWith("/api/")) {
    event.respondWith(fetch(event.request));
    return;
  }
  if (event.request.method !== "GET") return;
  event.respondWith(
    caches.match(event.request).then(cached => cached || fetch(event.request))
  );
});
// AssemblIQ Service Worker (PWA standalone support)
const CACHE_NAME = 'assembl-iq-v1';

self.addEventListener('install', (event) => {
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(self.clients.claim());
});

self.addEventListener('fetch', (event) => {
  // Network-first strategy to ensure real-time telemetry and state
  event.respondWith(
    fetch(event.request).catch(() => caches.match(event.request))
  );
});

self.addEventListener("install", event => {
  event.waitUntil((async () => {
    const cache = await caches.open("aria-engine-v1");
    await cache.addAll(["./", "index.html", "manifest.webmanifest"]);
    self.skipWaiting();
  })());
});

self.addEventListener("activate", event => {
  event.waitUntil(self.clients.claim());
});

self.addEventListener("fetch", event => {
  event.respondWith(
    caches.match(event.request).then(response => response || fetch(event.request))
  );
});

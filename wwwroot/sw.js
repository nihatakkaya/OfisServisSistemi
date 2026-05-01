const CACHE_NAME = 'oys-cache-v1';

// Uygulama kurulduðunda çalýþýr
self.addEventListener('install', event => {
    console.log('[ServiceWorker] Kuruldu');
    self.skipWaiting();
});

// Arka planda çalýþýp uygulamanýn hýzlý açýlmasýný saðlar
self.addEventListener('fetch', event => {
    event.respondWith(
        fetch(event.request).catch(() => {
            return caches.match(event.request);
        })
    );
});
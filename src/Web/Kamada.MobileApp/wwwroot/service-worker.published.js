// Offline support for the KAMADA phone app: the app shell (framework, assemblies, styles) is cached on install,
// so the app starts without network. API calls always go to the network. See https://aka.ms/blazor-offline-considerations

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/ ];
// appsettings.json is rewritten when the container starts (API address), so its published hash would not match.
// assistant/ holds the on-device assistant's libraries (tens of MB): cached on first use instead, see assistantRuntime.
const offlineAssetsExclude = [ /^service-worker\.js$/, /^appsettings(\.[\w-]+)?\.json$/, /^assistant\// ];
const runtimeConfig = /\/appsettings(\.[\w-]+)?\.json$/;
// WebLLM, transformers.js and ONNX Runtime of the on-device assistant: kept after the first load so the assistant starts
// without a network. Bump the cache name when deploy/assistant/fetch-runtime.sh pins other versions.
const assistantRuntime = /\/assistant\/vendor\//;
const assistantRuntimeCache = 'kamada-assistant-runtime-v1';

// Replace with your base path if you are hosting on a subfolder. Ensure there is a trailing '/'.
const base = "/";
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
    console.info('Service worker: Install');

    // Fetch and cache all matching items from the assets manifest
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
}

async function onActivate(event) {
    console.info('Service worker: Activate');

    // Delete unused caches
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    const request = event.request;
    const sameOrigin = new URL(request.url).origin === self.origin;

    // Runtime configuration: network first, last known copy when offline.
    if (request.method === 'GET' && sameOrigin && runtimeConfig.test(new URL(request.url).pathname)) {
        const cache = await caches.open(cacheName);
        try {
            const response = await fetch(request, { cache: 'no-cache' });
            if (response.ok) {
                await cache.put(request, response.clone());
            }
            return response;
        } catch {
            return (await cache.match(request)) || Response.error();
        }
    }

    // Assistant libraries: cache first, stored on first use (never part of the versioned app shell cache).
    if (request.method === 'GET' && sameOrigin && assistantRuntime.test(new URL(request.url).pathname)) {
        const cache = await caches.open(assistantRuntimeCache);
        const cached = await cache.match(request);
        if (cached) {
            return cached;
        }
        const response = await fetch(request);
        if (response.ok) {
            await cache.put(request, response.clone());
        }
        return response;
    }

    let cachedResponse = null;
    if (request.method === 'GET' && sameOrigin) {
        // For all navigation requests, try to serve index.html from cache,
        // unless that request is for an offline resource.
        const shouldServeIndexHtml = request.mode === 'navigate'
            && !manifestUrlList.some(url => url === request.url);

        const cache = await caches.open(cacheName);
        cachedResponse = await cache.match(shouldServeIndexHtml ? 'index.html' : request);
    }

    return cachedResponse || fetch(request);
}

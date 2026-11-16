// Registers the offline service worker (a file, not an inline script, so the CSP needs no exception for it).
navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });

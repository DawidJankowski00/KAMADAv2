// IndexedDB for the phone app's offline data (LocalStore in C#). Values are JSON strings written and read by .NET, so the
// C# side keeps one serializer; this module only stores them. Stores: "lookup" (products, clients and their ETags),
// "outbox" (orders waiting to be sent) and "meta" (whose data this is).

const VERSION = 1;
const STORES = ["lookup", "outbox", "meta"];
const connections = new Map();

function open(name) {
  if (!connections.has(name)) {
    connections.set(name, new Promise((resolve, reject) => {
      const request = indexedDB.open(name, VERSION);
      request.onupgradeneeded = () => {
        for (const store of STORES) {
          if (!request.result.objectStoreNames.contains(store)) request.result.createObjectStore(store);
        }
      };
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => {
        connections.delete(name);
        reject(request.error);
      };
    }));
  }
  return connections.get(name);
}

async function run(name, store, mode, action) {
  const db = await open(name);
  return new Promise((resolve, reject) => {
    const transaction = db.transaction(store, mode);
    const request = action(transaction.objectStore(store));
    transaction.oncomplete = () => resolve(request?.result ?? null);
    transaction.onerror = () => reject(transaction.error);
    transaction.onabort = () => reject(transaction.error);
  });
}

/** True when IndexedDB can be used (it is missing in some private modes). */
export function isSupported() {
  return typeof indexedDB !== "undefined";
}

export function get(name, store, key) {
  return run(name, store, "readonly", s => s.get(key));
}

export function getAll(name, store) {
  return run(name, store, "readonly", s => s.getAll());
}

export function put(name, store, key, json) {
  return run(name, store, "readwrite", s => s.put(json, key)).then(() => undefined);
}

export function remove(name, store, key) {
  return run(name, store, "readwrite", s => s.delete(key)).then(() => undefined);
}

/** Empties one store, or every store when none is given. */
export async function clear(name, store) {
  for (const s of store ? [store] : STORES) {
    await run(name, s, "readwrite", os => os.clear());
  }
}

/** Asks the browser not to evict the data under storage pressure (queued orders live here). */
export async function persist() {
  try {
    return navigator.storage?.persist ? await navigator.storage.persist() : false;
  } catch {
    return false;
  }
}

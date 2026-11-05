window.nexStorage = {
    area: name => name === 'session' ? sessionStorage : localStorage,
    get: (area, key) => window.nexStorage.area(area).getItem(key),
    set: (area, key, value) => window.nexStorage.area(area).setItem(key, value),
    remove: (area, key) => window.nexStorage.area(area).removeItem(key),
    isOnline: () => navigator.onLine,
    release: id => { window.nexStorage.locks?.get(id)?.(); },
    acquire: (name, id) => new Promise((resolve, reject) => {
        if (!navigator.locks) { reject(new Error('Este navegador não oferece bloqueio seguro para atendimento offline.')); return; }
        window.nexStorage.locks ??= new Map();
        const abort = new AbortController();
        const timeout = setTimeout(() => abort.abort(), 20000);
        navigator.locks.request(name, { signal: abort.signal }, () => new Promise(done => {
            clearTimeout(timeout);
            window.nexStorage.locks.set(id, () => { window.nexStorage.locks.delete(id); done(); });
            resolve();
        })).catch(reject);
    })
};

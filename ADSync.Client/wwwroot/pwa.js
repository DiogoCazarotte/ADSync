// Instalação pede uma ação do usuário; cache e preparação funcionam sem instalá-la.
window.nexPwa = (() => {
    let prompt, reference, registration, ready = false;
    const standalone = () => matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;
    const publish = () => reference?.invokeMethodAsync('PwaChanged', navigator.onLine, !!prompt, standalone(), ready, !!registration?.waiting).catch(() => {});
    const connected = () => { publish(); reference?.invokeMethodAsync('ConnectionRestored').catch(() => {}); };
    addEventListener('beforeinstallprompt', event => { event.preventDefault(); prompt = event; publish(); });
    addEventListener('appinstalled', () => { prompt = null; publish(); });
    addEventListener('online', connected);
    addEventListener('offline', publish);
    return {
        connect: ref => { reference = ref; publish(); },
        disconnect: () => { reference = null; },
        install: async () => {
            if (!prompt) return;
            const current = prompt; prompt = null;
            await current.prompt();
            await current.userChoice;
            publish();
        },
        register: async () => {
            if (!('serviceWorker' in navigator)) return;
            try {
                registration = await navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });
                registration.addEventListener('updatefound', () => {
                    const installing = registration.installing;
                    installing?.addEventListener('statechange', () => {
                        if (installing.state === 'installed') {
                            caches.keys().then(keys => { ready = keys.some(k => k.startsWith('offline-cache-')); publish(); });
                        }
                    });
                });
                await navigator.serviceWorker.ready;
                ready = (await caches.keys()).some(k => k.startsWith('offline-cache-'));
                publish();
            } catch (error) { console.error('Não foi possível preparar a PWA:', error); publish(); }
        }
    };
})();

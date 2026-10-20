(() => {
    const open = () => new Promise((resolve, reject) => {
        const request = indexedDB.open('chagi-results-v1', 1);
        request.onupgradeneeded = () => request.result.createObjectStore('snapshots');
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
        request.onblocked = () => reject(new Error('Databasen används i en annan flik.'));
    });
    window.chagiCache = {
        async read() {
            const db = await open();
            try {
                return await new Promise((resolve, reject) => {
                    const transaction = db.transaction('snapshots', 'readonly');
                    const request = transaction.objectStore('snapshots').get('current');
                    request.onsuccess = () => resolve(request.result ?? null);
                    request.onerror = () => reject(request.error);
                    transaction.onabort = () => reject(transaction.error);
                });
            } finally { db.close(); }
        },
        async write(snapshot) {
            const db = await open();
            try {
                await new Promise((resolve, reject) => {
                    const transaction = db.transaction('snapshots', 'readwrite');
                    transaction.objectStore('snapshots').put(snapshot, 'current');
                    transaction.oncomplete = () => resolve();
                    transaction.onerror = () => reject(transaction.error);
                    transaction.onabort = () => reject(transaction.error);
                });
            } finally { db.close(); }
        }
    };
})();

mergeInto(LibraryManager.library, {
    YardEditor_SaveToLocalStorage: function(keyPtr, valuePtr) {
        try {
            var key = UTF8ToString(keyPtr);
            var value = UTF8ToString(valuePtr);
            window.localStorage.setItem(key, value);
        } catch (e) {
            console.warn('[YardEditor] LocalStorage Save error:', e);
        }
    },
    YardEditor_LoadFromLocalStorage: function(keyPtr) {
        try {
            var key = UTF8ToString(keyPtr);
            var value = window.localStorage.getItem(key);
            if (!value) return null;
            var bufferSize = lengthBytesUTF8(value) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(value, buffer, bufferSize);
            return buffer;
        } catch (e) {
            console.warn('[YardEditor] LocalStorage Load error:', e);
            return null;
        }
    },
    YardEditor_HasLocalStorageKey: function(keyPtr) {
        try {
            var key = UTF8ToString(keyPtr);
            return (window.localStorage.getItem(key) !== null) ? 1 : 0;
        } catch (e) {
            return 0;
        }
    },
    YardEditor_RemoveLocalStorageKey: function(keyPtr) {
        try {
            var key = UTF8ToString(keyPtr);
            window.localStorage.removeItem(key);
        } catch (e) {
            console.warn('[YardEditor] LocalStorage Remove error:', e);
        }
    },
    YardEditor_DownloadFile: function(filenamePtr, contentPtr) {
        try {
            var filename = UTF8ToString(filenamePtr);
            var content = UTF8ToString(contentPtr);
            // Autosave langsung ke penyimpanan data/denah.json tanpa memunculkan popup dialog "Save As" browser
            window.localStorage.setItem('data/' + filename, content);
            if (typeof fetch === 'function') {
                fetch('data/' + filename, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: content
                }).catch(function() {});
            }
            console.log('[YardEditor] Autosaved to data/' + filename + ' silently!');
        } catch (e) {
            console.warn('[YardEditor] Autosave data/' + filename + ' warning:', e);
        }
    }
});


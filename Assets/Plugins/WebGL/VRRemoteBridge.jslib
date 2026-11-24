mergeInto(LibraryManager.library, {
    VRRemote_StartHost: function(roomCodePtr) {
        var roomCode = UTF8ToString(roomCodePtr);
        if (window.vrRemote && typeof window.vrRemote.startHost === 'function') {
            window.vrRemote.startHost(roomCode);
        }
    },
    VRRemote_ConnectViewer: function(roomCodePtr) {
        var roomCode = UTF8ToString(roomCodePtr);
        if (window.vrRemote && typeof window.vrRemote.connectViewer === 'function') {
            window.vrRemote.connectViewer(roomCode);
        }
    },
    VRRemote_SendMovement: function(dataStrPtr) {
        var dataStr = UTF8ToString(dataStrPtr);
        if (window.vrRemote && typeof window.vrRemote.sendMovement === 'function') {
            window.vrRemote.sendMovement(dataStr);
        }
    },
    VRRemote_GetUrlQuery: function() {
        var search = window.location.search || '';
        var bufferSize = lengthBytesUTF8(search) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(search, buffer, bufferSize);
        return buffer;
    },
    VRRemote_GetAbsoluteUrl: function() {
        var url = window.location.origin + window.location.pathname;
        var bufferSize = lengthBytesUTF8(url) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(url, buffer, bufferSize);
        return buffer;
    }
});


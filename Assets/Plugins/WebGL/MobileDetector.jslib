mergeInto(LibraryManager.library, {
    IsMobileBrowser: function() {
        var ua = window.navigator.userAgent || window.navigator.vendor || window.opera;
        var isMobile = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(ua);
        return isMobile ? 1 : 0;
    },
    EnterVRIfNotActive: function() {
        try {
            var xr = (typeof Module !== 'undefined' && Module.WebXR) ? Module.WebXR : null;
            if (xr && !xr.isInSession && xr.isVRSupported) {
                xr.toggleVR();
            }
        } catch (e) {
            console.warn('[WebXR] EnterVRIfNotActive error:', e);
        }
    }
});

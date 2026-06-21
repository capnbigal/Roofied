// Thin wrapper around Cloudflare Turnstile. No-op friendly: when the widget isn't present
// (captcha disabled), getToken returns an empty string and the server treats verification as passed.
window.roofiedCaptcha = {
    getToken: function () {
        try {
            if (window.turnstile && typeof window.turnstile.getResponse === "function") {
                return window.turnstile.getResponse() || "";
            }
        } catch (e) { /* ignore */ }
        return "";
    },
    reset: function () {
        try { if (window.turnstile) window.turnstile.reset(); } catch (e) { /* ignore */ }
    }
};

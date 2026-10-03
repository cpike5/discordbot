/**
 * ApiClient - shared fetch wrapper module.
 *
 * Centralizes anti-forgery token injection, JSON (de)serialization, and
 * error parsing/toast reporting that were previously duplicated across
 * individual page scripts (settings.js, moderation-settings.js, portal-tts.js, ...).
 *
 * Exposed as window.ApiClient (browser) and module.exports (Node/tests).
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory();
    } else {
        root.ApiClient = factory();
    }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    /** Default request timeout. Pass `timeout: 0` to wait indefinitely (e.g. long uploads). */
    const DEFAULT_TIMEOUT_MS = 30000;

    /** Where an expired session is sent to sign in again. */
    const LOGIN_PATH = '/Account/Login';

    const SESSION_EXPIRED_MESSAGE = 'Your session has expired. Sign in again to continue.';
    const NETWORK_MESSAGE = 'Could not reach the server. Check your connection and try again.';
    const TIMEOUT_MESSAGE = 'The server took too long to respond. Try again.';

    /**
     * Typed error thrown by the throwing request helpers (request/get/post/put/del), and by
     * every helper for network failures and timeouts.
     * Carries the HTTP status (0 when no response arrived) and, where available, the parsed
     * error body (a JSON error payload or an ASP.NET Core ProblemDetails object).
     * `kind` is 'http', 'network', 'timeout' or 'session-expired'.
     */
    class ApiClientError extends Error {
        constructor(message, status, data, retryAfter, kind) {
            super(message);
            this.name = 'ApiClientError';
            this.status = status;
            this.data = data;
            this.retryAfter = retryAfter !== undefined ? retryAfter : null;
            this.kind = kind || 'http';
        }

        /** True when the user has to sign in again; a toast offering that is already showing. */
        get sessionExpired() {
            return this.kind === 'session-expired';
        }
    }

    /**
     * Plain-language fallback for a status whose body carries no message, so pages never
     * show "HTTP 500" or "undefined".
     */
    function statusMessage(status) {
        if (status === 400) return 'The request was not valid. Check the form and try again.';
        if (status === 401) return SESSION_EXPIRED_MESSAGE;
        if (status === 403) return 'You do not have permission to do that.';
        if (status === 404) return 'That item no longer exists. Reload the page to see the latest.';
        if (status === 409) return 'Someone else changed this. Reload the page and try again.';
        if (status === 413) return 'That file is too large.';
        if (status === 429) return 'Too many requests. Wait a moment and try again.';
        if (status === 503) return 'The service is unavailable right now. Try again shortly.';
        if (status >= 500) return 'Something went wrong on the server. Try again.';
        return 'The request failed. Try again.';
    }

    /**
     * Parse a Retry-After response header, when present, into
     * `{ raw, seconds }` - `raw` is the untouched header string (needed by
     * callers that must handle the HTTP-date form of Retry-After), and
     * `seconds` is that value coerced to a number when it parses as one
     * (delay-in-seconds is the common case for rate-limit responses in this
     * app), or `null` when it doesn't (e.g. an HTTP-date). Returns `null`
     * (not an object) when the header is absent altogether.
     */
    function parseRetryAfter(response) {
        if (!response || typeof response.headers?.get !== 'function') return null;
        const raw = response.headers.get('Retry-After');
        if (!raw) return null;
        const seconds = Number(raw);
        return { raw, seconds: Number.isFinite(seconds) ? seconds : null };
    }

    /**
     * Locate the anti-forgery token the same way the existing page scripts do:
     * a hidden `<input name="__RequestVerificationToken">` somewhere on the page.
     * Returns null if none is present (e.g. anonymous pages).
     */
    function getAntiForgeryToken() {
        if (typeof document === 'undefined') return null;
        const input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : null;
    }

    /**
     * Best-effort extraction of a human-readable message from an error body.
     * Supports the ad-hoc `{ success, message }` / `{ errors: [...] }` shape
     * used by this app's Razor Pages handlers as well as ASP.NET Core
     * ProblemDetails (`{ title, detail, errors }`). For a client error (4xx)
     * `detail` wins over `message`, because it says what to fix ("Hours must be
     * between 1 and 720"). For a server error (5xx) `detail` is skipped: several
     * controllers put exception text there, and users never see that.
     * `title` is the last resort. An HTML body (an error page) is never used.
     *
     * @param {*} data - the parsed error body
     * @param {string} fallback - used when the body carries nothing usable
     * @param {number} [status] - the HTTP status, when known
     */
    function extractErrorMessage(data, fallback, status) {
        if (!data) return fallback;
        if (typeof data === 'string') {
            return (data && !looksLikeHtml(data) && !(status >= 500)) ? data : fallback;
        }
        if (data.detail && !(status >= 500)) return data.detail;
        if (data.message) return data.message;
        if (Array.isArray(data.errors) && data.errors.length) return data.errors.join(', ');
        if (data.errors && typeof data.errors === 'object') {
            const messages = Object.values(data.errors).flat();
            if (messages.length) return messages.join(', ');
        }
        if (data.title && !(status >= 500)) return data.title;
        return fallback;
    }

    function looksLikeHtml(text) {
        return /^\s*<(!doctype|html|head|body|div|p|!--)/i.test(text);
    }

    function isHtmlResponse(response, text) {
        const type = response && response.headers && typeof response.headers.get === 'function'
            ? (response.headers.get('Content-Type') || '')
            : '';
        return type.includes('text/html') || (!!text && looksLikeHtml(text));
    }

    /**
     * True when the response is the sign-in page: either the server said 401, or a cookie
     * redirect landed on the login page (servers older than the 401 JSON change, or a
     * proxy in between).
     */
    function isSessionExpired(response) {
        if (response.status === 401) return true;
        if (!response.redirected || !response.url) return false;
        try {
            const path = new URL(response.url, 'http://localhost').pathname;
            return path.toLowerCase().startsWith(LOGIN_PATH.toLowerCase());
        } catch {
            return false;
        }
    }

    /**
     * Show the "sign in again" toast, once however many requests fail together.
     */
    function notifySessionExpired() {
        if (typeof window === 'undefined') return;
        const toastApi = window.toast;
        if (!toastApi || typeof toastApi.error !== 'function') return;
        toastApi.error(SESSION_EXPIRED_MESSAGE, {
            key: 'session-expired',
            action: {
                label: 'Sign in',
                onClick: () => {
                    const here = window.location.pathname + window.location.search;
                    window.location.href = `${LOGIN_PATH}?ReturnUrl=${encodeURIComponent(here)}`;
                }
            }
        });
    }

    /**
     * Parse a body as JSON, tolerating empty/non-JSON bodies
     * (e.g. 204 No Content, or handlers that return nothing).
     */
    function parseText(text) {
        if (!text) return null;
        try {
            return JSON.parse(text);
        } catch {
            return text;
        }
    }

    /**
     * Core request helper. Never throws for HTTP error statuses -
     * it always resolves with { ok, status, data }, matching the
     * `response.ok && data.success` pattern used throughout the app.
     * Network failures and timeouts reject with an ApiClientError
     * (status 0, kind 'network' or 'timeout') carrying a plain-language message.
     *
     * HTML is never returned as data: an expired session (a 401, or a redirect to the
     * sign-in page) resolves with ok:false, status 401, a `{ message }` body and
     * `sessionExpired: true`, and shows the "sign in again" toast; any other HTML body
     * (an error page) is replaced by `{ message }` for its status.
     *
     * @param {string} url
     * @param {object} [options]
     * @param {string} [options.method='GET']
     * @param {object|FormData|null} [options.body] - plain objects are JSON-serialized;
     *   FormData is sent as-is (browser sets the multipart Content-Type/boundary).
     * @param {object} [options.headers]
     * @param {boolean} [options.token=true] - inject the RequestVerificationToken header.
     * @param {string} [options.credentials='same-origin']
     * @param {number} [options.timeout=30000] - ms before the request is aborted; 0 for none.
     * @param {AbortSignal} [options.signal] - caller's own abort signal (e.g. a newer search).
     *   An abort through it rejects with the browser's AbortError, untouched.
     * @param {string} [options.redirect='follow'] - 'manual' to stop at a redirect instead of
     *   following it. Following runs the redirect target's GET, which consumes TempData
     *   (toasts, one-time values) meant for the page the user lands on next. A manual redirect
     *   resolves with `{ ok: true, redirected: true, status: 0, data: null }`: the handler
     *   finished, and the caller navigates itself (see quickActions' confirm forms).
     * @param {string} [options.responseType] - 'blob' to read the body as a Blob
     *   (audio synthesize/preview endpoints) instead of JSON/text. Only applied
     *   when the response is ok; error bodies are always parsed as JSON/text so
     *   error messages can still be extracted.
     */
    async function requestRaw(url, options = {}) {
        const {
            method = 'GET',
            body,
            headers = {},
            token = true,
            credentials = 'same-origin',
            timeout = DEFAULT_TIMEOUT_MS,
            signal,
            responseType,
            redirect
        } = options;

        // Mark the request as script-initiated, so the server answers an expired
        // session with 401 JSON instead of redirecting to the sign-in page.
        const finalHeaders = Object.assign({
            'X-Requested-With': 'XMLHttpRequest',
            'Accept': responseType === 'blob' ? '*/*' : 'application/json'
        }, headers);
        let finalBody = body;

        const isFormData = typeof FormData !== 'undefined' && body instanceof FormData;
        if (body !== undefined && body !== null && !isFormData && typeof body !== 'string') {
            finalBody = JSON.stringify(body);
            if (!finalHeaders['Content-Type']) {
                finalHeaders['Content-Type'] = 'application/json';
            }
        }

        if (token) {
            const tokenValue = getAntiForgeryToken();
            if (tokenValue) {
                finalHeaders['RequestVerificationToken'] = tokenValue;
            }
        }

        const controller = typeof AbortController !== 'undefined' ? new AbortController() : null;
        let timedOut = false;
        let timer = null;
        if (controller && timeout > 0) {
            timer = setTimeout(() => { timedOut = true; controller.abort(); }, timeout);
        }
        let onCallerAbort = null;
        if (controller && signal) {
            if (signal.aborted) controller.abort();
            onCallerAbort = () => controller.abort();
            signal.addEventListener('abort', onCallerAbort);
        }

        try {
            let response;
            try {
                response = await fetch(url, {
                    method,
                    headers: finalHeaders,
                    body: finalBody,
                    credentials,
                    redirect: redirect || 'follow',
                    signal: controller ? controller.signal : signal
                });
            } catch (err) {
                if (timedOut) throw new ApiClientError(TIMEOUT_MESSAGE, 0, null, null, 'timeout');
                if (err && err.name === 'AbortError') throw err;
                throw new ApiClientError(NETWORK_MESSAGE, 0, null, null, 'network');
            }

            // redirect: 'manual' answers a redirect with an opaque, empty response
            if (response.type === 'opaqueredirect') {
                return { ok: true, status: 0, redirected: true, data: null, response };
            }

            if (isSessionExpired(response)) {
                notifySessionExpired();
                return {
                    ok: false,
                    status: 401,
                    data: { success: false, message: SESSION_EXPIRED_MESSAGE },
                    response,
                    sessionExpired: true
                };
            }

            if (responseType === 'blob' && response.ok) {
                return { ok: true, status: response.status, data: await response.blob(), response };
            }

            let text;
            try {
                text = await response.text();
            } catch (err) {
                if (timedOut) throw new ApiClientError(TIMEOUT_MESSAGE, 0, null, null, 'timeout');
                if (err && err.name === 'AbortError') throw err;
                throw new ApiClientError(NETWORK_MESSAGE, 0, null, null, 'network');
            }

            if (isHtmlResponse(response, text)) {
                // A page, not data: an error page or a misrouted URL
                const message = response.ok ? 'The server sent an unexpected response. Reload the page and try again.' : statusMessage(response.status);
                return { ok: false, status: response.ok ? 500 : response.status, data: { success: false, message }, response };
            }

            return { ok: response.ok, status: response.status, data: parseText(text), response };
        } finally {
            if (timer) clearTimeout(timer);
            if (onCallerAbort) signal.removeEventListener('abort', onCallerAbort);
        }
    }

    /**
     * Same as requestRaw, but throws ApiClientError for non-2xx responses.
     *
     * @param {object} [options.errorMessage] - fallback error message to use
     *   when the response body carries no message/detail/errors/title (mirrors
     *   the per-call-site fallback strings pages used to hard-code around raw
     *   fetch() calls, e.g. 'Failed to send message').
     */
    async function request(url, options = {}) {
        const { ok, status, data, response, sessionExpired } = await requestRaw(url, options);
        if (!ok) {
            if (sessionExpired) {
                throw new ApiClientError(SESSION_EXPIRED_MESSAGE, 401, data, null, 'session-expired');
            }
            const fallback = options.errorMessage || statusMessage(status);
            const message = extractErrorMessage(data, fallback, status);
            const retryAfter = status === 429 ? parseRetryAfter(response) : null;
            throw new ApiClientError(message, status, data, retryAfter);
        }
        return data;
    }

    function get(url, options = {}) {
        return request(url, Object.assign({}, options, { method: 'GET' }));
    }
    function post(url, body, options = {}) {
        return request(url, Object.assign({}, options, { method: 'POST', body }));
    }
    function put(url, body, options = {}) {
        return request(url, Object.assign({}, options, { method: 'PUT', body }));
    }
    function del(url, options = {}) {
        return request(url, Object.assign({}, options, { method: 'DELETE' }));
    }

    function getRaw(url, options = {}) {
        return requestRaw(url, Object.assign({}, options, { method: 'GET' }));
    }
    function postRaw(url, body, options = {}) {
        return requestRaw(url, Object.assign({}, options, { method: 'POST', body }));
    }
    function putRaw(url, body, options = {}) {
        return requestRaw(url, Object.assign({}, options, { method: 'PUT', body }));
    }
    function delRaw(url, options = {}) {
        return requestRaw(url, Object.assign({}, options, { method: 'DELETE' }));
    }

    /**
     * Show an error toast for a failed request. Pass the caught error or a message.
     * An expired session already has its own toast, so it is not shown twice.
     */
    function showErrorToast(errorOrMessage) {
        if (errorOrMessage instanceof ApiClientError && errorOrMessage.sessionExpired) return;
        const message = errorOrMessage instanceof Error
            ? (errorOrMessage instanceof ApiClientError ? errorOrMessage.message : 'Something went wrong. Try again.')
            : errorOrMessage;
        if (typeof window !== 'undefined' && window.toast && typeof window.toast.error === 'function') {
            window.toast.error(message);
            return;
        }
        console.error(message);
    }

    /**
     * About 30 page scripts still call fetch() directly. Watch same-origin responses so an
     * expired session shows the same "sign in again" toast there too. The response itself
     * is passed through untouched.
     */
    function watchFetchForExpiredSession() {
        if (typeof window === 'undefined' || typeof window.fetch !== 'function' || window.fetch.__sessionWatch) return;
        const nativeFetch = window.fetch.bind(window);
        const watched = function (input, init) {
            return nativeFetch(input, init).then(response => {
                try {
                    const url = new URL(response.url || (typeof input === 'string' ? input : input.url), window.location.href);
                    if (url.origin === window.location.origin && isSessionExpired(response)) {
                        notifySessionExpired();
                    }
                } catch {
                    // Never let the watcher break a request
                }
                return response;
            });
        };
        watched.__sessionWatch = true;
        window.fetch = watched;
    }

    watchFetchForExpiredSession();

    return {
        ApiClientError,
        getAntiForgeryToken,
        extractErrorMessage,
        statusMessage,
        parseRetryAfter,
        request,
        requestRaw,
        get,
        post,
        put,
        del,
        getRaw,
        postRaw,
        putRaw,
        delRaw,
        showErrorToast
    };
});

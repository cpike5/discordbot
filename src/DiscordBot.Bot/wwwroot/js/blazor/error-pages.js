/**
 * Classic script (not an ES module - no IJSObjectReference wrapper needed) for the three static
 * SSR error pages (Blazor/Pages/Error/{Forbidden,NotFound,ServerError}.razor). Ports the cshtml
 * error pages' inline onclick="history.back()" / onclick="location.reload()" handlers as a single
 * document-level delegated click listener instead, keyed off data-error-action="back|reload" -
 * inline event-handler attributes would be blocked by the script-src 'self' CSP (no
 * 'unsafe-inline') planned for Phase 6 (docs/plans/blazor-port-plan.md §5 Phase 6 "Hardening"),
 * while a delegated listener on an external script needs no per-element inline attribute.
 *
 * Referenced via <HeadContent><script src="/js/blazor/error-pages.js"></script></HeadContent> on
 * each error page. Initializes on both DOMContentLoaded (first, full-page load) and Blazor's
 * enhanced-navigation "enhancedload" event (arriving at an error page via a client-side route
 * change from another Blazor page) - see landing.js for the same pattern with more detail.
 * Idempotent: the bound flag on document.body means a second init() call (e.g. DOMContentLoaded
 * firing after this script's own synchronous run because it loaded before the parser reached
 * <body>) never attaches a second listener.
 */
(function () {
    function handleClick(event) {
        var target = event.target.closest('[data-error-action]');
        if (!target) {
            return;
        }

        var action = target.getAttribute('data-error-action');
        if (action === 'back') {
            history.back();
        } else if (action === 'reload') {
            location.reload();
        }
    }

    function init() {
        if (!document.body || document.body.dataset.errorPagesJsBound === 'true') {
            return;
        }

        document.body.addEventListener('click', handleClick);
        document.body.dataset.errorPagesJsBound = 'true';
    }

    document.addEventListener('DOMContentLoaded', init);
    if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
        window.Blazor.addEventListener('enhancedload', init);
    }
    // The script tag can execute after the parser has already reached <body> (it's injected via
    // <HeadContent>, not a fixed spot in the static markup), in which case DOMContentLoaded has
    // already fired and will never fire again - cover that case directly.
    if (document.readyState !== 'loading') {
        init();
    }
})();

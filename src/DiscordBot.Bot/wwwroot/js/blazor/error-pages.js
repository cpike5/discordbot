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
 * change from another Blazor page) - see landing.js for the same pattern with more detail,
 * including why the enhancedload hook can only be registered from inside init() (this script runs
 * in <head>, before blazor.web.js at the end of <body> has defined window.Blazor - registering it
 * at the top level here would silently never fire).
 * Idempotent: the bound flag on document.body means a second init() call (e.g. DOMContentLoaded
 * firing after this script's own synchronous run because it loaded before the parser reached
 * <body>) never attaches a second click listener, and a module-level flag keeps the enhancedload
 * hook itself from being registered more than once.
 */
(function () {
    var enhancedLoadHooked = false;

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

    function hookEnhancedLoad() {
        if (!enhancedLoadHooked && window.Blazor && typeof window.Blazor.addEventListener === 'function') {
            window.Blazor.addEventListener('enhancedload', init);
            enhancedLoadHooked = true;
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        init();
        // By DOMContentLoaded time blazor.web.js (end of <body>) has already run synchronously
        // during parsing, so window.Blazor is defined here even though it wasn't when this
        // script's own top-level code ran in <head>.
        hookEnhancedLoad();
    });
    // The script tag can execute after the parser has already reached <body> (it's injected via
    // <HeadContent>, not a fixed spot in the static markup), in which case DOMContentLoaded has
    // already fired and will never fire again - cover that case directly.
    if (document.readyState !== 'loading') {
        init();
        hookEnhancedLoad();
    }
})();

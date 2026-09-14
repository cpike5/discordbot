/**
 * Classic script (not an ES module) for the public landing page (Blazor/Pages/Landing.razor).
 * Ports the two inline <script> blocks from the old Pages/Landing.cshtml's @section Scripts
 * verbatim: the hero background parallax-on-scroll effect and the scroll-spy that highlights the
 * matching nav-link for whichever section is currently in view.
 *
 * Referenced via <HeadContent><script src="/js/blazor/landing.js"></script></HeadContent> on
 * Landing.razor. Runs on DOMContentLoaded (first, full-page load) and on Blazor's
 * enhanced-navigation "enhancedload" event, which fires after a client-side route change swaps in
 * new DOM (e.g. a Blazor page's link to /landing) without a full reload - DOMContentLoaded does
 * not fire again for that case, so the listeners here would otherwise never attach.
 *
 * Idempotent by design rather than by a guard flag: init() is safe to call any number of times
 * because everything it does either (a) is a pure function of the *current* DOM, re-run from
 * scratch each call (updateActiveNav), or (b) is guarded so it only ever happens once for the
 * life of the page (the window scroll listener itself, via the module-level `scrollBound` flag -
 * window is not torn down by enhanced navigation, so re-attaching on every enhancedload would
 * stack duplicate listeners; the handler always re-queries the DOM for the current .hero-bg /
 * section elements, so one listener is enough for the page's entire lifetime regardless of how
 * many times enhanced navigation swaps the body content under it).
 *
 * Sets [data-landing-page]'s data-landing-js-ready="true" once init has run against the current
 * DOM - a hook for tests/DiscordBot.E2E (Test_F) to assert this script actually executed, since
 * the parallax/scroll-spy effects it drives have no other easily-assertable signal.
 */
(function () {
    var scrollBound = false;

    function handleScroll() {
        if (!window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
            var scrolled = window.pageYOffset;
            var hero = document.querySelector('.hero-bg');
            if (hero && scrolled < 600) {
                hero.style.transform = 'translateY(' + (scrolled * 0.3) + 'px)';
                hero.style.opacity = String(1 - (scrolled / 600));
            }
        }

        updateActiveNav();
    }

    function updateActiveNav() {
        var scrollY = window.pageYOffset;

        document.querySelectorAll('section[id]').forEach(function (section) {
            var sectionTop = section.offsetTop - 100;
            var sectionHeight = section.offsetHeight;
            var sectionId = section.getAttribute('id');
            var inSection = scrollY >= sectionTop && scrollY < sectionTop + sectionHeight;

            document.querySelectorAll('.nav-link').forEach(function (link) {
                var isThisSectionsLink = link.getAttribute('href') === '#' + sectionId;
                if (inSection) {
                    link.classList.toggle('active', isThisSectionsLink);
                }
            });
        });
    }

    function init() {
        var page = document.querySelector('[data-landing-page]');
        if (!page) {
            // Not on the landing page (enhanced navigation moved elsewhere) - nothing to do.
            return;
        }

        if (!scrollBound) {
            window.addEventListener('scroll', handleScroll, { passive: true });
            scrollBound = true;
        }

        updateActiveNav();
        page.dataset.landingJsReady = 'true';
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

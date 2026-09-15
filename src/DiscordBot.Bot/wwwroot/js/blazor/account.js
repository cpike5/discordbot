/**
 * Password-visibility toggle for Blazor/Pages/Account/Login.razor (docs/plans/blazor-port-plan.md
 * Phase 4 cluster 4c) - the one piece of wwwroot/js/login.js that survives the port. The
 * per-keystroke blur validation login.js also did is dropped; DataAnnotationsValidator's
 * server-rendered messages are enough for a static SSR page (see "Static-SSR account pages" in
 * docs/architecture/patterns.md).
 *
 * Classic script (not an ES module), loaded via <HeadContent> from Login.razor itself rather than
 * from App.razor - this page never goes @rendermode interactive, so there is no IJSRuntime to
 * import a module through, and no other page needs this behaviour. One delegated `click` listener
 * on `document`, keyed off `data-account-action="toggle-password"` (no inline onclick= - Phase 6
 * adds a CSP), guarded the same way wwwroot/js/blazor/shell.js guards its own document-level
 * listener against double registration if the page's <head> content is ever re-evaluated.
 */
(function () {
    if (window.__discordBotAccountInitialized) {
        return;
    }
    window.__discordBotAccountInitialized = true;

    document.addEventListener('click', function (event) {
        var button = event.target.closest('[data-account-action="toggle-password"]');
        if (!button) {
            return;
        }

        var input = button.parentElement ? button.parentElement.querySelector('input') : null;
        var showIcon = button.querySelector('.password-show');
        var hideIcon = button.querySelector('.password-hide');
        if (!input) {
            return;
        }

        if (input.type === 'password') {
            input.type = 'text';
            if (showIcon) showIcon.classList.add('hidden');
            if (hideIcon) hideIcon.classList.remove('hidden');
            button.setAttribute('aria-label', 'Hide password');
            button.setAttribute('aria-pressed', 'true');
        } else {
            input.type = 'password';
            if (showIcon) showIcon.classList.remove('hidden');
            if (hideIcon) hideIcon.classList.add('hidden');
            button.setAttribute('aria-label', 'Show password');
            button.setAttribute('aria-pressed', 'false');
        }
    });
})();

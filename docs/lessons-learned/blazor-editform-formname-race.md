# EditForm's FormName routes a premature submit into a broken static fallback

**Symptom.** `Admin/Users/Create.razor`'s `<EditForm>` (`@rendermode InteractiveServer`,
`OnValidSubmit`, `DataAnnotationsValidator`) worked in every bUnit test but consistently failed
end to end under Playwright: clicking "Create User" right after filling the form left the browser
on the same page with no navigation, no toast, and no server-side log line at all from
`HandleValidSubmit` - as if the click had no effect. A retry-click loop made it *worse*, sometimes
surfacing a real server error ("A valid antiforgery token was not provided with the request." or
"The POST request does not specify which form is being submitted.").

**Cause.** A page rendered with `@rendermode InteractiveServer` is still **prerendered as static
HTML first**; the Interactive Server circuit attaches afterward, and that attach is not
instantaneous - Blazor Web App documents no reliable client-side signal for "the circuit has
attached to this DOM region" (the same gap `tests/DiscordBot.E2E/BrowserTests.cs`'s
`AssertCounterIncrementsAsync` already works around for a plain `@onclick` button). Setting
`FormName` on an `EditForm` opts that form into Blazor Web App's **named static form-post
fallback** - the mechanism that lets a form submit correctly even during that narrow prerender
window, via a real (non-circuit) HTTP POST handled statically. A submit that lands in that window
is routed into the fallback whether or not the page actually implements it. Neither
`Create.razor`/`Edit.razor` implemented it (no `<AntiforgeryToken />`, no
`[SupplyParameterFromForm]` on the bound model), so a submit that hit the window failed instead of
reaching `OnValidSubmit` - the antiforgery/form-name errors above are that fallback failing, not
the interactive path. **bUnit never exercises any of this**: it invokes `OnValidSubmit` directly
against the component instance and never touches the real `<form>`/JS/HTTP path a browser does, so
this class of bug is invisible to it entirely - it only surfaces against a real host.

**Fix.** Drop `FormName` from an `EditForm` that has no legitimate no-JS/static-fallback use case
(this admin console has none). Without it, a submit that lands before the circuit attaches is a
harmless no-op - the user (or, in a test, a retry) just submits again once the circuit is live,
instead of hitting a static handler that was never wired to succeed. The alternative - actually
implementing the static fallback (`<AntiforgeryToken />` + `[SupplyParameterFromForm]` on the
model, `[CascadingParameter] HttpContext` to distinguish a real postback from a fresh load so a
page like `Edit.razor` that also reloads its model from the database on init doesn't clobber
form-supplied values) - is the framework-documented alternative and may be worth it for a page
with real anonymous/no-JS traffic, but is meaningfully more code for no benefit here.

**Rule that falls out of it.** For any `EditForm` on an `@rendermode InteractiveServer` page in
this codebase: do not set `FormName` unless the static-fallback path is deliberately implemented
end to end (antiforgery token, `[SupplyParameterFromForm]`, and - if the page has other
`OnInitializedAsync` work - a way to tell a real postback apart from a fresh load). The
`anthropic-skills` "blazor" skill's own `03-forms-validation.md` documents `FormName` as
"**Required** unique name for SSR form handling" and lists `[SupplyParameterFromForm]` as one of
the "SSR Form Requirements" - both true, but only for a form meant to work that way; an
interactive-only form (the norm in this admin console) is simpler and more robust without either.

**Test-side consequence.** An E2E test driving an `EditForm` submit (or any other first
interaction on a route Playwright just navigated to, including a plain `@onclick` button - the
toggle-active button in `Blazor/Pages/Admin/Users/Index.razor` needed the same treatment) needs an
explicit settle wait before that first interaction, since there is no DOM signal to poll instead.
See `Test_R_Users_CreateEditDetails_RoundTrip` in `tests/DiscordBot.E2E/BrowserTests.cs` for the
pattern (and its own comments) - one `page.WaitForTimeoutAsync(1_500)` per freshly-navigated page,
right before its first click.

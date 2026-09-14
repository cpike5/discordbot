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

**What a premature submit actually sends (verified, not assumed).** An earlier version of this
note claimed dropping `FormName` makes a pre-circuit submit "a harmless no-op" because, without
`method="post"`, the browser would fall back to a **GET** that puts the password fields in the
URL/history/logs. That specific claim is wrong, and was never checked against the real markup.
Booting the host web-only (`Discord__Enabled=false`, matching `BotHostFixture`) and curling the
authenticated, prerendered `/Admin/Users/Create` page shows `EditForm` always renders a real
`method="post"` form regardless of `FormName`, plus (independently of `FormName`) its own
antiforgery hidden input:

```html
<!-- WITHOUT FormName (this repo's actual code) -->
<form method="post" class="p-6 space-y-6" action="/Admin/Users/Create">
  <input type="hidden" name="__RequestVerificationToken" value="..." />
  ...
  <input type="password" id="Input_Password" name="Input_Password" ... />

<!-- WITH FormName="CreateUserRepro" (reproduced only for this comparison, not shipped) -->
<form method="post" class="p-6 space-y-6" action="/Admin/Users/Create">
  <input type="hidden" name="__RequestVerificationToken" value="..." />
  <input type="hidden" name="_handler" value="CreateUserRepro" />
  ...
```

The only difference `FormName` makes to the emitted markup is the extra hidden `_handler` field
that tells the server which named static handler to route the POST to. So a pre-circuit submit is
a real `POST` either way - the password travels in the request body, not the URL, and never reaches
browser history or a URL-keyed log. **Without `FormName`/`_handler` the server has nothing to route
the POST to** (no `[SupplyParameterFromForm]` model is listening for it), so the request is
processed like any other request to that route: the component boots fresh and prerenders again,
discarding the submitted values. That is the real, verified shape of "harmless no-op" - a wasted
POST and a reset form, not a data leak and not the antiforgery/`_handler` errors from the Symptom
section (those came specifically from `FormName` routing the request into a fallback path that was
never wired to succeed).

**Fix.** Drop `FormName` from an `EditForm` that has no legitimate no-JS/static-fallback use case
(this admin console has none) - `Create.razor`/`Edit.razor` already do this. `method="post"` needs
no separate fix; `EditForm` emits it unconditionally, `FormName` or not, as shown above. The
remaining gap is UX/robustness, not correctness or security: a user who submits in the prerender
window gets a silently reset form instead of an error, which is confusing even though nothing is
lost or leaked. Closed by disabling the submit button (and, on `AuditLogs/Details.razor`, the
Export JSON button) until `ComponentBase.RendererInfo.IsInteractive` is `true` - that flips from
`false` (prerendered/static) to `true` once the interactive circuit has actually attached and
re-rendered the component, so it is a real readiness signal available in `.razor` markup with no
extra state (`RendererInfo` is inherited from `ComponentBase`; confirmed present and working in
this repo's SDK, .NET 10). A short "Connecting…" hint sits next to the disabled button. The
alternative - actually implementing the static fallback (`<AntiforgeryToken />` +
`[SupplyParameterFromForm]` on the model, `[CascadingParameter] HttpContext` to distinguish a real
postback from a fresh load so a page like `Edit.razor` that also reloads its model from the
database on init doesn't clobber form-supplied values) - is the framework-documented alternative
and may be worth it for a page with real anonymous/no-JS traffic, but is meaningfully more code for
no benefit here.

**Rule that falls out of it.** For any `EditForm` on an `@rendermode InteractiveServer` page in
this codebase:

1. No `FormName` unless the static-fallback path is deliberately implemented end to end
   (antiforgery token, `[SupplyParameterFromForm]`, and - if the page has other
   `OnInitializedAsync` work - a way to tell a real postback apart from a fresh load). The
   `anthropic-skills` "blazor" skill's own `03-forms-validation.md` documents `FormName` as
   "**Required** unique name for SSR form handling" and lists `[SupplyParameterFromForm]` as one
   of the "SSR Form Requirements" - both true, but only for a form meant to work that way; an
   interactive-only form (the norm in this admin console) is simpler and more robust without
   either.
2. `method="post"` needs no action - `EditForm` always emits it. (Do not add an explicit
   `method="post"` attribute of your own to work around a "defaults to GET" theory; it doesn't,
   and a hand-added one would just duplicate what the framework already renders.)
3. Disable the submit button (`disabled="@(!RendererInfo.IsInteractive)"`) with a brief
   "Connecting…" hint, so a click in the prerender window is prevented rather than silently
   discarded. Apply the same pattern to any other first-interaction control on a freshly rendered
   interactive page (a plain `@onclick` button has the identical race - see `AuditLogs/Details.razor`'s
   Export JSON button).

This rule does **not** apply to `Profile.razor`: that page is static SSR by design (no
`@rendermode`), so its `<form>` is a real, always-working static POST with no circuit-attach window
to race.

**Test-side consequence.** An E2E test driving an `EditForm` submit (or any other first
interaction on a route Playwright just navigated to) now has a real DOM signal to wait on instead
of a blind sleep: `Expect(button).ToBeEnabledAsync()` on the submit/action button, which only
becomes enabled once `RendererInfo.IsInteractive` flips. `Test_R_Users_CreateEditDetails_RoundTrip`
and `Test_T_AuditLogDetails_ExportDownloadsJson` in `tests/DiscordBot.E2E/BrowserTests.cs` use this
instead of the earlier `page.WaitForTimeoutAsync(1_500)` per freshly-navigated page.

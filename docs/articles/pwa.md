# Progressive Web App

The admin portal is installable as a Progressive Web App: Chrome, Edge, and Android offer
"Install app", and iOS Safari can "Add to Home Screen". Installed, it opens in its own window
without browser chrome, and shows an offline page instead of a browser error when the bot
cannot be reached.

## Pieces

| File | Purpose |
| --- | --- |
| `wwwroot/manifest.webmanifest` | Name, colours, `start_url` (`/`), `display: standalone`, icons. |
| `wwwroot/sw.js` | The service worker, served from the root so its scope is the whole site. |
| `wwwroot/offline.html` | Self-contained page shown when a navigation fails. Inline styles only. |
| `wwwroot/js/pwa.js` | Registers the service worker (secure contexts only: HTTPS or `localhost`). |
| `wwwroot/images/icons/` | PNG icons: 192/512 `any`, 192/512 `maskable`, and the 180px `apple-touch-icon`. |
| `Pages/Shared/_PwaHead.cshtml` | Head partial with the manifest link, Apple meta tags, and `pwa.js`. |

`_PwaHead` is included by `_Layout`, `Portal/_PortalLayout`, `_LayoutLanding`, and the
standalone `Account/Login` page. A new `Layout = null` page that should stay installable
adds `<partial name="_PwaHead" />` to its `<head>`.

## Caching policy

The service worker is deliberately conservative. Pages and API data are per-user and
authenticated, and a stale guild or moderation view is worse than none, so:

- **Pages** (navigations) always go to the network. Only if that fails is `offline.html`
  served from the precache. Pages are never cached.
- **Static assets** under `/css/`, `/js/`, `/images/`, `/lib/` are cached at runtime:
  cache-first when the URL carries `asp-append-version`'s `?v=` hash (the URL changes when the
  file does), stale-while-revalidate otherwise. The runtime cache is capped at 150 entries.
  Redirected or non-OK responses are never stored.
- **Bypassed entirely:** non-GET requests, cross-origin requests (Google Fonts, Discord CDN),
  `/api/`, `/hubs/` (SignalR), `/Account/`, `/signin-*`, `/health`, `/metrics`, `/swagger`,
  `/exports/`.

The routing decision is the pure `classifyRequest` function in `sw.js`, unit tested in
`wwwroot/js/__tests__/sw.test.js` (`npm test` in `src/DiscordBot.Bot`).

## Changing it

- Bump `CACHE_VERSION` in `sw.js` when the precache list or caching behaviour changes. On
  activation the new worker deletes every `discordbot-*` cache that is not its own.
- Browsers check `/sw.js` for changes on navigation and bypass the HTTP cache for it, so a
  deploy is picked up on the next visit; the new worker takes over immediately
  (`skipWaiting` + `clients.claim`).
- Icons are rendered from `images/logo.svg`. To regenerate after a logo change, render the SVG
  at each size (maskable ones on `#0f1114` with the logo at 60% so it survives the mask).

## Root-level static files and routing

`/sw.js` and `/manifest.webmanifest` must be served from the site root. Static files run after
routing, and the static file middleware steps aside whenever routing already matched an
endpoint, even a 405 one. So a controller route that matches a bare single segment at the root
(for example `[HttpDelete("{name}")]` on an action that also has its own `[Route(...)]`,
which registers a second, unprefixed route) makes every root file unreachable and redirects it
to the login page. `ControllerRoutingConventionTests` guards against that shape.

## Deployment

Service workers need a secure context. Behind nginx/Cloudflare with HTTPS this just works;
over plain HTTP on anything but `localhost`, `pwa.js` does nothing and the site behaves as
before.

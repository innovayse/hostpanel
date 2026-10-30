# Storefront templates and branding

How the public client portal picks a design, how to add one, and which settings brand it.

The public portal ships three designs and renders whichever one is selected. All
produce identical SEO output — canonical, hreflang and schema.org live in
`client/pages/`, never in a template.

Set the site's own origin, or the sitemap will advertise whatever host each
request happened to arrive on — behind a proxy that can be the container's
internal address. `NUXT_PUBLIC_BASE_URL` covers it at build time; a prebuilt
image reads `NUXT_SITE_URL` at startup, which overrides the built-in value.

| Template | Description |
|---|---|
| `aurora` | Default. Dark and light modes, Armenian typography, live domain search |
| `nova` | Conversion-focused hosting landing page: pricing, plan comparison, security, migration, FAQ |
| `classic` | The original storefront design |

`nova` supplies its own header, footer, homepage and hosting page, and reuses
aurora's domain search and checkout — the checkout is the ordering path, and a
second copy of it would be a second place for a payment bug to live. Its design
tokens are prefixed `--n-*` and declared only under `.tpl-nova` and
`html[data-template='nova']`, so activating it cannot disturb the other two.

Several of its cards state things this codebase cannot verify — NVMe storage, a
CDN, server-side caching, a firewall, a malware scanner, a backup schedule, an
uptime figure, free migration. Each waits on a setting and is hidden until an
operator turns it on:

| Setting | Shows |
|---|---|
| `portal.features.storage` | the SSD/NVMe storage card |
| `portal.features.caching` | the server-side caching card |
| `portal.features.cdn` | the CDN card |
| `portal.features.firewall` | the firewall card |
| `portal.features.malware` | the malware-protection card |
| `portal.features.backups` | the backup cards, in the hero, trust bar and security grid |
| `portal.trust.uptime` | the uptime figure in the trust bar, printed verbatim |
| `portal.migration.free` | the "free migration" badge, when set to `true` |

Any non-empty value turns the first six on; the uptime setting holds the figure
itself. The comparison table and the testimonials section are data-driven the
same way: the table renders only the specification lines entered under
**Admin → Products → Specification**, and the testimonials section does not
render at all, because nothing serves customer quotes yet.

**Choosing one.** In order of precedence:

1. The `portal.template` setting, editable in **Admin → Settings → Portal appearance**
2. The `NUXT_PUBLIC_PORTAL_TEMPLATE` environment variable
3. `classic` (`DEFAULT_TEMPLATE` in `client/templates/types.ts`)

An unrecognised value falls back to `classic` rather than leaving the site blank, so a
typo in the admin field cannot take the storefront down.

**Adding a template.** Create `client/templates/<name>/` with `layout/Header.vue`,
`layout/Footer.vue` and a `pages/` component for each route, then register the
loaders in `client/templates/registry.ts` and add the name to `TEMPLATE_NAMES` in
`client/templates/types.ts`. A unit test asserts every template implements every
slot, so a missing page fails the build rather than rendering nothing.

Two rules make templates safe to swap:

- **Templates render, pages decide.** A template component takes typed props and
  returns markup; the page above it owns fetching, SEO and business logic. That
  keeps both designs on one code path and one set of head tags.
- **Copy lives in i18n** under a top-level key named for the template
  (`aurora.hero.title`), registered in the `modules` array of
  `client/plugins/i18n.ts`. Locale files are not auto-discovered.

`aurora` follows the first rule throughout, with one exception: its
`pages/Checkout.vue` still carries the ordering flow, because splitting a working
payment path is only worth doing with a backend on hand to place a test order
against. It is `classic`'s script byte-for-byte with restyled markup, so the two
cannot drift apart.

`classic`'s pages are lift-and-shifts of the original route components and keep
their own data loading. That was the point: moving a 500-line page unchanged is
far safer than rewriting it, and the design it renders is the one being replaced.
New templates should follow `aurora`, not `classic`.

## Branding and colour mode

**Branding and colour mode**, all from **Admin → Settings** with a
`NUXT_PUBLIC_PORTAL_*` environment fallback for a first boot (the admin value
wins once set):

| Setting | Env fallback | What it does |
|---|---|---|
| `portal.site.name` | `NUXT_PUBLIC_PORTAL_SITE_NAME` | Site name for `og:site_name`, logo alt text and structured data. Empty shows the built-in name. |
| `portal.site.tagline` | `NUXT_PUBLIC_PORTAL_SITE_TAGLINE` | Default meta description. |
| `portal.theme.default` | `NUXT_PUBLIC_PORTAL_THEME_DEFAULT` | `light`, `dark` or `system` for a visitor with no saved choice. A visitor's own choice is kept in a `color-mode` cookie and rendered on the server, so there is no flash of the wrong theme. |
| `portal.theme.user_toggle` | `NUXT_PUBLIC_PORTAL_THEME_USER_TOGGLE` | `false` hides the light/dark switch. |
| `portal.logo` | `NUXT_PUBLIC_PORTAL_LOGO` | Logo for light backgrounds. |
| `portal.logo.dark` | `NUXT_PUBLIC_PORTAL_LOGO_DARK` | Logo for dark backgrounds; falls back to the light one. |
| `portal.logo.mark` | `NUXT_PUBLIC_PORTAL_LOGO_MARK` | Square icon-only logo for compact places. |
| `portal.favicon` | `NUXT_PUBLIC_PORTAL_FAVICON` | Browser tab icon; an upload generates the whole icon set. |
| `portal.brand.primary` | `NUXT_PUBLIC_PORTAL_BRAND_PRIMARY` | Brand colour as `#rrggbb`. The whole primary scale — buttons, links, highlights, e-mail buttons — is derived from it; the text on it turns dark automatically when the colour is too light for white. |
| `portal.brand.accent` | `NUXT_PUBLIC_PORTAL_BRAND_ACCENT` | Accent colour as `#rrggbb`; gradients, badges and secondary highlights. |
| `portal.brand.font_heading` | `NUXT_PUBLIC_PORTAL_BRAND_FONT_HEADING` | `inter`, `noto-sans-armenian`, `noto-serif-armenian` or `system`. Bundled; nothing loads from a font service. |
| `portal.brand.font_body` | `NUXT_PUBLIC_PORTAL_BRAND_FONT_BODY` | Same list, for body text. |

Logos and the favicon can be uploaded in **Admin → Settings → Branding** or pasted
as URLs. `portal.template`, `portal.theme.default` and the typefaces accept only their
listed values, and the colours only `#rrggbb`; anything else is refused with
`INVALID_SETTING_VALUE` and a sentence saying what was expected. With every brand
setting empty the storefront renders each template's own colours and typefaces,
exactly as before the settings existed.

**Analytics and chat** are settings too, empty by default, and nothing loads until
they are set:

| Setting | Env fallback | What it does |
|---|---|---|
| `portal.analytics.gtm_id` | `NUXT_PUBLIC_PORTAL_GTM_ID` | Google Tag Manager container (`GTM-…`). Loads only after a visitor accepts all cookies in the banner. |
| `portal.chat.provider` | `NUXT_PUBLIC_PORTAL_CHAT_PROVIDER` | `chatwoot`, `innochat`, or empty for no widget. |
| `portal.chat.base_url` | `NUXT_PUBLIC_PORTAL_CHAT_BASE_URL` | The chat server, `https://` only. |
| `portal.chat.website_token` | `NUXT_PUBLIC_PORTAL_CHAT_WEBSITE_TOKEN` | The website token from the chat inbox. `.ru` / `.hy` variants override it per language. |

**E-mails follow the brand.** Every template rendered by the API can use
`{{ site.name }}`, `{{ brand.primary }}`, `{{ brand.primary_dark }}`,
`{{ brand.on_primary }}`, `{{ brand.accent }}` and `{{ brand.logo_url }}` (the dark-background
logo as an absolute URL, empty when none is uploaded). The password-reset template shipped
with a fresh install uses them; an install that already has that row keeps its stored HTML
until it is re-saved in **Admin → Settings → Email Templates**.

Related operator settings, all optional and hidden when empty:
`portal.contact.whatsapp`, `portal.contact.telegram`, `portal.contact.email`,
`portal.contact.phone`, `portal.contact.address`, `portal.contact.map_url` (https only),
`portal.stats.clients`, `portal.stats.uptime`, `portal.stats.regions`, `portal.hero.badge`,
`portal.chat.provider`, `portal.newsletter.action_url`,
`portal.legal.tax_id`, `portal.social.{facebook,instagram,twitter,linkedin,youtube}`, and the
support hours `portal.contact.hours` with their zone `portal.contact.timezone`.

The email, phone, address, map, hours and zone are all editable in **Admin → Settings → Storefront**;
the email and the map address are checked when saved. The address and the hours can also be set
per language with the `.ru` and `.hy` variants (`portal.contact.address.ru`, `portal.contact.hours.hy`, and so on);
a language without its own value shows the default one.

The nova template states a hardware or commercial claim only when the operator switches it on
(`true`; empty or `false` hides it): `portal.features.{backups,storage,caching,cdn,firewall,malware}`
and `portal.migration.free`. The uptime line in its trust bar prints `portal.stats.uptime`.

The header app launcher is off unless `portal.apps.enabled` is `true`, because
the apps it links to only exist in a deployment that runs them. Each entry then
needs a URL of its own — `portal.apps.{account,tasks,erp,hostpanel,sheets,mail,docs,calendar}`,
or the matching `NUXT_PUBLIC_*` variable — and entries without one are left out.

Every seeded setting is editable in **Admin → Settings**; the table takes one row
at a time.

The plans page's comparison table is built from each product's specification
lines, edited in **Admin → Products → Specification**. Plans line up by feature
name, so the same line has to be spelled the same way across products; a plan
that omits a line shows a dash rather than shifting its column. With no product
specified, the table is not rendered at all.

# hostpanel client

The customer portal of hostpanel: a Nuxt 4 application with a public, server-rendered storefront
(three templates: `aurora`, `nova`, `classic`) and a session-gated client area under `/client/**`.
It talks only to its own Nitro routes (`server/api/portal/**`, the BFF), which forward to the C#
API. The product rules are in `../rules/project.md` ("The client's shape"); the Vue and JSDoc
conventions are in `../rules/vuejs-style.md` and `../rules/jsdoc.md`.

## Layout

```
components/ui/        the kit (Ui*): every screen is composed from it
components/<area>/    area components (account, catalog, client, checkout and so on)
pages/ layouts/       routes and shells -- kit components only, no raw tag
templates/<name>/     aurora, nova, classic: header, footer, page bodies, sections
stores/               Pinia -- the only callers of composables/apis
composables/apis/     one typed function per endpoint, no state
composables/useApi.ts the only module that knows transport (X-Language, 401, base URL)
lib/errorHandler.ts   the one place an error sentence is chosen
server/               Nitro BFF: every portal route goes through internalApiCall
types/ constants/     one concept per file; closed sets are enums
utils/ utils/ui/      pure helpers (formatting, rich-text allow-list, tone classes)
locales/{en,ru,hy}/   one JSON per area, all three languages together
tests/                vitest, mirrors the source tree
e2e/                  Playwright, one spec per flow, data-testid selectors only
scripts/              lint:i18n, check-layers, legacy-field gate, audit:conformance
```

## Running it

```bash
yarn install
yarn dev                  # http://localhost:3000
yarn build && yarn preview
```

Copy `.env.example` to `.env`. The client holds no credential for an external service: the contact
form posts to the API, which sends the mail and the chat message (`EMAIL_TO`, `TELEGRAM_*` live in
`docker/.env`). `BILLING_PORTAL_URL` is the address of the billing portal that the
"add a payment method" and "announcements" links point at; leave it unset and those links are
hidden. In a deployed image the same value is read as `NUXT_PUBLIC_BILLING_PORTAL_URL`.

## Gates

There is no ratchet any more: every rule applies to every `.vue` and `.ts` file.

| Command | What it checks |
|---|---|
| `yarn lint` | ESLint conformance block (JSDoc, const arrows, braces, placeholders, no raw tags in screens, layering imports, no `any`/`!`), `scripts/check-layers.mjs`, `scripts/check-no-legacy-error-fields.mjs` |
| `yarn lint:i18n` | en/ru/hy parity, unused keys, literal `t('key')` absent from `en` |
| `yarn audit:conformance --strict` | every conformance metric is zero (raw tags, JSDoc, test ids, hard-coded text, `style`, `catch` in `.vue` and so on) |
| `yarn typecheck` | `nuxt typecheck` (`vue-tsc`) |
| `yarn test --maxWorkers=2` | vitest: unit and component tests in `tests/` (real kit, real Pinia) |
| `yarn e2e` | Playwright against the production build, the C# API replaced by `e2e/support/stub-backend.mjs` (see `e2e/README.md`) |

CI runs them in this order in `client:test`, then `client:e2e`; none is allowed to fail.

## Adding a language

Add the code to `AppLocale` (`types/applocale.ts`), to `i18n.locales` in `nuxt.config.ts`, and a
`locales/<code>/` folder with every JSON of `locales/en/`. `tests/locales/parity.test.ts` and
`yarn lint:i18n` fail until the set is complete; the backend needs its own `.resx` set
(`rules/localization.md`).

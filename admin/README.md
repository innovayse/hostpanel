# admin

The Hostpanel operator back office: Vue 3.5 + Vite + TypeScript (strict) + Pinia + vue-i18n
(en, ru, hy) + Tailwind 4. The rules it follows are in `../rules/project.md` ("The admin's shape")
and the workspace `rules/frontend.md`.

## Layout

```
src/
  views/            <Page>View.vue: routed screens (read a store, call its actions)
  layouts/          routed shells
  components/ui/    the kit every screen is composed from (Ui*)
  components/<area>/ screen-specific parts composed from the kit
  stores/           <area>.ts: Pinia; the only callers of composables/apis
  composables/      useApi.ts (the only transport), apis/use<Area>Api.ts, use*.ts
  types/            one file per concept
  lib/              pure helpers (errorHandler, runAction, format, money, tones, ...)
  utils/ui/         TypeScript the kit shares
  middleware/       the router guard
  locales/          <lang>/<area>.json: one file per area, all three languages together
tests/              vitest, mirrors src/
e2e/                Playwright flows (API stubbed with page.route), fixtures/ and support/
scripts/            lint-i18n.ts, audit.mjs
eslint.config.ts   flat config; exports strictRules (the house rules) and the local require-placeholder rule
```

## Setup

```sh
npm install
npm run dev
```

## Checks (all run in CI, in this order)

```sh
npm run lint:ci       # oxlint + ESLint, non-fixing (npm run lint fixes locally)
npm run lint:i18n     # catalogues: missing / extra / unused / unknown keys
npm run typecheck     # vue-tsc -b --force
npm test -- --maxWorkers=2
npm run build-only
npm run e2e           # Playwright against vite preview; needs the Playwright browsers
npm run audit         # conformance counters, all zero
```

## Adding a screen

1. A view in `src/views/`, composed from `components/ui/` only, with a `placeholder` on every
   input and a `data-testid` on every interactive element.
2. State in `src/stores/<area>.ts`, requests in `src/composables/apis/use<Area>Api.ts`; the view
   never calls the API composable.
3. Every sentence as `t('area.key')` in `src/locales/{en,ru,hy}/<area>.json`.
4. A test under `tests/` (the coverage-shape test fails without one) and, for a flow, a spec in `e2e/`.

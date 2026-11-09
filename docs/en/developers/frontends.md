---
title: Frontends
description: Data flow and conventions for the admin panel and client portal.
---

# Frontends

## Admin panel data flow

`admin/src/` follows a strict four-layer path. Nothing skips a layer.

```
View
  └─ store           src/stores/<area>.ts
       └─ API composable   src/composables/apis/use<Area>Api.ts
            └─ useApi
```

- **Views** declare what to show and call store actions. They never call `useApi` directly and never hold a `catch` block.
- **Stores** run every API call through `runAction` and expose `error` and `fieldErrors` for the view to read.
- **API composables** wrap each endpoint in one typed function and call `useApi`.
- **`useApi`** sends every request with the CSRF header and `X-Language`.

## Client portal data flow

`client/` adds one layer: the portal's own server routes sit between the API composable and the backend API.

```
Page
  └─ store
       └─ API composable
            └─ useApi
                 └─ server route   server/api/portal/**
                      └─ API
```

The layer order is enforced by the lint rules and `client/scripts/check-layers.mjs`. Templates for server-rendered pages live under `client/templates/<template>/`.

## The component kit

Both apps share the same component conventions:

- No raw HTML controls in views. Always use a component from the kit.
- Every input has a placeholder.
- Every interactive element has a `data-testid` attribute in the form `<area>-<element>`.
- JSDoc on every exported function and component.

## Translations

Each area has its own locale files. Run `lint:i18n` before pushing any translation change.

<Callout type="tip">
Run <code>lint:i18n</code> before pushing translations. It catches missing keys before CI does.
</Callout>

See [Translating Hostpanel](./translating) for the full translation workflow.

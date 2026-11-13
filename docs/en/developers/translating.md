---
title: Translating Hostpanel
description: Fix or complete a translation in the admin panel, client portal or API error messages.
---

# Translating Hostpanel

Hostpanel ships in English, Russian and Armenian.

<Callout type="note">
English is the source language and the fallback when a text is missing in another language.
</Callout>

## Where the texts live

| Part | Path | Loaded by |
|------|------|-----------|
| Admin panel | `admin/src/locales/<lang>/<area>.json` | `admin/src/locales/messages.ts` |
| Client portal | `client/locales/<lang>/<module>.json` | `client/plugins/i18n.ts` (`MODULES` list) |
| API error messages | `backend/src/Innovayse.Hostpanel.Application/Resources/ErrorMessages.resx` (en) with `ErrorMessages.ru.resx` / `ErrorMessages.hy.resx` | Language is read from the `X-Language` request header; `Accept-Language` is not used. Unknown or missing values fall back to `en`. |
| API validation messages | `ApplicationValidationMessages*.resx` in `Application/Resources/` and `ValidationMessages*.resx` in `Shared/Resources/` | Same as above: `X-Language` header, fallback to `en`. |
| Email templates | Edited per language in the admin panel, not in files | |
| Storefront content | Entered per language in the admin panel | |

## How keys are named

**Admin panel.** The file name is the first segment of the key. `nav.json` holds `nav.*`, used as `t('nav.dashboard')`. Each area has its own file: `billing.json`, `clients.json`, `servers.json`, and so on.

**Client portal.** Keys are nested inside module files and merged into one set at startup by `client/plugins/i18n.ts`.

**API.** The key is the error code, for example `SIGN_IN_UNAUTHORIZED`.

<Callout type="warning">
Never rename a key in a translation file. Only change the value.
</Callout>

## Placeholders and plurals

Keep every `{name}` placeholder exactly as it appears in the English source. The parity tests check them and fail if a placeholder is missing or renamed.

The admin panel has a Russian plural rule defined in `admin/src/i18n.ts`. When adding a plural form in Russian, follow the rule already there. For example:

```ts
// admin/src/locales/ru/billing.json
{
  "invoiceCount": "{n} счёт | {n} счёта | {n} счетов"
}
```

The three slots correspond to one, two to four and five-or-more respectively, matching the `russianPlural` function in `i18n.ts`.

## Style

Short, plain sentences. Use the same terms as the rest of the language. A small glossary of the main terms:

| English | Русский | Հայերեն |
|---------|---------|---------|
| Invoices | Счета | Հաշիվ-ապրանքագրեր |
| Services | Услуги | Ծառայություններ |
| Support Tickets | Тикеты поддержки | Աջակցության հայտեր |
| Servers | Серверы | Սերվերներ |
| Clients | Клиенты | Հաճախորդներ |

Do not add product names that are not in the English source.

## Check that nothing is missing

<Callout type="tip">
Run <code>lint:i18n</code> before you push. CI runs the same scripts.
</Callout>

::: code-group

```bash [Admin panel]
cd admin && npm run lint:i18n
```

```bash [Client portal]
cd client && yarn lint:i18n
```

```bash [API]
cd backend && dotnet test tests/Innovayse.Hostpanel.Architecture.Tests --filter ResourceCoverageTests
```

:::

A run with missing keys looks like this:

```
lint:i18n failed: 2 problem(s).
  missing in ru: nav.documentation
  missing in hy: nav.documentation
```

The admin panel check reports missing keys, extra keys, unused keys and translation calls with no English source. The test files `admin/tests/locales/parity.test.ts` and `admin/tests/locales/messages.test.ts` check that all languages have the same keys and that no placeholders are missing.

The client portal `lint:i18n` and `client/tests/locales/parity.test.ts` check the same things: same keys in every language, no empty strings, same placeholders.

`ResourceCoverageTests` (in the backend architecture tests) checks that every error code in `HostpanelErrorCodes.cs` has a sentence in every language file, with no extra keys and with values that differ from English.

## See your language locally

Start the dev stack (see [Run locally](./local-dev)).

- **Demo:** if a public demo is available, the "Try the demo" button in the docs navigation bar opens the admin panel. Switch language there to check the current state without a local stack.
- **Admin panel:** use the language switcher in the top bar. The choice is stored in the browser as `admin.locale`.
- **Client portal:** use the language switcher, which adds `/ru` or `/hy` to the URL. English is served without a prefix. See [Client portal](../customers/portal) for a tour of what customers see.
- **API error messages:** send `X-Language: en`, `X-Language: ru` or `X-Language: hy` on any request and read the `detail` field of the error body. Any unsupported value falls back to `en`.
- **Email templates:** translated versions are managed in the admin panel. See [Email templates](../staff/email-templates) for how to add a language version.

## Send the change

One pull request per language and area is fine. Run all checks above first. Screenshots of two or three translated screens help review. Open the pull request against `develop`.

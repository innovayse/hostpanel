---
title: Add a new language
description: Steps to add a fourth language to the admin panel, client portal and API.
---

# Add a new language

<Callout type="note">
Open an issue before you start. A new language needs all three parts translated before it is switched on, so it is planned in advance.
</Callout>

## Admin panel

<Steps>
<li>

Copy `admin/src/locales/en/` to a new folder named with the language code, for example `admin/src/locales/fr/`.

</li>
<li>

Add the language to the `MessageLanguage` enum in `admin/src/locales/messages.ts`:

```ts
export enum MessageLanguage {
  En = 'en',
  Ru = 'ru',
  Hy = 'hy',
  Fr = 'fr', // add here
}
```

</li>
<li>

Add an entry for the new language to the initial object of `buildMessages` in `messages.ts`:

```ts
const result: Record<MessageLanguage, LanguageMessages> = {
  [MessageLanguage.En]: {},
  [MessageLanguage.Ru]: {},
  [MessageLanguage.Hy]: {},
  [MessageLanguage.Fr]: {}, // add here
}
```

</li>
<li>

Add the language code to `SUPPORTED_LOCALES` in `admin/src/i18n.ts`:

```ts
export const SUPPORTED_LOCALES = [
  MessageLanguage.En,
  MessageLanguage.Hy,
  MessageLanguage.Ru,
  MessageLanguage.Fr, // add here
] as const
```

</li>
<li>

Add the code to `LANGUAGES` in `admin/scripts/lib/i18nCheck.ts`:

```ts
export const LANGUAGES = ['en', 'ru', 'hy', 'fr'] as const
```

</li>
<li>

If the language needs a plural rule, add it to `admin/src/i18n.ts` following the `russianPlural` function already there, and register it under `pluralRules`.

</li>
</Steps>

## Client portal

<Steps>
<li>

Copy `client/locales/en/` to a new folder named with the language code, for example `client/locales/fr/`.

</li>
<li>

Add the language to the `AppLocale` enum in `client/types/applocale.ts`:

```ts
export enum AppLocale {
  En = 'en',
  Ru = 'ru',
  Hy = 'hy',
  Fr = 'fr', // add here
}
```

`APP_LOCALES` and `DEFAULT_APP_LOCALE` in `client/constants/appLocales.ts` are derived from the enum automatically.

</li>
<li>

Add an entry to the `i18n.locales` list in `client/nuxt.config.ts`:

```ts
locales: [
  { code: 'en', name: 'English',   language: 'en-US' },
  { code: 'ru', name: 'Русский',   language: 'ru-RU' },
  { code: 'hy', name: 'Հայերեն', language: 'hy-AM' },
  { code: 'fr', name: 'Français',  language: 'fr-FR' }, // add here
],
```

</li>
</Steps>

## API

<Steps>
<li>

Add the language files next to the existing ones:

- `backend/src/Innovayse.Hostpanel.Application/Resources/ErrorMessages.fr.resx`
- `backend/src/Innovayse.Hostpanel.Application/Resources/ApplicationValidationMessages.fr.resx`
- `backend/src/Shared/Resources/ValidationMessages.fr.resx`

</li>
<li>

Add the code to `SupportedLanguages.All` in `backend/src/Shared/Constants/SupportedLanguages.cs`:

```csharp
public static readonly HashSet<string> All = ["en", "ru", "hy", "fr"];
```

</li>
<li>

Add the code to `LocaleOptions.SupportedLocales` in `backend/src/Innovayse.Hostpanel.Application/Common/Settings/LocaleOptions.cs`:

```csharp
public static readonly IReadOnlyList<string> SupportedLocales = ["en", "ru", "hy", "fr"];
```

</li>
<li>

The backend list above covers the core error and validation messages. The following places also need updating for a complete language addition:

**Required — the language will not work without these:**

- `SupportedLanguages.All` and `LocaleOptions.SupportedLocales` as shown above.

**Client portal extras (required for a fully translated storefront):**

- `client/composables/useSeo.ts` — add an entry to `ogLocaleMap` so Open Graph and hreflang tags use the correct locale code, e.g. `[AppLocale.Fr]: 'fr_FR'`.
- `client/server/utils/clientMenuLabels.ts` — add a new `[AppLocale.Fr]: { ... }` block to `CLIENT_MENU_MESSAGES` with the translated labels for the SSO widget menu.
- `client/composables/useLocalizedSetting.ts` — the `getLocalized` function checks only `AppLocale.Ru` and `AppLocale.Hy`; add a branch for the new locale so per-locale settings (the contact address, office hours) are read correctly.
- `client/plugins/live-chat.ts` — add a case to `languageOf` returning the English name of the new language, and add a branch to the `token` computed value reading the new per-locale portal setting key.
- `client/constants/publicPortalSettingKeys.ts` — add the new chat token key (e.g. `PortalSettingKey.ChatWebsiteTokenFr`) to the allow-list so the storefront endpoint exposes it. This is optional if the operator does not use the live-chat widget.

**Admin panel extras (required):**

- `admin/src/lib/constants.ts` — add an entry to the `CLIENT_LANGUAGE_OPTIONS` list for the new language so it appears in the client form language selector.
- `admin/src/lib/storefrontTranslations.ts` — add the new locale to `STOREFRONT_LOCALES` and add a `[MessageLanguage.Fr]: blank()` entry to `blankTranslations` so storefront content forms carry a tab for the new language.
- `admin/src/composables/useStorefrontLocaleTabs.ts` — add a tab entry for the new locale to the `tabs` computed array.
- `admin/src/types/setting.ts` — add language-suffixed keys for contact address, contact hours and the chat token, e.g. `ContactAddressFr`, `ContactHoursFr`, `ChatWebsiteTokenFr`, plus their `SETTING_ENV` entries. Without these, the per-language portal settings panel will not save or read the new language's values.
- `admin/src/components/settings/TrackingChatCard.vue` — add a row for the new chat token key alongside the existing Russian and Armenian rows. This is optional if the operator does not use the live-chat widget.

</li>
</Steps>

## Email templates

Add a version of each email template in the new language in the admin panel.

## Check it

Run all the checks from the [Translating Hostpanel](./translating) page. In addition:

- The new language appears in the language switcher in both the admin panel and the client portal.
- Sending `X-Language: fr` on a request returns translated error messages.

## Before opening the pull request

Do not switch the language on until all texts are translated. Open the pull request against `develop`.

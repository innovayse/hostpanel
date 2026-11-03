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
  { code: 'hy', name: 'Հայerен', language: 'hy-AM' },
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

Search the backend for other places that list languages before writing this step, since the list above may not be complete.

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

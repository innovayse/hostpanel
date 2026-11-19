---
title: Add a new language
description: Steps to add a fourth language to the admin panel, client portal and API.
---

# Add a new language

<Callout type="note">
Open an issue before you start. A new language needs all three parts translated before it is switched on, so it is planned in advance.
</Callout>

## Admin panel

The admin panel keeps two lists of languages. `MessageLanguage` (`admin/src/locales/messages.ts`) holds only the languages the admin interface itself is translated into; it drives the `locales` folders, `SUPPORTED_LOCALES` and the i18n check. `ContentLanguage` (`admin/src/locales/contentLanguage.ts`) holds the languages of content: what the API answers in, email templates, a client's preferred language and storefront content. The steps below add an interface language, which goes into both lists. A language that is only a content language, as German and Spanish are for now, is added to `ContentLanguage` alone, with the admin extras in the API section.

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

**Required (the language will not work without these):**

- `SupportedLanguages.All` and `LocaleOptions.SupportedLocales` as shown above.

**Client portal extras (required for a fully translated storefront):**

- `client/composables/useSeo.ts`: add an entry to `ogLocaleMap` so Open Graph and hreflang tags use the correct locale code, e.g. `[AppLocale.Fr]: 'fr_FR'`.
- `client/server/utils/clientMenuLabels.ts`: add a new `[AppLocale.Fr]: { ... }` block to `CLIENT_MENU_MESSAGES` with the translated labels for the SSO widget menu.

**Admin panel extras for a content language (required):**

- `admin/src/locales/contentLanguage.ts`: add the language to the `ContentLanguage` enum.
- `admin/src/lib/constants.ts`: add an entry to the `LANGUAGE_OPTIONS` list so the language appears in the language selectors of the client and user forms.
- `admin/src/lib/storefrontTranslations.ts`: add the locale to `STOREFRONT_LOCALES` and a `[ContentLanguage.Fr]: blank()` entry to `blankTranslations`, so storefront content forms carry a tab for it.
- `admin/src/composables/useStorefrontLocaleTabs.ts` and `admin/src/views/SlideFormView.vue`: add a tab for the new locale.
- The labels, in every language file of the admin panel: `storefront.locales.fr`, `settings.slideForm.locales.fr` and `clients.clientForm.french` (use the name of your language), in `storefront.json`, `settings.json` and `clients.json` under `admin/src/locales/<lang>/`.

**Backend extras:**

- `backend/src/Innovayse.Hostpanel.Application/Billing/Common/CreditNotePdfLabelSets.cs`: add a label set for the language and an arm for its code to the `switch` in `For`, so the credit note PDF prints in it.
- The email template seeders under `backend/src/Innovayse.Hostpanel.Application/Notifications/Seeding/`: each seeder adds a version of its template in the new language, and `SeededEmailLayout.RightsReserved` gets the footer sentence.
- The demo seed translations in `backend/src/Innovayse.Hostpanel.Infrastructure/Demo/DemoContentSeeder.cs` and `DemoStorefrontSeeder.cs`.
- The language theories of `ResourceCoverageTests` (`backend/tests/Innovayse.Hostpanel.Architecture.Tests/`) and the other tests that list the languages, which take a row for the new code.

**Settings with a language suffix (optional):**

The contact address, the working hours, the live chat token and the company story of the About page (`portal.about.text`) each have one value per language, for example `portal.contact.address.ru` and `portal.chat.website_token.hy`. A language without its own value uses the default one, so a new language works without any of this. Add them only if the operator needs a different address, hours, chat token or company story for the new language. Then every one of these places needs the new key, with the same suffix:

- `backend/src/Innovayse.Hostpanel.Domain/Settings/PortalSettingKeys.cs`: the constants, for example `ContactAddressFr = "portal.contact.address.fr"`, and their entries in the list of defaults, with an empty default and a description such as "Office address for French visitors. Empty uses the default address."
- `admin/src/types/setting.ts`: the `SettingKey` entries (`ContactAddressFr`, `ContactHoursFr`, `ChatWebsiteTokenFr`) and their `SettingEnv` entries, for example `CONTACT_ADDRESS_FR`. The company story has no field in the admin panel, so it needs nothing here.
- `client/constants/portalSettingKey.ts`: the same keys in the `PortalSettingKey` enum.
- `client/constants/publicPortalSettingKeys.ts`: the keys in the allow-lists, so the storefront endpoint exposes them: the contact keys, the chat token key and the company story key.
- `client/nuxt.config.ts`: the runtime config entries `portalContactAddressFr`, `portalContactHoursFr`, `portalChatWebsiteTokenFr` and `portalAboutTextFr`, each read from `NUXT_PUBLIC_PORTAL_..._FR`. They are the values a deployment sets in the environment.
- `client/composables/useContactDetails.ts` (address), `client/composables/useContactHours.ts` (hours), `client/composables/useAboutText.ts` (company story) and `client/composables/useLocalizedSetting.ts`, a branch for the new locale, so the right value is read.
- `client/plugins/live-chat.ts`: the token branch, and `languageOf` for the language name the chat widget shows.
- `admin/src/components/settings/StorefrontContentCard.vue` (address and hours fields) and `admin/src/components/settings/TrackingChatCard.vue` (token field), one field for each new key.
- The admin locale files: the labels of those fields: `settings.storefrontContent.contactAddressFr`, `settings.storefrontContent.contactHoursFr` (each with `label` and `hint`) and `settings.trackingChat.chat.tokenFr`, in every language of `admin/src/locales/<lang>/settings.json`.

The public demo has its own sample values in `DemoPortalSettingsSeeder.cs`. Add the new language there only if the demo should show it.

</li>
</Steps>

## Email templates

The seeders above create a version of each seeded email template in the new language at start-up. Open each template in the admin panel and check the text.

## Check it

Run all the checks from the [Translating Hostpanel](./translating) page. In addition:

- The new language appears in the language switcher in both the admin panel and the client portal.
- Sending `X-Language: fr` on a request returns translated error messages.

## Before opening the pull request

Do not switch the language on until all texts are translated. Open the pull request against `develop`.

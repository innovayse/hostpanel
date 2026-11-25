---
title: Add a new language
description: Steps to add a fourth language to the admin panel, client portal and API.
---

# Add a new language

<Callout type="note">
Open an issue before you start. A new language needs all three parts translated before it is switched on, so it is planned in advance.
</Callout>

## Admin panel

The admin panel keeps two lists of languages. `MessageLanguage` (`admin/src/locales/messages.ts`) holds only the languages the admin interface itself is translated into; it drives the `locales` folders, `SUPPORTED_LOCALES` and the i18n check. `ContentLanguage` (`admin/src/locales/contentLanguage.ts`) holds the languages of content: what the API answers in, email templates, a client's preferred language and storefront content. The steps below add an interface language, which goes into both lists. A language that is only a content language, as Spanish is for now, is added to `ContentLanguage` alone, with the admin extras in the API section.

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

The language switcher list `SUPPORTED_LOCALES` in `admin/src/i18n.ts` is built from `MessageLanguage`, so adding the language to `MessageLanguage` (step above) is all it needs.

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

Add the language to `LOCALE_META` in `client/nuxt.config.ts`: its own name and its language tag. The `i18n.locales` list and the runtime config entries of the per-language settings are built from `AppLocale`.

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

Add the code to `LanguageCodes.All` in `backend/src/SharedKernel/Localization/LanguageCodes.cs`, the one list `SupportedLanguages` and every per-language setting row are built from:

```csharp
public static readonly IReadOnlyList<string> All = [Default, "ru", "hy", "fr"];
```

</li>
<li>

The backend list above covers the core error and validation messages. The following places also need updating for a complete language addition:

**Required (the language will not work without these):**

- `LanguageCodes.All` as shown above.

**Client portal extras (required for a fully translated storefront):**

- `client/composables/useSeo.ts`: add an entry to `ogLocaleMap` so Open Graph and hreflang tags use the correct locale code, e.g. `[AppLocale.Fr]: 'fr_FR'`.
- `client/server/utils/clientMenuLabels.ts`: add a new `[AppLocale.Fr]: { ... }` block to `CLIENT_MENU_MESSAGES` with the translated labels for the SSO widget menu.

**Admin panel extras for a content language (required):**

- `admin/src/locales/contentLanguage.ts`: add the language to the `ContentLanguage` enum.
- `admin/src/lib/storefrontTranslations.ts`: nothing to add, `STOREFRONT_LOCALES` and the blank forms are built from the `ContentLanguage` enum, so the storefront content forms carry a tab for the new language as soon as it is in the enum.
- The labels, in every language file of the admin panel: `storefront.locales.fr`, (use the name of your language), in `storefront.json`, `settings.json` and `clients.json` under `admin/src/locales/<lang>/`.

**Backend extras:**

- `backend/src/Innovayse.Hostpanel.Application/Billing/Common/CreditNotePdfLabelSets.cs`: add a label set for the language and an arm for its code to the `switch` in `For`, so the credit note PDF prints in it.
- The email template seeders under `backend/src/Innovayse.Hostpanel.Application/Notifications/Seeding/`: each seeder adds a version of its template in the new language, and `SeededEmailLayout.RightsReserved` gets the footer sentence.
- The demo seed translations in `backend/src/Innovayse.Hostpanel.Infrastructure/Demo/DemoContentSeeder.cs` and `DemoStorefrontSeeder.cs`.
- The language theories of `ResourceCoverageTests` (`backend/tests/Innovayse.Hostpanel.Architecture.Tests/`) and the other tests that list the languages, which take a row for the new code.

**Settings with a language suffix:**

The contact address, the working hours, the live chat token, the company story of the About page (`portal.about.text`) and the five legal texts each have one value per language, for example `portal.contact.address.ru` and `portal.chat.website_token.de`. A language without its own value uses the default one. The rows of every language are named from the default key and the language code (`PortalSettingKeys.ForLanguage`, `localizedSettingKey` in the admin and the portal), so a new language needs no key written out anywhere:

- The backend seeds the empty row of the new language for each of these settings on its next start, from `LanguageCodes.All`. Nothing is added to `PortalSettingKeys.cs`, and no migration is needed.
- The admin panel draws a tab for the new language in each of these fields as soon as the language is in `ContentLanguage`, because `UiLocalizedField` reads `STOREFRONT_LOCALES`.
- The portal exposes the row through the allow-list and reads it for visitors of that language (`client/utils/localizedSetting.ts`), with no list to extend.
- `client/plugins/live-chat.ts`: `languageOf` needs the language name the chat widget shows.

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

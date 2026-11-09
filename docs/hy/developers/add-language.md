---
title: Նոր լեզվի ավելացում
description: Ադմինի վահանակին, հաճախորդի պորտալին և API-ին չորրորդ լեզու ավելացնելու քայլերը։
---

# Նոր լեզվի ավելացում {#add-a-new-language}

<Callout type="note">
Սկսելուց առաջ բացեք issue։ Նոր լեզուն միացնելուց առաջ պետք է թարգմանված լինեն երեք մասերը, ուստի այն պլանավորվում է նախապես։
</Callout>

## Ադմինի վահանակ {#admin-panel}

<Steps>
<li>

Պատճենեք `admin/src/locales/en/` պանակը լեզվի կոդով անվանված նոր պանակի մեջ, օրինակ՝ `admin/src/locales/fr/`։

</li>
<li>

Ավելացրեք լեզուն `admin/src/locales/messages.ts` ֆայլի `MessageLanguage` enum-ում․

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

Ավելացրեք նոր լեզվի գրառումը `messages.ts`-ի `buildMessages` ֆունկցիայի սկզբնական օբյեկտում․

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

Ավելացրեք լեզվի կոդը `admin/src/i18n.ts` ֆայլի `SUPPORTED_LOCALES`-ում․

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

Ավելացրեք կոդը `admin/scripts/lib/i18nCheck.ts` ֆայլի `LANGUAGES`-ում․

```ts
export const LANGUAGES = ['en', 'ru', 'hy', 'fr'] as const
```

</li>
<li>

Եթե լեզվին հոգնակի թվի կանոն է պետք, ավելացրեք այն `admin/src/i18n.ts` ֆայլում՝ հետևելով այնտեղ արդեն եղած `russianPlural` ֆունկցիային, և գրանցեք այն `pluralRules`-ում։

</li>
</Steps>

## Հաճախորդի պորտալ {#client-portal}

<Steps>
<li>

Պատճենեք `client/locales/en/` պանակը լեզվի կոդով անվանված նոր պանակի մեջ, օրինակ՝ `client/locales/fr/`։

</li>
<li>

Ավելացրեք լեզուն `client/types/applocale.ts` ֆայլի `AppLocale` enum-ում․

```ts
export enum AppLocale {
  En = 'en',
  Ru = 'ru',
  Hy = 'hy',
  Fr = 'fr', // add here
}
```

`client/constants/appLocales.ts` ֆայլի `APP_LOCALES`-ը և `DEFAULT_APP_LOCALE`-ը ստացվում են enum-ից ինքնաշխատ։

</li>
<li>

Ավելացրեք գրառում `client/nuxt.config.ts` ֆայլի `i18n.locales` ցանկում․

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

## API {#api}

<Steps>
<li>

Ավելացրեք լեզվի ֆայլերը արդեն եղածների կողքին․

- `backend/src/Innovayse.Hostpanel.Application/Resources/ErrorMessages.fr.resx`
- `backend/src/Innovayse.Hostpanel.Application/Resources/ApplicationValidationMessages.fr.resx`
- `backend/src/Shared/Resources/ValidationMessages.fr.resx`

</li>
<li>

Ավելացրեք կոդը `backend/src/Shared/Constants/SupportedLanguages.cs` ֆայլի `SupportedLanguages.All`-ում․

```csharp
public static readonly HashSet<string> All = ["en", "ru", "hy", "fr"];
```

</li>
<li>

Ավելացրեք կոդը `backend/src/Innovayse.Hostpanel.Application/Common/Settings/LocaleOptions.cs` ֆայլի `LocaleOptions.SupportedLocales`-ում․

```csharp
public static readonly IReadOnlyList<string> SupportedLocales = ["en", "ru", "hy", "fr"];
```

</li>
<li>

Վերևի backend ցանկը ծածկում է սխալների և վավերացման հիմնական հաղորդագրությունները։ Լեզվի լրիվ ավելացման համար թարմացնել է պետք նաև հետևյալ տեղերը․

**Պարտադիր․ առանց սրանց լեզուն չի աշխատի։**

- `SupportedLanguages.All` և `LocaleOptions.SupportedLocales`, ինչպես ցույց է տրված վերևում։

**Հաճախորդի պորտալի լրացումներ (պարտադիր է լրիվ թարգմանված հանրային կայքի համար)․**

- `client/composables/useSeo.ts`՝ ավելացրեք գրառում `ogLocaleMap`-ում, որպեսզի Open Graph և hreflang թեգերը օգտագործեն ճիշտ լեզվի կոդը, օրինակ՝ `[AppLocale.Fr]: 'fr_FR'`։
- `client/server/utils/clientMenuLabels.ts`՝ ավելացրեք նոր `[AppLocale.Fr]: { ... }` բլոկ `CLIENT_MENU_MESSAGES`-ում՝ SSO վիջեթի մենյուի թարգմանված պիտակներով։

**Ադմինի վահանակի լրացումներ (պարտադիր է)․**

- `admin/src/lib/constants.ts`՝ ավելացրեք գրառում `LANGUAGE_OPTIONS` ցանկում նոր լեզվի համար, որպեսզի այն երևա հաճախորդի և օգտատիրոջ ձևերի լեզվի ընտրիչներում։ Նրա պիտակը կարդում է նոր բանալի՝ `clients.clientForm.french` (օգտագործեք ձեր լեզվի անունը), որը ավելացնում եք `admin/src/locales/<lang>/clients.json` ֆայլի յուրաքանչյուր լեզվի տարբերակում՝ `english`, `russian` և `armenian` բանալիների կողքին։
- `admin/src/lib/storefrontTranslations.ts`՝ ավելացրեք նոր լեզուն `STOREFRONT_LOCALES`-ում և `[MessageLanguage.Fr]: blank()` գրառումը `blankTranslations`-ում, որպեսզի հանրային կայքի բովանդակության ձևերն ունենան ներդիր նոր լեզվի համար։
- `admin/src/composables/useStorefrontLocaleTabs.ts`՝ ավելացրեք նոր լեզվի ներդիրը `tabs` computed զանգվածում։

**Լեզվի վերջածանցով կարգավորումներ (ըստ ցանկության)․**

Կոնտակտային հասցեն, աշխատանքային ժամերը, կենդանի չաթի թոկենը և «Մեր մասին» էջի ընկերության պատմությունը (`portal.about.text`) ունեն մեկական արժեք յուրաքանչյուր լեզվի համար, օրինակ՝ `portal.contact.address.ru` և `portal.chat.website_token.hy`։ Առանց իր արժեքի լեզուն օգտագործում է լռելյայնը, ուստի նոր լեզուն աշխատում է առանց սրանցից որևէ մեկի։ Ավելացրեք դրանք միայն եթե օպերատորին նոր լեզվի համար պետք է այլ հասցե, այլ աշխատանքային ժամեր, այլ չաթի թոկեն կամ այլ պատմություն։ Այդ դեպքում այս բոլոր տեղերում պետք է նոր բանալին՝ նույն վերջածանցով․

- `backend/src/Innovayse.Hostpanel.Domain/Settings/PortalSettingKeys.cs`՝ հաստատունները, օրինակ՝ `ContactAddressFr = "portal.contact.address.fr"`, և դրանց գրառումները լռելյայն արժեքների ցանկում՝ դատարկ լռելյայն արժեքով և նկարագրությամբ, օրինակ՝ «Office address for French visitors. Empty uses the default address.»։
- `admin/src/types/setting.ts`՝ `SettingKey` գրառումները (`ContactAddressFr`, `ContactHoursFr`, `ChatWebsiteTokenFr`) և դրանց `SettingEnv` գրառումները, օրինակ՝ `CONTACT_ADDRESS_FR`։ Ընկերության պատմությունը ադմինի վահանակում դաշտ չունի, ուստի այստեղ ոչինչ պետք չէ։
- `client/constants/portalSettingKey.ts`՝ նույն բանալիները `PortalSettingKey` enum-ում։
- `client/constants/publicPortalSettingKeys.ts`՝ բանալիները թույլատրված ցանկերում, որպեսզի հանրային կայքի endpoint-ը դրանք բացահայտի՝ կոնտակտային բանալիները, չաթի թոկենի բանալին և ընկերության պատմության բանալին։
- `client/nuxt.config.ts`՝ runtime կարգավորման գրառումները `portalContactAddressFr`, `portalContactHoursFr`, `portalChatWebsiteTokenFr` և `portalAboutTextFr`, որոնցից յուրաքանչյուրը կարդացվում է `NUXT_PUBLIC_PORTAL_..._FR` փոփոխականից։ Սրանք այն արժեքներն են, որ տեղակայումը սահմանում է միջավայրում։
- `client/composables/useContactDetails.ts` (հասցե), `client/composables/useContactHours.ts` (աշխատանքային ժամեր), `client/composables/useAboutText.ts` (ընկերության պատմություն) և `client/composables/useLocalizedSetting.ts`՝ ճյուղ նոր լեզվի համար, որպեսզի կարդացվի ճիշտ արժեքը։
- `client/plugins/live-chat.ts`՝ թոկենի ճյուղը և `languageOf`՝ լեզվի այն անվան համար, որը ցույց է տալիս չաթի վիջեթը։
- `admin/src/components/settings/StorefrontContentCard.vue` (հասցեի և աշխատանքային ժամերի դաշտեր) և `admin/src/components/settings/TrackingChatCard.vue` (թոկենի դաշտ)՝ մեկ դաշտ յուրաքանչյուր նոր բանալու համար։
- Ադմինի լեզվական ֆայլերը՝ այդ դաշտերի պիտակները՝ `settings.storefrontContent.contactAddressFr`, `settings.storefrontContent.contactHoursFr` (յուրաքանչյուրը `label` և `hint` դաշտերով) և `settings.trackingChat.chat.tokenFr`, `admin/src/locales/<lang>/settings.json` ֆայլի յուրաքանչյուր լեզվում։

Հանրային դեմոն իր նմուշային արժեքները պահում է `DemoPortalSettingsSeeder.cs` ֆայլում։ Նոր լեզուն այնտեղ ավելացրեք միայն եթե դեմոն պետք է այն ցույց տա։

</li>
</Steps>

## Նամակների ձևանմուշներ {#email-templates}

Ադմինի վահանակում ավելացրեք նամակի յուրաքանչյուր ձևանմուշի տարբերակը նոր լեզվով։

## Ստուգեք {#check-it}

Գործարկեք բոլոր ստուգումները [Hostpanel-ի թարգմանություն](./translating) էջից։ Բացի այդ՝

- Նոր լեզուն երևում է լեզվի փոխարկիչում և՛ ադմինի վահանակում, և՛ հաճախորդի պորտալում։
- Հարցմանը `X-Language: fr` ուղարկելիս վերադարձվում են թարգմանված սխալի հաղորդագրություններ։

## Pull request բացելուց առաջ {#before-opening-the-pull-request}

Լեզուն մի միացրեք, մինչև բոլոր տեքստերը թարգմանված չեն։ Pull request-ը բացեք `develop` ճյուղի դեմ։

---
title: Նոր լեզվի ավելացում
description: Ադմինի վահանակին, հաճախորդի պորտալին և API-ին չորրորդ լեզու ավելացնելու քայլերը։
---

# Նոր լեզվի ավելացում {#add-a-new-language}

<Callout type="note">
Սկսելուց առաջ բացեք issue։ Նոր լեզուն միացնելուց առաջ պետք է թարգմանված լինեն երեք մասերը, ուստի այն պլանավորվում է նախապես։
</Callout>

## Ադմինի վահանակ {#admin-panel}

Ադմինի վահանակն ունի լեզուների երկու ցանկ։ `MessageLanguage`-ը (`admin/src/locales/messages.ts`) պարունակում է միայն այն լեզուները, որոնցով թարգմանված է հենց վահանակի ինտերֆեյսը․ դրանից են կախված `locales` պանակները, `SUPPORTED_LOCALES`-ը և i18n ստուգումը։ `ContentLanguage`-ը (`admin/src/locales/contentLanguage.ts`) պարունակում է բովանդակության լեզուները՝ API-ի պատասխանների լեզուն, նամակների ձևանմուշները, հաճախորդի նախընտրած լեզուն և հանրային կայքի բովանդակությունը։ Ստորև քայլերը ավելացնում են ինտերֆեյսի լեզու, որը մտնում է երկու ցանկում էլ։ Լեզուն, որը միայն բովանդակության լեզու է, ինչպես հիմա իսպաներենը, ավելացվում է միայն `ContentLanguage`-ում՝ API բաժնի ադմինի վահանակի լրացումների հետ։

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

`admin/src/i18n.ts`-ի `SUPPORTED_LOCALES` լեզվի փոխարկիչի ցանկը կառուցվում է `MessageLanguage`-ից, ուստի բավական է լեզուն ավելացնել `MessageLanguage`-ում (վերևի քայլը)։

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

Ավելացրեք լեզուն `client/nuxt.config.ts`-ի `LOCALE_META`-ում՝ նրա սեփական անունը և լեզվի թեգը։ `i18n.locales` ցանկը և լեզվով կարգավորումների runtime կարգավորման գրառումները կառուցվում են `AppLocale`-ից։

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

Ավելացրեք կոդը `backend/src/SharedKernel/Localization/LanguageCodes.cs` ֆայլի `LanguageCodes.All`-ում․ սա այն միակ ցանկն է, որից կառուցվում են `SupportedLanguages`-ը և կարգավորումների բոլոր լեզվական տողերը․

```csharp
public static readonly IReadOnlyList<string> All = [Default, "ru", "hy", "fr"];
```

</li>
<li>

Վերևի backend ցանկը ծածկում է սխալների և վավերացման հիմնական հաղորդագրությունները։ Լեզվի լրիվ ավելացման համար թարմացնել է պետք նաև հետևյալ տեղերը․

**Պարտադիր․ առանց սրանց լեզուն չի աշխատի։**

- `LanguageCodes.All`, ինչպես ցույց է տրված վերևում։

**Հաճախորդի պորտալի լրացումներ (պարտադիր է լրիվ թարգմանված հանրային կայքի համար)․**

- `client/composables/useSeo.ts`՝ ավելացրեք գրառում `ogLocaleMap`-ում, որպեսզի Open Graph և hreflang թեգերը օգտագործեն ճիշտ լեզվի կոդը, օրինակ՝ `[AppLocale.Fr]: 'fr_FR'`։
- `client/server/utils/clientMenuLabels.ts`՝ ավելացրեք նոր `[AppLocale.Fr]: { ... }` բլոկ `CLIENT_MENU_MESSAGES`-ում՝ SSO վիջեթի մենյուի թարգմանված պիտակներով։

**Ադմինի վահանակի լրացումներ բովանդակության լեզվի համար (պարտադիր է)․**

- `admin/src/locales/contentLanguage.ts`՝ ավելացրեք լեզուն `ContentLanguage` թվարկման մեջ։
- `admin/src/lib/storefrontTranslations.ts`՝ ոչինչ ավելացնել պետք չէ. `STOREFRONT_LOCALES`-ը և դատարկ ձևերը կառուցվում են `ContentLanguage` թվարկումից, ուստի հանրային կայքի բովանդակության ձևերում նոր լեզվի ներդիրը հայտնվում է հենց լեզուն թվարկման մեջ ավելացվելուն պես։
- Պիտակները ադմինի վահանակի յուրաքանչյուր լեզվի ֆայլում՝ `storefront.locales.fr`, (օգտագործեք ձեր լեզվի անունը), `admin/src/locales/<lang>/` պանակի `storefront.json`, `settings.json` և `clients.json` ֆայլերում։

**Backend-ի լրացումներ․**

- `backend/src/Innovayse.Hostpanel.Application/Billing/Common/CreditNotePdfLabelSets.cs`՝ ավելացրեք լեզվի պիտակների հավաքածուն և նրա կոդի ճյուղը `For` մեթոդի `switch`-ում, որպեսզի կրեդիտ նոտայի PDF-ը տպվի այդ լեզվով։
- Նամակների ձևանմուշների seeder-ները `backend/src/Innovayse.Hostpanel.Application/Notifications/Seeding/` պանակում՝ յուրաքանչյուր seeder ավելացնում է իր ձևանմուշի տարբերակը նոր լեզվով, իսկ `SeededEmailLayout.RightsReserved`-ը ստանում է ստորագրի նախադասությունը։
- Դեմո տվյալների թարգմանությունները `backend/src/Innovayse.Hostpanel.Infrastructure/Demo/DemoContentSeeder.cs` և `DemoStorefrontSeeder.cs` ֆայլերում։
- `ResourceCoverageTests`-ի (`backend/tests/Innovayse.Hostpanel.Architecture.Tests/`) լեզուների theory թեստերը և լեզուները թվարկող մյուս թեստերը՝ նոր կոդի համար պետք է տող ավելացնել։

**Լեզվի վերջածանցով կարգավորումներ․**

Կոնտակտային հասցեն, աշխատանքային ժամերը, կենդանի չաթի թոկենը, «Մեր մասին» էջի ընկերության պատմությունը (`portal.about.text`) և հինգ իրավական տեքստերը ունեն մեկական արժեք յուրաքանչյուր լեզվի համար, օրինակ՝ `portal.contact.address.ru` և `portal.chat.website_token.de`։ Առանց իր արժեքի լեզուն օգտագործում է լռելյայնը։ Յուրաքանչյուր լեզվի տողը անվանվում է լռելյայն բանալուց և լեզվի կոդից (`PortalSettingKeys.ForLanguage`, `localizedSettingKey` ադմինում և պորտալում), ուստի նոր լեզվի համար ոչ մի տեղ բանալի գրել պետք չէ․

- Backend-ը հաջորդ գործարկման ժամանակ `LanguageCodes.All`-ից ստեղծում է նոր լեզվի դատարկ տողը այս կարգավորումներից յուրաքանչյուրի համար։ `PortalSettingKeys.cs`-ում ոչինչ ավելացնել պետք չէ, միգրացիա նույնպես պետք չէ։
- Ադմինի վահանակը նոր լեզվի ներդիրը ցույց է տալիս այս դաշտերից յուրաքանչյուրում, հենց լեզուն հայտնվում է `ContentLanguage`-ում, քանի որ `UiLocalizedField`-ը կարդում է `STOREFRONT_LOCALES`-ը։
- Պորտալը տողը բացահայտում է թույլատրված ցանկով և կարդում է այդ լեզվի այցելուների համար (`client/utils/localizedSetting.ts`), ցանկը ընդլայնել պետք չէ։
- `client/plugins/live-chat.ts`՝ `languageOf`-ին պետք է լեզվի այն անունը, որը ցույց է տալիս չաթի վիջեթը։

Հանրային դեմոն իր նմուշային արժեքները պահում է `DemoPortalSettingsSeeder.cs` ֆայլում։ Նոր լեզուն այնտեղ ավելացրեք միայն եթե դեմոն պետք է այն ցույց տա։

</li>
</Steps>

## Նամակների ձևանմուշներ {#email-templates}

Վերևում նշված seeder-ները գործարկման ժամանակ ստեղծում են յուրաքանչյուր seed արված նամակի ձևանմուշի տարբերակը նոր լեզվով։ Բացեք յուրաքանչյուր ձևանմուշ ադմինի վահանակում և ստուգեք տեքստը։

## Ստուգեք {#check-it}

Գործարկեք բոլոր ստուգումները [Hostpanel-ի թարգմանություն](./translating) էջից։ Բացի այդ՝

- Նոր լեզուն երևում է լեզվի փոխարկիչում և՛ ադմինի վահանակում, և՛ հաճախորդի պորտալում։
- Հարցմանը `X-Language: fr` ուղարկելիս վերադարձվում են թարգմանված սխալի հաղորդագրություններ։

## Pull request բացելուց առաջ {#before-opening-the-pull-request}

Լեզուն մի միացրեք, մինչև բոլոր տեքստերը թարգմանված չեն։ Pull request-ը բացեք `develop` ճյուղի դեմ։

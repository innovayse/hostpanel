---
title: Добавление нового языка
description: Шаги по добавлению четвёртого языка в панель администратора, клиентский портал и API.
---

# Добавление нового языка {#add-a-new-language}

<Callout type="note">
Откройте issue, прежде чем начинать. Новый язык нужно перевести во всех трёх частях до включения, поэтому его планируют заранее.
</Callout>

## Панель администратора {#admin-panel}

<Steps>
<li>

Скопируйте `admin/src/locales/en/` в новую папку, названную кодом языка, например `admin/src/locales/fr/`.

</li>
<li>

Добавьте язык в перечисление `MessageLanguage` в `admin/src/locales/messages.ts`:

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

Добавьте запись для нового языка в начальный объект `buildMessages` в `messages.ts`:

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

Добавьте код языка в `SUPPORTED_LOCALES` в `admin/src/i18n.ts`:

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

Добавьте код в `LANGUAGES` в `admin/scripts/lib/i18nCheck.ts`:

```ts
export const LANGUAGES = ['en', 'ru', 'hy', 'fr'] as const
```

</li>
<li>

Если для языка нужно правило множественного числа, добавьте его в `admin/src/i18n.ts` по образцу уже имеющейся там функции `russianPlural` и зарегистрируйте в `pluralRules`.

</li>
</Steps>

## Клиентский портал {#client-portal}

<Steps>
<li>

Скопируйте `client/locales/en/` в новую папку, названную кодом языка, например `client/locales/fr/`.

</li>
<li>

Добавьте язык в перечисление `AppLocale` в `client/types/applocale.ts`:

```ts
export enum AppLocale {
  En = 'en',
  Ru = 'ru',
  Hy = 'hy',
  Fr = 'fr', // add here
}
```

`APP_LOCALES` и `DEFAULT_APP_LOCALE` в `client/constants/appLocales.ts` выводятся из этого перечисления автоматически.

</li>
<li>

Добавьте запись в список `i18n.locales` в `client/nuxt.config.ts`:

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

Добавьте языковые файлы рядом с существующими:

- `backend/src/Innovayse.Hostpanel.Application/Resources/ErrorMessages.fr.resx`
- `backend/src/Innovayse.Hostpanel.Application/Resources/ApplicationValidationMessages.fr.resx`
- `backend/src/Shared/Resources/ValidationMessages.fr.resx`

</li>
<li>

Добавьте код в `SupportedLanguages.All` в `backend/src/Shared/Constants/SupportedLanguages.cs`:

```csharp
public static readonly HashSet<string> All = ["en", "ru", "hy", "fr"];
```

</li>
<li>

Добавьте код в `LocaleOptions.SupportedLocales` в `backend/src/Innovayse.Hostpanel.Application/Common/Settings/LocaleOptions.cs`:

```csharp
public static readonly IReadOnlyList<string> SupportedLocales = ["en", "ru", "hy", "fr"];
```

</li>
<li>

Список бэкенда выше охватывает основные сообщения об ошибках и проверках. Для полного добавления языка нужно обновить ещё и следующие места:

**Обязательно (без этого язык не заработает):**

- `SupportedLanguages.All` и `LocaleOptions.SupportedLocales`, как показано выше.

**Дополнения клиентского портала (обязательны для полностью переведённой витрины):**

- `client/composables/useSeo.ts`: добавьте запись в `ogLocaleMap`, чтобы теги Open Graph и hreflang использовали правильный код локали, например `[AppLocale.Fr]: 'fr_FR'`.
- `client/server/utils/clientMenuLabels.ts`: добавьте новый блок `[AppLocale.Fr]: { ... }` в `CLIENT_MENU_MESSAGES` с переведёнными подписями для меню виджета SSO.

**Дополнения панели администратора (обязательны):**

- `admin/src/lib/constants.ts`: добавьте запись в список `LANGUAGE_OPTIONS` для нового языка, чтобы он появился в селекторах языка в формах клиента и пользователя. Его подпись читает новый ключ `clients.clientForm.french` (используйте название вашего языка), который вы добавляете во все языковые файлы `admin/src/locales/<lang>/clients.json` рядом с `english`, `russian` и `armenian`.
- `admin/src/lib/storefrontTranslations.ts`: добавьте новую локаль в `STOREFRONT_LOCALES` и запись `[MessageLanguage.Fr]: blank()` в `blankTranslations`, чтобы в формах содержимого витрины появилась вкладка для нового языка.
- `admin/src/composables/useStorefrontLocaleTabs.ts`: добавьте запись вкладки для новой локали в вычисляемый массив `tabs`.

**Настройки с языковым суффиксом (необязательно):**

Контактный адрес, часы работы, токен онлайн-чата и рассказ о компании на странице «О нас» (`portal.about.text`) имеют по одному значению на язык, например `portal.contact.address.ru` и `portal.chat.website_token.hy`. Язык без собственного значения использует значение по умолчанию, поэтому новый язык работает и без всего этого. Добавляйте их, только если оператору нужен другой адрес, часы работы, токен чата или рассказ о компании для нового языка. Тогда в каждом из этих мест нужен новый ключ с тем же суффиксом:

- `backend/src/Innovayse.Hostpanel.Domain/Settings/PortalSettingKeys.cs`: константы, например `ContactAddressFr = "portal.contact.address.fr"`, и их записи в списке значений по умолчанию, с пустым значением по умолчанию и описанием вроде «Office address for French visitors. Empty uses the default address.»
- `admin/src/types/setting.ts`: записи `SettingKey` (`ContactAddressFr`, `ContactHoursFr`, `ChatWebsiteTokenFr`) и их записи `SettingEnv`, например `CONTACT_ADDRESS_FR`. У рассказа о компании нет поля в панели администратора, поэтому здесь ему ничего не нужно.
- `client/constants/portalSettingKey.ts`: те же ключи в перечислении `PortalSettingKey`.
- `client/constants/publicPortalSettingKeys.ts`: ключи в списках разрешённых, чтобы конечная точка витрины их отдавала: контактные ключи, ключ токена чата и ключ рассказа о компании.
- `client/nuxt.config.ts`: записи конфигурации времени выполнения `portalContactAddressFr`, `portalContactHoursFr`, `portalChatWebsiteTokenFr` и `portalAboutTextFr`, каждая читается из `NUXT_PUBLIC_PORTAL_..._FR`. Это значения, которые развёртывание задаёт в окружении.
- `client/composables/useContactDetails.ts` (адрес), `client/composables/useContactHours.ts` (часы работы), `client/composables/useAboutText.ts` (рассказ о компании) и `client/composables/useLocalizedSetting.ts`: ветка для новой локали, чтобы читалось правильное значение.
- `client/plugins/live-chat.ts`: ветка токена и `languageOf` для названия языка, которое показывает виджет чата.
- `admin/src/components/settings/StorefrontContentCard.vue` (поля адреса и часов работы) и `admin/src/components/settings/TrackingChatCard.vue` (поле токена): по одному полю на каждый новый ключ.
- Языковые файлы панели администратора: подписи этих полей: `settings.storefrontContent.contactAddressFr`, `settings.storefrontContent.contactHoursFr` (каждый с `label` и `hint`) и `settings.trackingChat.chat.tokenFr`, во всех языках `admin/src/locales/<lang>/settings.json`.

У публичного демо есть собственные примеры значений в `DemoPortalSettingsSeeder.cs`. Добавляйте туда новый язык, только если демо должно его показывать.

</li>
</Steps>

## Шаблоны писем {#email-templates}

Добавьте версию каждого шаблона письма на новом языке в панели администратора.

## Проверьте результат {#check-it}

Выполните все проверки со страницы [Перевод Hostpanel](/ru/developers/translating). Дополнительно:

- Новый язык появляется в переключателе языка и в панели администратора, и в клиентском портале.
- Отправка `X-Language: fr` в запросе возвращает переведённые сообщения об ошибках.

## Перед открытием pull request {#before-opening-the-pull-request}

Не включайте язык, пока не переведены все тексты. Откройте pull request в ветку `develop`.

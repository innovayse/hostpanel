---
title: Добавление нового языка
description: Шаги по добавлению четвёртого языка в панель администратора, клиентский портал и API.
---

# Добавление нового языка {#add-a-new-language}

<Callout type="note">
Откройте issue, прежде чем начинать. Новый язык нужно перевести во всех трёх частях до включения, поэтому его планируют заранее.
</Callout>

## Панель администратора {#admin-panel}

В панели администратора есть два списка языков. `MessageLanguage` (`admin/src/locales/messages.ts`) содержит только языки, на которые переведён сам интерфейс панели; от него зависят папки `locales`, `SUPPORTED_LOCALES` и проверка i18n. `ContentLanguage` (`admin/src/locales/contentLanguage.ts`) содержит языки содержимого: язык ответов API, шаблоны писем, предпочитаемый язык клиента и содержимое витрины. Шаги ниже добавляют язык интерфейса, который попадает в оба списка. Язык, который является только языком содержимого, как сейчас испанский, добавляется только в `ContentLanguage`, вместе с дополнениями панели администратора из раздела API.

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

Список переключателя языка `SUPPORTED_LOCALES` в `admin/src/i18n.ts` строится из `MessageLanguage`, поэтому достаточно добавить язык в `MessageLanguage` (шаг выше).

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

Добавьте язык в `LOCALE_META` в `client/nuxt.config.ts`: его собственное название и языковой тег. Список `i18n.locales` и записи конфигурации времени выполнения для настроек по языкам строятся из `AppLocale`.

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

Добавьте код в `LanguageCodes.All` в `backend/src/SharedKernel/Localization/LanguageCodes.cs`: это единственный список, из которого строятся `SupportedLanguages` и все языковые строки настроек:

```csharp
public static readonly IReadOnlyList<string> All = [Default, "ru", "hy", "fr"];
```

</li>
<li>

Список бэкенда выше охватывает основные сообщения об ошибках и проверках. Для полного добавления языка нужно обновить ещё и следующие места:

**Обязательно (без этого язык не заработает):**

- `LanguageCodes.All`, как показано выше.

**Дополнения клиентского портала (обязательны для полностью переведённой витрины):**

- `client/composables/useSeo.ts`: добавьте запись в `ogLocaleMap`, чтобы теги Open Graph и hreflang использовали правильный код локали, например `[AppLocale.Fr]: 'fr_FR'`.
- `client/server/utils/clientMenuLabels.ts`: добавьте новый блок `[AppLocale.Fr]: { ... }` в `CLIENT_MENU_MESSAGES` с переведёнными подписями для меню виджета SSO.

**Дополнения панели администратора для языка содержимого (обязательны):**

- `admin/src/locales/contentLanguage.ts`: добавьте язык в перечисление `ContentLanguage`.
- `admin/src/lib/storefrontTranslations.ts`: добавлять ничего не нужно, `STOREFRONT_LOCALES` и пустые формы строятся из перечисления `ContentLanguage`, поэтому в формах содержимого витрины вкладка нового языка появляется, как только язык попал в перечисление.
- Подписи во всех языковых файлах панели администратора: `storefront.locales.fr`, (используйте название вашего языка), в файлах `storefront.json`, `settings.json` и `clients.json` в `admin/src/locales/<lang>/`.

**Дополнения бэкенда:**

- `backend/src/Innovayse.Hostpanel.Application/Billing/Common/CreditNotePdfLabelSets.cs`: добавьте набор подписей для языка и ветку с его кодом в `switch` метода `For`, чтобы PDF кредит-ноты печатался на этом языке.
- Сидеры шаблонов писем в `backend/src/Innovayse.Hostpanel.Application/Notifications/Seeding/`: каждый сидер добавляет версию своего шаблона на новом языке, а `SeededEmailLayout.RightsReserved` получает фразу для подвала.
- Переводы демо-данных в `backend/src/Innovayse.Hostpanel.Infrastructure/Demo/DemoContentSeeder.cs` и `DemoStorefrontSeeder.cs`.
- Языковые theory-тесты `ResourceCoverageTests` (`backend/tests/Innovayse.Hostpanel.Architecture.Tests/`) и другие тесты, перечисляющие языки: для нового кода нужна строка.

**Настройки с языковым суффиксом:**

Контактный адрес, часы работы, токен онлайн-чата, рассказ о компании на странице «О нас» (`portal.about.text`) и пять юридических текстов имеют по одному значению на язык, например `portal.contact.address.ru` и `portal.chat.website_token.de`. Язык без собственного значения использует значение по умолчанию. Строки каждого языка именуются по ключу по умолчанию и коду языка (`PortalSettingKeys.ForLanguage`, `localizedSettingKey` в админке и в портале), поэтому для нового языка нигде не нужно выписывать ключи:

- Бэкенд при следующем запуске создаёт пустую строку нового языка для каждой из этих настроек из `LanguageCodes.All`. В `PortalSettingKeys.cs` ничего добавлять не нужно, миграция не нужна.
- Панель администратора рисует вкладку нового языка в каждом из этих полей, как только язык есть в `ContentLanguage`, потому что `UiLocalizedField` читает `STOREFRONT_LOCALES`.
- Портал отдаёт строку через список разрешённых и читает её для посетителей этого языка (`client/utils/localizedSetting.ts`), расширять список не нужно.
- `client/plugins/live-chat.ts`: в `languageOf` нужно название языка, которое показывает виджет чата.

У публичного демо есть собственные примеры значений в `DemoPortalSettingsSeeder.cs`. Добавляйте туда новый язык, только если демо должно его показывать.

</li>
</Steps>

## Шаблоны писем {#email-templates}

Сидеры, описанные выше, при запуске создают версию каждого заложенного шаблона письма на новом языке. Откройте каждый шаблон в панели администратора и проверьте текст.

## Проверьте результат {#check-it}

Выполните все проверки со страницы [Перевод Hostpanel](/ru/developers/translating). Дополнительно:

- Новый язык появляется в переключателе языка и в панели администратора, и в клиентском портале.
- Отправка `X-Language: fr` в запросе возвращает переведённые сообщения об ошибках.

## Перед открытием pull request {#before-opening-the-pull-request}

Не включайте язык, пока не переведены все тексты. Откройте pull request в ветку `develop`.

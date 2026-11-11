---
title: Написание плагина
description: "Как написать плагин Hostpanel как библиотеку классов .NET на основе SDK: платёжные плагины и плагины провижининга, манифест plugin.json, упаковка и тесты."
keywords: "написание плагина, hostpanel sdk, платёжный плагин, плагин провижининга, манифест плагина"
---

# Написание плагина {#write-a-plugin}

Плагин это библиотека классов .NET, которая ссылается на SDK Hostpanel и добавляет платёжный шлюз, модуль провижининга, регистратора доменов или другую возможность. Плагин никогда не ссылается на хост Hostpanel напрямую, только на SDK.

## Прежде чем начать {#before-you-start}

Проект SDK лежит в репозитории по пути `backend/src/Hostpanel.Sdk`. Сошлитесь на него из своей библиотеки классов или сошлитесь напрямую на собранный `Hostpanel.Sdk.dll`. Это руководство написано для Hostpanel.Sdk 3.1.0, поэтому собирайте плагин с этой версией или более поздней 3.x: пакет, собранный со старой основной версией SDK, отклоняется с `PLUGIN_SDK_ASSEMBLY_OUTDATED`. Репозиторий SDK также публикует второй пакет, `Hostpanel.Sdk.Packaging`, версия которого меняется вместе с SDK (3.1.0). В нём собраны проверки, которые хост выполняет над пакетом, поэтому вы можете запустить их на своей сборке до загрузки: формат подписи и её проверка, правила идентификатора и версии плагина, зарезервированные имена сборок, лимиты архива и проверки путей, проверка значка и структурные правила манифеста. Ссылайтесь на него только из инструментов сборки или релиза, никогда из самой сборки плагина. Встроенные плагины в `backend/src/Innovayse.Providers.*` это эталонные реализации; прочитайте их код, прежде чем писать свой.

### Имя проекта {#name-the-project}

Называйте проект плагина `<Company>.Hostpanel.Plugins.<Name>`: это соглашение .NET для плагинов `<Company>.<Product>.<Type>.<Name>` (как в `Serilog.Sinks.Console`). Проект, его папка, его сборка и корневое пространство имён носят это имя, а тестовый проект добавляет `.Tests`. Собственные плагины Innovayse называются `Innovayse.Hostpanel.Plugins.<Name>`, тесты лежат в `Innovayse.Hostpanel.Plugins.<Name>.Tests`, и каждый плагин находится в отдельном репозитории с именем `hostpanel-<name>`. Встроенные проекты `Innovayse.Providers.*` появились раньше этого соглашения и переименовываются по мере переноса в отдельные репозитории.

`id` плагина в `plugin.json` не входит в имя и никогда не меняется при переименовании проекта: хост связывает с ним сохранённые настройки и данные плагина.

## Типы плагинов {#plugin-types}

Перечисление `PluginType` определяет, чем может быть плагин:

| Тип | Значение | Рассматривается в этом руководстве |
|------|-------|-----------------------|
| `Provisioning` | 0 | Да, см. ниже |
| `Payment` | 1 | Да, см. ниже |
| `Registrar` | 2 | Только перечислен |
| `Identity` | 3 | Только перечислен |
| `Fulfilment` | 4 | Только перечислен |
| `Hosted` | 5 | Только перечислен |
| `App` | 6 | Только перечислен |

## Платёжный плагин {#a-payment-plugin}

Наследуйте `PaymentGatewayBase` и реализуйте `IPaymentPlugin` из `Hostpanel.Sdk.Plugins.Payment`.

### Обязательные члены {#required-members}

| Член | Описание |
|--------|-------------|
| `string CurrencyCode` | Числовой код валюты по ISO 4217, в которой проводит расчёты этот шлюз (например, `"840"` для USD, `"051"` для AMD) |
| `Task<PaymentSession> CreatePaymentAsync(PaymentInstruction instruction, CancellationToken ct)` | Начать платёжную сессию и вернуть URL перенаправления |
| `Task<GatewayPaymentStatus> GetStatusAsync(string gatewayOrderId, CancellationToken ct)` | Опросить шлюз о текущем состоянии |
| `Task<string> RefundAsync(string gatewayOrderId, long amountMinor, CancellationToken ct)` | Выполнить возврат; вернуть ссылку шлюза на возврат |

### Типы {#types}

Свойства **`PaymentInstruction`**: `OrderNumber`, `AmountMinor`, `ReturnUrl`, `Description`, `Language`.

Свойства **`PaymentSession`**: `GatewayOrderId`, `RedirectUrl`.

Свойства **`GatewayPaymentStatus`**: `State` (`GatewayPaymentState`), `TransactionId`, `RawStatus`, `AmountMinor`, `Currency` (числовой код ISO 4217).

У `AcceptsCurrency(string currencyNumeric)` есть реализация по умолчанию, которая сравнивает с `CurrencyCode`; переопределяйте её, только когда шлюз принимает валюты, отличные от валюты расчётов.

Перечисление **`GatewayPaymentState`**: `Pending` (0), `Paid` (1), `Declined` (2).

### Чтение настроек {#reading-settings}

Вызовите `GetConfig(key)` (из `PaymentGatewayBase`), чтобы прочитать настройку. Настройки хранятся как `integration:{pluginId}:{key}` в конфигурации приложения и записываются страницей «Интеграции» панели администратора.

```csharp
public string? GetBaseUrl() => GetConfig("gateway_url");
```

### Сохранение данных между вызовами {#keeping-data-between-calls}

`CreatePaymentAsync`, `GetStatusAsync` и `RefundAsync` это отдельные вызовы, и шлюзу может понадобиться в следующем то, что было в предыдущем (идентификатор заказа, курсор). Примите `IPluginStore` в конструкторе плагина рядом с `IConfiguration`, которую передаёт хост:

```csharp
public MyGateway(IConfiguration configuration, IPluginStore store) : base(configuration)
{
    _store = store;
}
```

Хост предоставляет его при активации плагина. Оно общее для плагина: привязано к идентификатору вашего плагина и ни к какому клиенту, с теми же правилами ключей и ограничениями размера, что и `call.Store`, и удаляется вместе с данными плагина, когда плагин удалён и его данные очищены. Оно никогда не дотягивается до значений другого плагина или значений какого-либо клиента; данные об одном клиенте относятся к `call.Store`, который привязан к клиенту вызова. Хранилище это те же данные, что и хранилище вызова администратора вне какого-либо клиента.

### Пример {#example}

::: code-group

```csharp [MyGateway.cs]
using Hostpanel.Sdk.Base;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Payment;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MyCompany.Hostpanel.Plugins.MyGateway;

public sealed class MyGateway : PaymentGatewayBase, IPaymentPlugin
{
    public MyGateway(string pluginId, IConfiguration configuration, ILogger<MyGateway> logger)
        : base(pluginId, configuration, logger) { }

    public string CurrencyCode => "840"; // USD (ISO 4217 numeric)

    public Task<PaymentSession> CreatePaymentAsync(
        PaymentInstruction instruction, CancellationToken ct)
    {
        var url = GetConfig("gateway_url")
            ?? throw new InvalidOperationException("gateway_url is required");
        // ... call the gateway API and return a redirect URL
        return Task.FromResult(new PaymentSession("order-123", url + "/pay?order=123"));
    }

    public Task<GatewayPaymentStatus> GetStatusAsync(
        string gatewayOrderId, CancellationToken ct)
    {
        // ... query the gateway
        return Task.FromResult(
            new GatewayPaymentStatus(GatewayPaymentState.Paid, gatewayOrderId, "ok", null, null));
    }

    public Task<string> RefundAsync(string gatewayOrderId, long amountMinor, CancellationToken ct)
    {
        // ... call the gateway refund endpoint
        return Task.FromResult("refund-ref-456");
    }
}
```

```json [plugin.json]
{
  "id": "my-gateway",
  "name": "My Gateway",
  "version": "1.0.0",
  "author": "Your Company",
  "description": "Accept payments through My Gateway.",
  "type": "Payment",
  "category": "Payment Gateways",
  "color": "#0369a1",
  "entryPoint": "MyCompany.Hostpanel.Plugins.MyGateway.MyGateway",
  "sdkVersion": "1.0",
  "showInIntegrations": true,
  "integrationsCategory": "Payment Gateways",
  "fields": [
    { "key": "gateway_url", "label": "Gateway URL", "type": "text",   "required": true  },
    { "key": "api_key",     "label": "API Key",     "type": "secret", "required": true  },
    { "key": "mode",        "label": "Mode",        "type": "select", "required": false }
  ]
}
```

```csharp [MyGatewayTests.cs]
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Hostpanel.Sdk.Plugins.Payment;
using Xunit;

public class MyGatewayTests
{
    [Fact]
    public async Task CreatePaymentAsync_ReturnsRedirectUrl()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["integration:my-gateway:gateway_url"] = "https://test.example.com",
                ["integration:my-gateway:api_key"]     = "test-key",
            })
            .Build();

        var gateway = new MyGateway("my-gateway", config, NullLogger<MyGateway>.Instance);
        var instruction = new PaymentInstruction(
            OrderNumber: "1001",
            AmountMinor: 5000,
            ReturnUrl:   "https://panel.example.com/payment/result",
            Description: "Invoice #1001",
            Language:    "en");

        var session = await gateway.CreatePaymentAsync(instruction, CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(session.RedirectUrl));
        Assert.False(string.IsNullOrEmpty(session.GatewayOrderId));
    }
}
```

:::

## Плагин провижининга {#a-provisioning-plugin}

Наследуйте `ProvisioningProviderBase` и реализуйте `IProvisioningPlugin` из `Hostpanel.Sdk.Plugins.Provisioning`. Прочитайте `backend/src/Innovayse.Providers.CWP7` как эталонную реализацию: она показывает полный контракт клиента панели хостинга и то, как обрабатывать каждую команду провижининга (создание, приостановка, возобновление, удаление, смена пакета, смена пароля, вход).

## Манифест (`plugin.json`) {#the-manifest-plugin-json}

В корне каждого ZIP-архива плагина должен лежать `plugin.json`.

<SettingTable :settings="[
  { name: 'id', type: 'string', default: '', description: 'Уникальный идентификатор: строчные буквы, цифры и дефисы, не более 64 символов. Должен оставаться неизменным между версиями.' },
  { name: 'name', type: 'string', default: '', description: 'Отображаемое имя в менеджере плагинов и в разделе «Интеграции».' },
  { name: 'version', type: 'string', default: '', description: 'Строка семантической версии, например 1.0.0.' },
  { name: 'author', type: 'string', default: '', description: 'Имя автора, которое показывается на карточке плагина.' },
  { name: 'description', type: 'string', default: '', description: 'Однострочное описание, которое показывается на карточке плагина.' },
  { name: 'type', type: 'string', default: '', description: 'Тип плагина: Payment, Provisioning, Registrar, Identity, Fulfilment, Hosted или App.' },
  { name: 'category', type: 'string', default: '', description: 'Категория, по которой плагин группируется в менеджере плагинов.' },
  { name: 'color', type: 'string', default: '', description: 'Шестнадцатеричный цвет фона значка плагина, например #0369a1.' },
  { name: 'icon', type: 'string', default: '', description: 'Необязательное изображение плагина: путь внутри пакета к файлу SVG или PNG, например icon.svg или assets/icon.png. Строчные буквы, цифры, точка, дефис и подчёркивание, прямые слеши, без начального слеша и сегмента .., расширение .svg или .png, не более 256 КиБ. Если не указывать, показывается цветной блок с первой буквой плагина.' },
  { name: 'entryPoint', type: 'string', default: '', description: 'Полное имя класса, реализующего интерфейс плагина.' },
  { name: 'sdkVersion', type: 'string', default: '', description: 'Версия SDK в манифесте. Допустимое значение 1.0. Основная версия SDK сборки проверяется отдельно, и более старые основные версии отклоняются.' },
  { name: 'showInIntegrations', type: 'boolean', default: 'false', description: 'Если true, плагин появляется карточкой на экране «Интеграции».' },
  { name: 'integrationsCategory', type: 'string', default: '', description: 'Заголовок категории, под которым карточка появляется на экране «Интеграции».' },
  { name: 'fields', type: 'array', default: '[]', description: 'Настройки, которые показываются на странице интеграции, по порядку.' }
]" />

Файл `icon` проверяется при установке плагина и ещё раз при его загрузке. Путь, выходящий за пределы пакета, отсутствующий файл (`PLUGIN_ICON_MISSING`), файл сверх лимита размера, `.png`, не являющийся PNG, и SVG, который не является корректным XML или содержит скрипт, `foreignObject`, атрибут обработчика события (`onload`, `onclick`, ...), DOCTYPE или ссылку, выходящую за пределы изображения (`href`, не являющийся `#fragment`, `url(...)`, не являющийся `#fragment`, `@import`), отклоняются с `PLUGIN_ICON_INVALID`. Панель администратора показывает изображение там, где показывает цветной блок; хост отдаёт его по `GET /api/admin/plugins/{id}/icon` с `X-Content-Type-Options: nosniff` и политикой, блокирующей скрипты и любые загрузки.

Поля выше относятся к версии манифеста 1. Манифест без `manifestVersion` это версия 1; хост принимает `1` и `2` и отклоняет любое другое значение (`PLUGIN_MANIFEST_VERSION_UNKNOWN`). `version` должна быть строгой версией `major.minor.patch`. Версия манифеста 2 (`"manifestVersion": 2`) добавляет члены ниже.

### Члены манифеста v2 {#manifest-v2-members}

<SettingTable :settings="[
  { name: 'manifestVersion', type: 'number', default: '1', description: 'Укажите 2, чтобы использовать члены ниже.' },
  { name: 'settings', type: 'array', default: '[]', description: 'Типизированные настройки, которые заполняет администратор. Заменяет fields; если settings пуст, хост преобразует fields в settings.' },
  { name: 'capabilities', type: 'string[]', default: '[]', description: 'Что умеет плагин; показывается без его активации. Известные имена: storedCredentials (платёжный плагин, который также реализует IStoredCredentialPlugin) и endpoints (класс плагина реализует IPluginEndpoints; для плагина, не являющегося плагином App, хост отдаёт только его публичные обратные вызовы, см. раздел «Обратные вызовы извне»; плагин App обслуживается без такого объявления). Должно согласовываться с интерфейсами, которые класс действительно реализует.' },
  { name: 'hostAccess', type: 'string[]', default: '[]', description: 'Виды доступа к хостингу клиента, которые плагин запрашивает у хоста. Сейчас единственный вид это ssh. Страница «Плагины» панели администратора показывает каждый объявленный плагином вид.' },
  { name: 'locales', type: 'object', default: '{}', description: 'Тексты по коду языка, затем по ключу, например язык en с ключом menu.chat и текстом Chat. Ключ текста состоит минимум из двух частей, разделённых точкой, например menu.chat. Тексты en должны определять каждый ключ, который использует манифест.' },
  { name: 'api', type: 'object', default: '{}', description: 'Маршруты, которые хост перенаправляет плагину: api.client и api.admin, каждый со списком маршрутов.' },
  { name: 'ui', type: 'object', default: '{}', description: 'Что плагин добавляет в панели: ui.client и ui.admin (меню и страницы) и ui.slots (виджеты в слотах хоста).' },
  { name: 'tags', type: 'string[]', default: '[]', description: 'Необязательные ключевые слова, по которым реестр плагинов помогает людям находить плагин. Требует SDK 3.3.0 или новее. Не более 10 тегов, каждый из 2–30 строчных букв, цифр и дефисов без начального #, без повторов. Неверный список отклоняется с PLUGIN_MANIFEST_TAGS_INVALID.' },
  { name: 'externalOrigins', type: 'string[]', default: '[]', description: 'Необязательно. Веб-origin (схема, хост и порт, например https://example.com или https://*.example.com), на которые блок ссылки (link) в представлении плагина может указывать абсолютным адресом https и которые блок предпросмотра (preview) может встраивать во фрейм (только чистый origin https). Не указывайте, если плагин ссылается только на сайты, размещённые на этой установке: см. раздел о ссылках на сайты читателя ниже.' }
]" />

Теги это простые ключевые слова, которые записываются в манифесте так:

```json
"tags": ["billing", "card-payments", "invoices"]
```

Хост проверяет список при установке плагина и при его загрузке по тому же правилу, которое пакет `Hostpanel.Sdk.Packaging` применяет к вашей сборке: тег короче 2 или длиннее 30 символов, тег с заглавной буквой, пробелом, начальным `#` или любым символом, кроме букв, цифр и дефисов, повторяющийся тег или более 10 тегов отклоняется с `PLUGIN_MANIFEST_TAGS_INVALID`. Манифест без `tags` допустим.

### Поддерживаемые версии хоста и список изменений {#supported-host-versions-and-changelog}

Эти необязательные члены допустимы в манифесте любой версии. Требуется SDK 3.4.0 или новее.

<SettingTable :settings="[
  { name: 'minHostVersion', type: 'string', default: '', description: 'Самая ранняя версия Hostpanel, с которой работает плагин, строго в виде major.minor.patch, например 2.1.0. Не указывайте, если нижней границы нет.' },
  { name: 'maxHostVersion', type: 'string', default: '', description: 'Самая поздняя версия Hostpanel, с которой плагин заведомо работает, в том же формате и не ниже minHostVersion. Не указывайте, если верхней границы нет.' },
  { name: 'changelog', type: 'string', default: '', description: 'Краткий список изменений этой версии в Markdown, не более 4000 символов. Переносы строк сохраняются.' }
]" />

```json
"minHostVersion": "2.1.0",
"maxHostVersion": "2.9.0",
"changelog": "Adds refunds.
Fixes the renewal date."
```

Обе границы включительные и сравниваются по правилам семантических версий, поэтому предварительная версия, например `2.1.0-rc.1`, ниже `2.1.0`. Граница, которая не является допустимой версией, отклоняется с `PLUGIN_MANIFEST_HOST_VERSION_INVALID`, `minHostVersion` выше `maxHostVersion` отклоняется с `PLUGIN_MANIFEST_HOST_VERSION_RANGE_INVERTED`, а более длинный список изменений с `PLUGIN_MANIFEST_CHANGELOG_TOO_LONG`.

Когда администратор загружает пакет, панель администратора сначала показывает, что он запрашивает (каждый вид `hostAccess` и каждую возможность словами), список изменений и поддерживаемые версии, и устанавливает его только после подтверждения. Хост сравнивает диапазон со своей версией при любой установке: загрузке, установке из реестра и обновлении установленного плагина. Вне диапазона установка отклоняется с `PLUGIN_HOST_VERSION_OUT_OF_RANGE` (409), и ничего не распаковывается и не ставится в очередь.

Везде, где подпись, заголовок или описание могут быть текстом, пишите либо ключ из `locales`, либо простой текст. Значение, похожее на ключ (буквы, цифры, дефисы и подчёркивания в двух или более частях, разделённых точками), ищется в текстах `en` и отклоняется, если не определено (`PLUGIN_LABEL_KEY_MISSING`).

Каждая запись в `settings`:

<SettingTable :settings="[
  { name: 'key', type: 'string', default: '', description: 'Ключ настройки, читаемый через GetConfig(key).' },
  { name: 'label', type: 'string', default: '', description: 'Текст, показываемый для настройки.' },
  { name: 'type', type: 'string', default: '', description: 'Вид поля ввода: Text, Url, Select, Secret, Number, Boolean или Textarea. Secret хранится в зашифрованном виде и никогда не возвращается при чтении.' },
  { name: 'required', type: 'boolean', default: 'false', description: 'Если true, настройку нужно заполнить, прежде чем плагин можно будет включить.' },
  { name: 'description', type: 'string', default: '', description: 'Справочный текст под настройкой.' },
  { name: 'default', type: 'string', default: '', description: 'Значение, которое используется, пока администратор не задаст своё. Всегда записывается как текст, например 30 или true.' },
  { name: 'placeholder', type: 'string', default: '', description: 'Подсказка в пустом поле ввода.' },
  { name: 'min', type: 'number', default: '', description: 'Наименьшее значение настройки Number.' },
  { name: 'max', type: 'number', default: '', description: 'Наибольшее значение настройки Number.' },
  { name: 'options', type: 'array', default: '[]', description: 'Варианты настройки Select, каждый это объект с value (сохраняется) и label (показывается).' }
]" />

Каждая запись в `api.client` и `api.admin` это маршрут, который хост обслуживает по адресам `/api/client/plugins/{id}/...` и `/api/admin/plugins/{id}/ext/...`. Хост проверяет вход, разрешение вызывающего, CSRF и ограничение частоты запросов до запуска плагина:

<SettingTable :settings="[
  { name: 'method', type: 'string', default: '', description: 'Get, Post, Put, Patch или Delete.' },
  { name: 'path', type: 'string', default: '', description: 'Путь ниже префикса плагина: начальный слеш, затем строчные буквы, цифры, дефисы и подчёркивания либо сегменты {parameter}, например /invoices/{id}. Без пустого сегмента и сегмента ... Один и тот же метод и путь можно объявить один раз для каждой области.' },
  { name: 'permission', type: 'string', default: '', description: 'Только для клиентских маршрутов: имя клиентского разрешения или Any для любого вошедшего вызывающего. Маршрут администратора его не объявляет, потому что область администратора защищена одной политикой администратора (PLUGIN_ADMIN_PERMISSION_DECLARED).' },
  { name: 'rateLimit', type: 'string', default: 'Default', description: 'Политика ограничения частоты запросов: Default, Strict (дорогие или чувствительные маршруты, например отправка сообщений) или Heavy (редкая тяжёлая работа, например экспорт).' },
  { name: 'streaming', type: 'boolean', default: 'false', description: 'Если true, маршрут отвечает потоком server-sent events вместо одного ответа. Потоковый маршрут также подчиняется ограничению числа одновременных соединений.' }
]" />

В `ui.client` и `ui.admin` есть `menus` и `pages`. Каждая страница:

<SettingTable :settings="[
  { name: 'id', type: 'string', default: '', description: 'Идентификатор страницы, уникальный в пределах области: от 1 до 64 строчных букв, цифр, дефисов и подчёркиваний, начинается с буквы или цифры.' },
  { name: 'path', type: 'string', default: '', description: 'Путь страницы ниже префикса плагина в панели, по тем же правилам, что и путь api.' },
  { name: 'title', type: 'string', default: '', description: 'Заголовок страницы.' },
  { name: 'view', type: 'string', default: '', description: 'Путь внутри пакета к документу представления, который описывает страницу. Хост отображает его собственными компонентами, поэтому в браузере не выполняется код плагина.' },
  { name: 'permission', type: 'string', default: '', description: 'Только для клиентских страниц: имя клиентского разрешения или Any. На странице администратора не допускается.' }
]" />

Каждая запись меню:

<SettingTable :settings="[
  { name: 'id', type: 'string', default: '', description: 'Идентификатор записи, уникальный в пределах плагина.' },
  { name: 'label', type: 'string', default: '', description: 'Текст, показываемый в меню.' },
  { name: 'page', type: 'string', default: '', description: 'Идентификатор страницы той же области, которую открывает запись.' },
  { name: 'icon', type: 'string', default: '', description: 'Имя значка: от 1 до 64 строчных букв, цифр и дефисов. Не указывайте, чтобы использовать значок панели по умолчанию.' },
  { name: 'order', type: 'number', default: '0', description: 'Позиция в меню, меньшие значения идут первыми.' }
]" />

Каждая запись в `ui.slots` помещает виджет в слот, который предлагает хост. Список слотов версионируется; хост предлагает `client.dashboard.cards` (без контекста), `client.service.tabs` (ключ контекста `serviceId`), `admin.client.panels` (`clientId`) и `admin.service.panels` (`serviceId` и `clientId`). Слот, которого нет в списке, или ключ контекста, которого слот не предлагает, отклоняется.

<SettingTable :settings="[
  { name: 'id', type: 'string', default: '', description: 'Идентификатор виджета, уникальный в пределах слота: строчные буквы, цифры, дефисы и подчёркивания.' },
  { name: 'slot', type: 'string', default: '', description: 'Идентификатор слота, один из четырёх выше.' },
  { name: 'view', type: 'string', default: '', description: 'Путь внутри пакета к документу представления, который описывает виджет.' },
  { name: 'title', type: 'string', default: '', description: 'Заголовок, показываемый вместе с виджетом.' },
  { name: 'order', type: 'number', default: '0', description: 'Позиция среди виджетов слота, меньшие значения идут первыми.' },
  { name: 'permission', type: 'string', default: '', description: 'Только для клиентских слотов: имя клиентского разрешения или Any.' },
  { name: 'productIds', type: 'number[]', default: '[]', description: 'Продукты, к которым привязан виджет; он показывается только там, где услуга слота относится к одному из них. Пустой список показывает его для каждого продукта. Администратор может изменить привязку на странице «Плагины».' },
  { name: 'contextKeys', type: 'string[]', default: '[]', description: 'Контекст, который виджету нужен от слота. Хост проверяет ключи для вызывающего и передаёт плагину только их.' }
]" />

### Обратные вызовы извне {#callbacks-from-outside}

Платёжный шлюз обращается к серверу панели от сервера к серверу, без вошедшего пользователя: вебхук карты, предварительная проверка и подтверждение, URL результата банка. Плагин, реализующий `IPluginEndpoints`, может принимать их через публичную дверь без какого-либо кода шлюза в хосте. Это доступно плагину любого типа: платёжный плагин указывает `"capabilities": ["endpoints"]` в своём манифесте, а его класс реализует `IPluginEndpoints` наряду с `IPaymentPlugin`; плагину App возможность не нужна. Эта возможность открывает только эту дверь. Маршруты `api.client` и `api.admin`, страницы, меню и слоты плагина, не являющегося плагином App, не обслуживаются, что бы он ни объявлял. Манифест, где указан `endpoints`, а класс не реализует `IPluginEndpoints`, отклоняется при загрузке с `PLUGIN_ENDPOINTS_NOT_IMPLEMENTED`, а плагин, который её не указал, отвечает на свои обратные вызовы тем же `404 PLUGIN_ROUTE_NOT_FOUND`, что и неизвестный плагин.

Объявите каждый обратный вызов в `api.callbacks`, списке маршрутов с теми же правилами `method` и `path`, что и у `api.client` (`permission` не допускается, как и `streaming`; `rateLimit` игнорируется, потому что у двери собственный лимит):

```json
{
  "api": {
    "callbacks": [
      { "method": "Post", "path": "/webhook" },
      { "method": "Post", "path": "/confirm/{step}" }
    ]
  }
}
```

Хост обслуживает их по адресу `/api/plugins/{id}/callback/...`, поэтому второй маршрут выше это `POST /api/plugins/{id}/callback/confirm/result`. Передайте этот адрес шлюзу.

Что делает хост:

- Он отвечает только на объявленный обратный вызов установленного и включённого плагина. Неизвестный плагин, выключенный или ненастроенный, необъявленный путь и неверный метод отвечают одинаково: `404 PLUGIN_ROUTE_NOT_FOUND`, поэтому дверь никогда не выдаёт, что установлено. Маршруты `api.client` и `api.admin` через неё недоступны.
- Здесь нет ни входа, ни заголовка CSRF. Плагин получает `call.Area == PluginArea.Callback`, ни ссылки на клиента, ни разрешений, фиксированную ссылку вызывающего `callback` и общее хранилище плагина.
- Тело передаётся байт в байт, никогда не разбирается и не сериализуется заново, поэтому подпись по нему по-прежнему проверяется. В `call.Headers` лежит список разрешённых заголовков запроса точно в том виде, как они получены (см. ниже), а в `call.Query` строка запроса.
- Обратный вызов может ответить через `PluginReply.Raw(status, body, contentType)` для шлюзов, ожидающих простой ответ вроде `OK` как `text/plain`: байты, статус и тип содержимого записываются как заданы, с `X-Content-Type-Options: nosniff`. «Сырой» ответ допускается только в обратном вызове (на клиентском или административном маршруте это ошибка плагина, `PLUGIN_FAILED`), его тип содержимого должен быть `text/plain`, `application/json`, `application/xml` или `application/x-www-form-urlencoded` (всё остальное, включая разметку и скрипты, это `PLUGIN_FAILED`), а его тело ограничено `PluginApi__MaxBodyBytes`. Каждый «сырой» ответ также несёт `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`.
- В `call.Headers` попадают только такие заголовки запроса: `Content-Type`, `Content-Length`, `User-Agent`, `Signature`, `Digest`, `Content-Digest`, `Date`, `Idempotency-Key`, `Webhook-Id`, `Webhook-Timestamp`, `Webhook-Signature` и любой заголовок `X-*`, кроме `X-Forwarded-*`, `X-Real-IP` и собственных заголовков хоста (`X-Language`, `X-Requested-With`, `X-Logout-Key`, `X-Service-Key`, `X-Renewed-Access-Token`). `Cookie`, `Authorization`, `Proxy-Authorization` и `Host` никогда не передаются. Если ваш шлюз подписывает заголовок, которого нет в этом списке, отправляйте его как заголовок `X-*` или помещайте в тело.
- Тело ограничено `PluginApi__MaxBodyBytes` (413 `PLUGIN_REQUEST_TOO_LARGE` сверх лимита), а каждый запрос к двери учитывается по адресу вызывающего относительно `RateLimit__PluginCallback` (429 сверх лимита).
- Сбой записывается в журнал только с идентификатором плагина, никогда с заголовком, запросом или телом.

Что должен сделать плагин: проверить вызов самостоятельно (подпись шлюза, контрольную сумму или общий секрет), прежде чем доверять чему-либо в нём, используя секрет из собственных настроек. Отвечайте `PluginReply.Problem(400, ...)` или `403` на вызов, который не проходит проверку, и ответом об успехе на тот, который проходит; делайте работу идемпотентной, потому что шлюзы доставляют одно и то же событие более одного раза. Ничто в хосте не гарантирует подлинность вызова.

Манифест, нарушающий одно из этих правил, отклоняется при установке и при запуске с кодом ошибки манифеста `PLUGIN_*`, называющим проблемный член (`PLUGIN_CALLBACK_INVALID` для обратного вызова, который объявляет разрешение или работает потоком, `PLUGIN_ROUTE_PATH_INVALID` и `PLUGIN_ROUTE_DUPLICATE` для его пути и повторов). Точные типы смотрите в `backend/src/Hostpanel.Sdk/Plugins/Manifest`.

### Возможности (capabilities) {#capabilities}

Манифест версии 2 может перечислять `capabilities`, каждая это имя из `PluginCapabilities` SDK:

| Возможность | Допустима для | Класс плагина должен также реализовать |
|------------|------------|--------------------------------------|
| `storedCredentials` | только `Payment` | `IStoredCredentialPlugin` |
| `endpoints` | любой тип | `IPluginEndpoints` |

Плагин `App` отвечает на маршруты и показывает страницы, меню и слоты без каких-либо объявлений. Любой другой тип, объявляющий
маршруты `api` или меню, страницы или слоты `ui`, должен объявить `endpoints`, иначе пакет отклоняется при установке и пропускается
при запуске (`PLUGIN_ENDPOINTS_NOT_DECLARED`). Так один плагин, например плагин `Fulfilment`, который продаёт кредиты,
может дать клиенту ещё и свой экран. Неизвестное имя, имя, объявленное дважды, или `storedCredentials` для типа, отличного от
`Payment`, отклоняются так же (`PLUGIN_CAPABILITY_NOT_ALLOWED`).

Объявленная возможность сверяется с классом при активации плагина. Если класс не реализует
интерфейс, обещанный возможностью, плагин не активируется ни для чего, включая вызовы собственного типа: платёжный
плагин, объявивший `storedCredentials` без реализации `IStoredCredentialPlugin`, не принимает платежи, а
плагин исполнения (fulfilment), объявивший `endpoints` без реализации `IPluginEndpoints`, не получает вызовов исполнения. В
журнале администратора названы плагин и возможность.

Каждая запись в `fields`:

<SettingTable :settings="[
  { name: 'key', type: 'string', default: '', description: 'Ключ настройки, читаемый через GetConfig(key).' },
  { name: 'label', type: 'string', default: '', description: 'Подпись рядом с полем на странице интеграции.' },
  { name: 'type', type: 'string', default: 'text', description: 'Тип поля: text, secret, select или textarea.' },
  { name: 'required', type: 'boolean', default: 'false', description: 'Если true, страница интеграции показывает «Не настроено», пока это поле не заполнено.' }
]" />

## Состояние представления {#view-state}

Представление страницы или слота (`{ "schema": 1, "blocks": [...] }`) может реагировать на собственные данные без перезагрузки. Это делают три необязательных
члена; всем нужен SDK 2.2.0 или новее, а схема представления остаётся 1.

**Условие** это одна форма, используемая в `visibleWhen` и в `followUp.until`, ровно с такими тремя членами:

```json
{ "route": { "method": "get", "path": "/services/{serviceId}/site" }, "field": "site.status", "equals": ["ready", "live"] }
```

- `route` это маршрут `GET`, объявленный в вашем манифесте в той же области, что и страница или слот.
- `field` это путь через точки в JSON-ответе (буквы, цифры, `_` и `-` в каждом сегменте, не более 200 символов).
- `equals` это одно значение JSON (строка, число, `true`, `false`, `null`) или список из 1–20 таких значений. Условие выполняется,
  когда поле строго равно одному из них: `"1"` не совпадает с `1`.

**`visibleWhen`** ставится на любой блок, включая действия заголовка и блоки внутри вкладок. Блок показывается, только когда
условие выполняется; пока оно читается, при сбое чтения или когда поля нет, блок остаётся скрытым. Оно читается снова,
когда обновляется собственный `id` блока. Скройте форму создания, когда сайт уже существует:

```json
{
  "kind": "form",
  "id": "create-site",
  "visibleWhen": { "route": { "method": "get", "path": "/services/{serviceId}/site" }, "field": "exists", "equals": false },
  "fields": [
    { "key": "name", "label": "site.name", "type": "text", "required": true },
    { "key": "description", "label": "site.description", "type": "textarea" }
  ],
  "submit": {
    "route": { "method": "post", "path": "/services/{serviceId}/site" },
    "label": "site.create",
    "refresh": ["create-site", "site"],
    "resetOnSuccess": true
  }
}
```

**Как часто читается маршрут.** Страница это один снимок ваших данных. Каждый блок, условие и карточка, называющие на странице один и тот же
маршрут `GET`, обслуживаются одним запросом, а панель отправляет новый только тогда, когда страница обновляется (этого потребовали
действие, форма, ход беседы или повторная проверка) после любого вызова, не являвшегося чтением, или когда
ход беседы начинается или заканчивается. Блок, появившийся позже, берёт ответ, который страница уже хранит, поэтому страница, называющая
один маршрут состояния в десяти местах, стоит одного запроса на обновление, а не десяти. Панель не хранит чтение дольше этого момента, ни одно
старше десяти секунд и ни одно с другой страницы. Маршрут, читаемый
несколькими блоками, всё равно читается один раз на каждое обновление, поэтому делайте ответ на него дешёвым.

**`resetOnSuccess`** принимает `true` или `false` и допускается только внутри `submit` формы. После успешной отправки каждое поле
снова становится пустым.

**`followUp`** ставится на блок действия или внутрь `submit` формы, ровно с `refresh` и `until`. После
успешного вызова (и его обычного `refresh`) панель читает `until.route` снова и снова, каждый раз обновляя перечисленные блоки,
пока условие не выполнится (тогда ещё один раз), пока не кончатся попытки или пока читатель не покинет страницу. Неудавшееся
чтение считается попыткой. Наблюдение живёт дольше кнопки, которая его запустила, поэтому кнопка, скрывающая себя после завершения своей
работы (`visibleWhen` на тех же данных), его не останавливает. Предложение, которым ответило действие, убирается, когда
условие выполняется, поэтому строка вроде «публикуется» не переживает публикацию. Покажите результат публикации без
перезагрузки:

```json
{
  "kind": "action",
  "label": "site.publish",
  "route": { "method": "post", "path": "/services/{serviceId}/site/publish" },
  "followUp": {
    "refresh": ["publish-result"],
    "until": {
      "route": { "method": "get", "path": "/services/{serviceId}/site" },
      "field": "publish.state",
      "equals": ["done", "failed"]
    }
  }
}
```

Оба примера предполагают, что в представлении есть также блоки с идентификаторами `site` и `publish-result`, а манифест объявляет
три маршрута в своём блоке `api` и ключи подписей в `locales.en`:

```json
{
  "locales": {
    "en": {
      "site.name": "Site name",
      "site.description": "Description",
      "site.create": "Create site",
      "site.publish": "Publish"
    }
  }
}
```

`refresh` перечисляет от 1 до 20 `id` блоков, каждый из которых есть в том же представлении. Давайте каждому блоку с `id` свой собственный, в любом
контейнере (вкладка, панель, заголовок): панель считает блоки с общим `id` одним блоком, поэтому обновление затронет все
их, а новая повторная проверка одного завершит выполняющуюся повторную проверку другого. Представление, в котором один `id` у двух блоков,
отклоняется при установке (`PLUGIN_VIEW_BLOCK_ID_REPEATED`); плагин, уже установленный с таким представлением, всё равно загружается,
хост пишет предупреждение в журнал, а панель рисует каждый блок, но действует на блоки с одним `id` как на один. `id` вкладки или источника предпросмотра это отдельное имя и может совпадать с `id`
блока. Как часто и как долго панель опрашивает, решает
хост, а не вы: каждые 3 секунды, не более 20 раз (около минуты). Долгая задача должна сообщать о своём состоянии
на маршруте `until`, а не ожидать больше времени.

**`bodySource`** ставится на блок действия в клиентской области и заставляет кнопку отправлять то, что видел читатель. Он содержит
ровно `route` типа `GET` и от 1 до 10 `fields`, каждое из которых это член тела (имя в camelCase, не член подписи вроде `title`)
и путь через точки, из которого он читается. Без него действие отправляет `{}`, как раньше.

```json
{
  "kind": "action",
  "id": "publish",
  "label": "site.publish",
  "route": { "method": "post", "path": "/services/{serviceId}/site/publish" },
  "labelSource": { "route": { "method": "get", "path": "/services/{serviceId}/site" }, "field": "publishLabel" },
  "bodySource": {
    "route": { "method": "get", "path": "/services/{serviceId}/site" },
    "fields": { "expectedVersion": "publishVersion" }
  }
}
```

Действию нужны `id` и маршрут `POST`, `PUT` или `PATCH`, а `labelSource` должен читать тот же маршрут: панель
делает одно чтение для обоих, поэтому кнопка отправляет значение, которое написано на ней. Кнопка отключена, пока каждый член не будет
прочитан как строка, число или булево значение. Тело фиксируется в момент нажатия. Отклонённый вызов заново читает данные и блоки
`refresh` действия, поэтому отвечайте на конфликт своим предложением; ваш маршрут, а не панель, решает, актуально ли ещё
значение. Действие карточки результата в событии чата несёт буквальное `body` той же формы; событие с
некорректным телом теряет это действие.

Представление, нарушающее эти правила, отклоняется при установке и пропускается при запуске: `PLUGIN_VIEW_ACTION_BODY_INVALID` (неверный
`bodySource` или `bodySource` не на своём месте), `PLUGIN_VIEW_CONDITION_INVALID` (неверное
условие, маршрут условия не `GET`, `visibleWhen` вне блока), `PLUGIN_VIEW_FOLLOW_UP_INVALID` (неверный
`followUp` или `followUp` не на своём месте, идентификатор в `refresh`, которого нет ни у одного блока, `resetOnSuccess` не булево или не внутри
`submit`) и `PLUGIN_VIEW_ROUTE_UNDECLARED` для маршрута условия, не объявленного в манифесте.

Две проверки были добавлены, когда плагины уже были установлены, поэтому они применяются только при **установке**: `PLUGIN_VIEW_BLOCK_ID_REPEATED`
(один `id` на нескольких блоках) и `PLUGIN_VIEW_INVALID` для члена отображения с неверным типом (`compact` заголовка,
`titleSource` или `subtitleSource`, `labelSource` действия, `note` предпросмотра, `display` таблицы). Плагин, который
уже установлен с таким представлением, не удаляется при обновлении хоста: он загружается, а хост пишет по одному предупреждению на каждую ошибку
в свой журнал. Исправьте их в следующей версии, потому что загрузка её будет отклонена, пока вы этого не сделаете.

## Рабочие экраны: компоновка, разделение и предпросмотр {#working-screens-layout-split-and-preview}

Требуется SDK 3.2.0 или новее; схема представления остаётся 1.

Клиентская страница может задать в `plugin.json` `"layout": "workspace"` (`PluginPageLayouts`): во всю ширину, без бокового меню услуги,
с узкой ссылкой назад к услуге (какая именно услуга, определяет параметр пути `{serviceId}`). Отсутствие значения означает `standard`. Страница администратора
не принимает компоновку. Неверное значение даёт `PLUGIN_PAGE_LAYOUT_UNKNOWN`.

`split` размещает две панели рядом, на телефоне друг под другом, и никогда не вкладывается в другой `split` (`PLUGIN_VIEW_SPLIT_INVALID`):

```json
{ "kind": "split", "panes": [ { "blocks": [ { "kind": "chat", "send": { "method": "post", "path": "/chat" } } ] },
                              { "blocks": [ { "kind": "preview", "sources": [ ] } ] } ] }
```

`preview` показывает один адрес в изолированном фрейме, только на клиентской странице:

```json
{
  "kind": "preview",
  "id": "site-preview",
  "size": "desktop",
  "sources": [
    { "id": "preview", "label": "site.preview", "route": { "method": "get", "path": "/services/{serviceId}/site" }, "field": "previewUrl" },
    { "id": "live", "label": "site.live", "route": { "method": "get", "path": "/services/{serviceId}/site" }, "field": "liveUrl" }
  ]
}
```

До 5 `sources`, каждый ровно с `id`, `label`, маршрутом `GET` `route` и `field` с путём через точки; `size` равен `desktop` или `phone`, и
читатель может переключать оба. Неверная форма или предпросмотр в слоте либо на странице администратора даёт `PLUGIN_VIEW_PREVIEW_INVALID`.

**Выбор элементов читателем.** Предпросмотр может объявить `selection`, а чат в том же документе `"selection": true`; оба выключены, когда не указаны:

```json
"selection": {
  "sources": ["preview"],
  "route": { "method": "post", "path": "/services/{serviceId}/site/selection" },
  "release": { "route": { "method": "get", "path": "/services/{serviceId}/site/state" }, "field": "previewRelease" }
}
```

`sources` это идентификаторы источников, в которых можно выбирать: рабочий сайт не включайте. Страница во фрейме выполняет ваш собственный скрипт и общается с панелью через `postMessage`: `{ "type": "hostpanel.preview.selection", "version": 1, "release": "...", "event": "ready" | "toggle" | "exit", "id": "..." }`, а от панели получает `{ type, version, "event": "state", "active": bool, "selected": [ids] }`. Кнопка выбора появляется только после `ready`. `id` это путь через точки из букв и цифр, не более 128 символов и не более 20 на один выбор. **Сообщение это заявление, и ему никогда не доверяют:** панель принимает его только из окна собственного фрейма, строго в такой форме, с названием релиза, о котором сообщает ваш маршрут, из источника из списка, а `toggle` только пока режим включён; любой скрипт во фрейме всё равно может отправить такое сообщение. Поэтому ваш `route` получает `{ "releaseId", "ids" }` и отвечает `{ "items": [{ "id", "label" }] }` с идентификаторами, которые он действительно знает, а панель рисует только эти подписи. Следующее сообщение чата несёт `"selection": { "releaseId", "ids" }`, которое ваш сервер должен проверить ещё раз; не берите никакой текст из фрейма. Считайте `ids` и `releaseId` недоверенным вводом в обоих маршрутах: они приходят из скрипта страницы, что бы ни проверила панель до этого, поэтому проверяйте их по своим данным и никогда не используйте как путь, фрагмент запроса или текст для модели без такой проверки. Выбор очищается при смене релиза. Затем панель игнорирует фрейм, пока он снова не скажет `ready`, поэтому **страница во фрейме должна снова отправить `ready` после смены релиза** (новый документ делает это при загрузке; страница, обновляющаяся на месте, должна отправить его сама), иначе кнопка выбора остаётся скрытой. «Повторить» (`resend`) отправляет тот же выбор, пока его релиз находится в предпросмотре; когда релиз сменился, панель ничего не отправляет, сообщает читателю, что страница изменилась и нужно выбрать заново, и оставляет текст в поле сообщения, потому что сообщение без своего выбора охватило бы больше, чем выбрал читатель. Панель читает релиз только тогда, когда читает блок, поэтому **ваш маршрут чата должен отклонять `releaseId`, которого больше нет в предпросмотре, ответом с проблемой и до любых изменений**: проверка панели это любезность, а держится ваша. Один предпросмотр на документ может объявлять `selection`, поскольку заявление не говорит, какой предпросмотр его сделал. Неверная форма, второй предпросмотр с выбором или выбор в чате без выбора в предпросмотре даёт `PLUGIN_VIEW_SELECTION_INVALID`.

**Ссылки на собственные сайты читателя.** Плагин публикуется один раз и работает на каждой установке, поэтому не может перечислить
домены каждой. Блок ссылки (link) (и ссылка в карточке чата) может также указывать на origin `https` домена из собственных
активных услуг хостинга читателя по тому же правилу, что и предпросмотр ниже: только для продукта, который оператор привязал к
виджетам слотов вашего плагина, в том виде, как домен был введён при заказе (с именем `www`), только для читателя, которому разрешено видеть услуги, и никогда для
собственного origin панели. Тот же список доступен представлению виджета слота. Поэтому ссылке «открыть рабочий сайт» или «открыть предпросмотр стенда» не
нужна запись в `externalOrigins` для сайтов, размещённых на этой установке; перечисляйте origin только для адреса вне её. У
панели администратора нет учётной записи читателя, поэтому там действуют только origin из манифеста. Ваш плагин не может ничего из этого изменить.

**Что можно встраивать во фрейм.** Адрес, который возвращает маршрут, показывается, только когда его origin это один из
`externalOrigins` вашего манифеста (чистый origin `https`) или домен из собственных активных услуг хостинга читателя для продукта, который
оператор привязал к виджетам слотов вашего плагина, в том виде, как домен услуги был введён при заказе (с именем `www`, и только для
читателя, которому разрешено видеть услуги; `productIds` из вашего манифеста не учитываются, отсутствие привязки означает отсутствие доменов, а собственный
origin панели никогда не входит в список). Всё остальное ничего не показывает, без сообщения, и ваш плагин не может
это изменить: хост пишет список для каждого читателя, и тот же список сужает `frame-src` страницы, поэтому браузер отклоняет
и любой другой фрейм.

**Фрейм имеет `sandbox="allow-scripts allow-forms"`.** Скрипты выполняются, но у фрейма непрозрачный origin: нет ни cookie, ни
хранилища самого сайта, нет всплывающих окон, нет навигации верхнего уровня, нет доступа к панели. Сайт, которому нужна собственная сессия,
будет выглядеть там как без входа, а сайт, запрещающий встраивание (`X-Frame-Options`, `frame-ancestors`), остаётся пустым, поэтому отдавайте
предпросмотр без этих заголовков.

На машине разработчика `PLUGIN_PREVIEW_DEV_HTTP_ORIGIN=http://localhost:8088` в `docker/.env` (файл compose для разработки сопоставляет
её с `PluginPreview__DevelopmentHttpOrigins__0`, а `PLUGIN_PREVIEW_DEV_HTTP_ORIGIN_2` с `__1` для второго адреса;
пусто означает отсутствие) разрешает точный origin `http` на хосте обратной петли. Вне окружения Development хост отказывается запускаться, если она задана.

## События чата {#chat-events}

Требуется SDK 3.2.0 или новее. Потоковый маршрут блока `chat` может отправлять эти события (`PluginChatEvents`) наряду с текстом
ответа, чтобы читатель видел, что произошло, не выискивая это в абзаце. Текст простой и уже на языке читателя. События `error` и `done`
остаются за хостом. Сам текст ответа (`delta`, `message`, строка ассистента в `history`) может
использовать небольшое подмножество Markdown: абзацы, заголовки `#`, списки `-` и `1.`, блоки кода, `**жирный**`, `*курсив*` и
`` `code` ``. HTML и ссылки Markdown в нём остаются простым текстом; ссылку отправляйте событием `link`.

```json
{ "event": "step", "data": { "id": "build", "label": "Building the site", "state": "running" } }
{ "event": "step", "data": { "id": "build", "label": "Building the site", "state": "done" } }
{ "event": "result", "data": { "tone": "success", "title": "Published", "text": "Your site is live.", "actions": [
  { "label": "Open site", "href": "https://example.com/" },
  { "label": "Roll back", "route": { "method": "post", "path": "/services/{serviceId}/site/rollback" }, "confirm": "Roll back?" } ] } }
{ "event": "issue", "data": { "text": "The build failed: the theme is missing." } }
```

- `step` это одна строка списка в ответе; повторная отправка её `id` (или её `label`, если нет `id`) обновляет строку.
  `state` равен `running`, `done` или `failed`.
- `result` это карточка. `tone` равен `info` (по умолчанию), `success`, `warning` или `danger`; нужен хотя бы один из `title` и `text`.
  Не более 5 `actions`, каждое с `label` и ровно одним из `route` (один из ваших собственных, вызываемый через хост, поэтому
  необъявленный маршрут или отсутствующее разрешение отклоняются как обычно) или `href` (по правилу блока ссылки).
  Карточка остаётся в беседе после того, как то, что она предлагает, изменилось, поэтому у действия может быть `visibleWhen`: то же
  условие, что у блока представления, кроме того, что в событии список значений записывается как `equalsAny` (запись C# не может иметь член
  с именем `equals`): `{ "route": { "method": "get", "path": "/services/{serviceId}/site" }, "field": "publishVersion",
  "equalsAny": [5] }`. Маршрут это ваш собственный маршрут `GET`, который страница может заполнить, `field` это путь через точки, а
  `equalsAny` это от одной до 20 строк, чисел, булевых значений или `null`; действие показывается, только пока поле строго равно одному
  из них. Панель читает его при отрисовке карточки и снова всякий раз, когда блок, названный в `refresh` чата, читается
  заново (после хода и после действия карточки), и скрывает действие, пока оно читается, при сбое чтения и
  когда условие не выполняется. Это относится к действию с маршрутом, с `href` и с `resend` в равной мере. Без него действие
  показывается всегда. Условие, которое панель не может прочитать, убирает действие, поскольку кнопка, которая должна скрываться, никогда не должна показываться.
  Актуальность вызова по-прежнему определяет ваш маршрут: условие это любезность по отношению к читателю, а не проверка.
  Строка, в которой не осталось видимых действий, тоже скрывается.
- `issue` это ваш собственный сбой в тоне danger. Поток продолжается; завершите его, когда закончите.

Панель игнорирует событие, которое не может прочитать. **Stop** отменяет поток: срабатывает токен вашего перечислителя (`EnumeratorCancellation`),
и больше ничего не отправляется. Stop ничего не откатывает, поэтому следите за токеном и оставляйте свою работу в согласованном состоянии.

## Упаковка {#package-it}

Структура папки внутри ZIP:

```
plugin.json                                ← at the root
MyCompany.Hostpanel.Plugins.MyGateway.dll  ← your assembly
plugin.sig                                 ← signature file (required on production servers)
```

**Именование сборок.** Префикс `Innovayse.Hostpanel.` принадлежит хосту, поэтому пакет, содержащий DLL с таким префиксом, отклоняется. Единственное исключение это пространство имён плагинов `Innovayse.Hostpanel.Plugins.<Name>` (например, `Innovayse.Hostpanel.Plugins.Stripe.dll`): так называются официальные плагины, и оно всегда допускается в пакете. Граница определяется точкой после `Plugins`: `Innovayse.Hostpanel.PluginsX.dll` и `Innovayse.Hostpanel.Plugins.dll` по-прежнему считаются именами хоста и отклоняются. Ни одна сборка хоста никогда не называется в пространстве имён плагинов (это охраняет архитектурный тест), поэтому плагин не может подменить ни одну из них. Сторонние плагины сохраняют свой префикс, например `MyCompany.MyGateway`.

**Не** включайте сборку SDK (`Hostpanel.Sdk.dll`) и любую сборку, которую хост уже предоставляет. Сервер отклоняет пакеты, содержащие сборку хоста (`PLUGIN_HOST_ASSEMBLY_REFUSED`). Давайте своим сборкам имена, которые не использует ни один другой плагин: пакет также отклоняется (`PLUGIN_ASSEMBLY_CONFLICT`), если имя одного из его файлов DLL или EXE, сравниваемое без учёта регистра, уже поставляется другим установленным или подготовленным плагином, потому что загрузчик привязал бы оба плагина к одной копии. Правило охватывает каждую DLL в пакете, включая общие сторонние библиотеки (библиотека JSON или HTTP, которую поставляют два плагина, тоже считается): если другой установленный плагин уже поставляет библиотеку с таким именем, выберите другое имя сборки или объедините библиотеку со своей сборкой. Обновление плагина с тем же `id` никогда не конфликтует с самим собой. Дубликат `id` плагина пропускается при запуске без ошибки: побеждает первая загруженная версия (встроенные плагины загружаются раньше загруженных вручную).

### Подпись пакета {#sign-the-package}

Пакеты, загружаемые вне окружений Development и Testing, должны быть подписаны. Чтобы пропустить проверки подписи, откройте **Плагины** в панели администратора и включите **Разрешить плагины без подписи**. Инструмент `PluginSigner` входит в репозиторий Hostpanel (`backend/tools/Innovayse.Hostpanel.PluginSigner`), а не в SDK. Запускайте его на **распакованной папке** до упаковки в ZIP. Инструмент использует `Hostpanel.Sdk.Packaging` (3.1.0) для формата подписи, поэтому подпись, которую он пишет, проверяется тем же кодом, с которым устанавливает хост, а ваш конвейер может вызывать `PluginSigner` и `PluginSignatureValidator` из этого пакета, чтобы проверить подписанную папку перед релизом.

Создайте пару ключей. Файл ключа это закрытый ключ в кодировке base64; храните его вне любого рабочего дерева git (инструмент отказывается подписывать, если файл ключа находится внутри него):

```bash
dotnet run --project backend/tools/Innovayse.Hostpanel.PluginSigner -- keygen my-key.key
```

Подпишите папку плагина:

```bash
dotnet run --project backend/tools/Innovayse.Hostpanel.PluginSigner -- sign ./MyCompany.Hostpanel.Plugins.MyGateway my-key.key my-key-id
```

Команда записывает `plugin.sig` в папку. Упакуйте папку в ZIP после подписи.

Оператор должен добавить соответствующий открытый ключ в `PluginSigning:OperatorKeys` в окружении своего развёртывания, иначе пакет будет отклонён с `PLUGIN_SIGNATURE_KEY_UNKNOWN`. Обновляя плагин, подпишите новую папку заново перед повторной упаковкой: обновление перепроверяет подпись, и сервер отклонит устаревший или отсутствующий `plugin.sig`.

## Тестирование {#test-it}

Пишите модульные тесты своего шлюза с подменённым HTTP-клиентом, чтобы ни одному тесту не требовался настоящий платёжный провайдер.

Для ручного сквозного теста:

<Steps>
<li>

Запустите локальный стек Hostpanel (см. [Локальная разработка](/ru/developers/local-dev)).

</li>
<li>

Соберите свою библиотеку классов в Release, подпишите папку с результатом через `PluginSigner`, упакуйте в ZIP и загрузите ZIP через **Настройки → Менеджер плагинов → Установить плагин**. В окружениях Development и Testing неподписанные пакеты загружаются без какого-либо переключателя. На других серверах откройте **Плагины** в панели администратора и включите **Разрешить плагины без подписи**, чтобы пропустить проверку подписи.

</li>
<li>

Перезапустите сервер с помощью баннера «Требуется перезапуск».

</li>
<li>

Перейдите в **Интеграции**, найдите карточку своего плагина, нажмите **Настроить →**, введите тестовые учётные данные, сохраните и включите.

</li>
<li>

Создайте тестового клиента, выставьте счёт и оплатите его. Убедитесь, что платёж появился в **Журнале транзакций шлюза**. Если проверка подписи не проходит, загрузка отклоняется с `PLUGIN_UNSIGNED`.

</li>
</Steps>

## Публикация для других {#share-it}

Используйте семантическое версионирование (`major.minor.patch`). Добавляйте список изменений, чтобы операторы знали, что изменилось. Операторы устанавливают плагин через менеджер плагинов, загружая ZIP.

<Callout type="danger">
Никогда не вставляйте действующие учётные данные (ключи API, секреты) в скриншот, issue или pull request. Используйте тестовые учётные данные или замаскируйте их.
</Callout>

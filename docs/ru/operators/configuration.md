---
title: Справочник по настройке
description: Все настройки .env, которые читает Hostpanel, со значениями по умолчанию, признаками обязательности и уровнем compose, к которому относится каждая настройка.
---

# Справочник по настройке {#configuration-reference}

## Как работает настройка {#how-configuration-works}

Настройки находятся в `docker/.env.example`, который копируется в `.env` рядом с файлом compose. Файл compose читает значения из `.env` и передаёт их службам как переменные окружения. Некоторые настройки доходят до служб под другим именем: например, `STACK_NETWORK_SUBNET` попадает в API как `RateLimit__KnownNetworks__0`.

Не все настройки находятся в `.env.example`. Часть настроек ограничения частоты запросов и нумерации заказов задаётся прямо на службе API в переопределяющем файле compose (полный список см. в разделах [Обратный прокси и доверенные прокси](#reverse-proxy-and-trusted-proxies) и [API и порты](#api-and-ports)).

## Файлы compose {#compose-files}

Каждый файл compose предназначен для своей цели развёртывания. В таблицах настроек ниже указано, к какому уровню относится каждая настройка.

### Как значение попадает в API {#how-a-value-reaches-the-api}

Одна и та же настройка может находиться в разных местах в зависимости от того, есть ли она в `.env.example` или задана в переопределяющем файле compose. Вот пример на настройках доверенных прокси и заказов:

::: code-group

```bash [.env]
# Most settings live here:
RATE_LIMIT_TRUSTED_PROXY_COUNT=1
STACK_NETWORK_SUBNET=172.30.0.0/24
```

```yaml [переопределение compose]
# Settings not in .env are set directly on the service:
services:
  hostpanel-api:
    environment:
      Orders__NumberPrefix: "ORD-"
      Orders__NumberDigits: 4
```

```text [Как это видит API]
# .env variable                   → API configuration key
STACK_NETWORK_SUBNET              → RateLimit__KnownNetworks__0
RATE_LIMIT_TRUSTED_PROXY_COUNT    → RateLimit__TrustedProxyCount
Orders__NumberPrefix (override)   → Orders__NumberPrefix (no rename)
```

:::

| Файл | Назначение | Уровень |
|---|---|---|
| `docker-compose.hub.yml` | Самостоятельная установка на своём сервере, используется `install.sh` | hub |
| `docker-compose.shared.yml` + `docker-compose.dev.yml` | Локальная разработка | dev |
| `docker-compose.prod.yml` | Развёртывание, собранное из исходников | prod |
| `docker-compose.demo.yml` | Демо-стек | demo |
| `docker-compose.platform.yml` | Подключение к существующей сети | н/д |

## Обязательные секреты {#required-secrets}

<Callout type="danger">
Каждое <code>CHANGE_ME</code> представляет собой шаблон, а не настоящее значение по умолчанию. API отказывается запускаться вне Development, если секрет содержит шаблон или слишком короткий. Для рабочего сервера всегда генерируйте новые значения.
</Callout>

<Callout type="tip">
Надёжные случайные значения создаются командой <code>openssl</code>:
<ul>
<li><strong>JWT_SECRET</strong> (не менее 32 символов): <code>openssl rand -hex 32</code></li>
<li><strong>ENCRYPTION_KEY</strong> (ровно 32 байта, base64): <code>openssl rand -base64 32</code></li>
<li><strong>SESSION_ENCRYPTION_KEY</strong> (ровно 32 байта, base64): <code>openssl rand -base64 32</code></li>
<li><strong>HOSTPANEL_BACKCHANNEL_LOGOUT_KEY</strong> (не менее 16 символов): <code>openssl rand -hex 24</code></li>
<li><strong>POSTGRES_PASSWORD</strong>: <code>openssl rand -hex 24</code></li>
<li><strong>RABBITMQ_PASSWORD</strong>: <code>openssl rand -hex 24</code></li>
</ul>
</Callout>

## Общие {#general}

<Callout type="warning">
Сервер, оставленный в режиме <code>Development</code>, запускает DevDataSeeder и при каждом перезапуске заново заполняет тестовыми данными. На развёрнутом хосте всегда задавайте <code>ASPNETCORE_ENVIRONMENT=Production</code>.
</Callout>

<SettingTable :settings="[
  { name: 'ASPNETCORE_ENVIRONMENT', type: 'string', default: 'Development', description: 'Режим работы: Production или Development. В режиме Development дополнительно запускается DevDataSeeder, который при каждом перезапуске заново заполняет тестовыми данными. Сервер должен работать в режиме Production. Обязательна в hub и prod. Уровни: hub, prod, dev.' },
  { name: 'AUTH_APP_NAME', type: 'string', default: 'hostpanel', description: 'Имя приложения, зарегистрированное в слое аутентификации (имена cookie и клиента). Уровни: hub, prod, dev.' },
  { name: 'DISABLE_RELEASE_UPDATE_CHECKS', type: 'flag', default: 'false', description: 'Значение true отключает ежедневную проверку наличия нового релиза на GitHub. Отключайте, если у установки нет выхода в интернет. Соответствует ScheduledJobs__DisableReleaseUpdateChecks. Уровни: hub, prod.' },
  { name: 'DISABLE_EXCHANGE_RATE_UPDATES', type: 'flag', default: 'false', description: 'Значение true отключает ежедневное обновление курсов валют (03:00 UTC). Отключайте, если у установки нет выхода в интернет; тогда курсы меняются только вручную или кнопкой «Обновить курсы обмена». Соответствует ScheduledJobs__DisableExchangeRateUpdates. Уровни: hub, prod.' },
]" />

## Режимы входа {#sign-in-modes}

Hostpanel поддерживает два режима аутентификации: локальный (пользователями управляет сам Hostpanel) и SSO (пользователи входят через внешнего провайдера OpenID Connect).

<SettingTable :settings="[
  { name: 'AUTH_MODE', type: 'string', default: 'local', description: 'local: Hostpanel сам аутентифицирует своих пользователей. sso: вход выполняется через внешнего провайдера OpenID Connect. Обязательна. Уровни: все.' },
]" />

### Настройки SSO {#sso-settings}

<Label type="sso" /> Эти настройки обязательны только при AUTH_MODE=sso.

<SettingTable :settings="[
  { name: 'SSO_URL', type: 'string', default: 'https://sso.example.com', description: 'Адрес, по которому API обращается к провайдеру напрямую, от сервера к серверу. Обязательна для SSO. Уровни: все.' },
  { name: 'SSO_PUBLIC_URL', type: 'string', default: 'https://sso.example.com', description: 'Куда браузер направляется для входа (публичное имя хоста провайдера, издатель). Обязательна для SSO. Уровни: все.' },
  { name: 'SSO_CLIENT_ID', type: 'string', default: 'hostpanel', description: 'Идентификатор клиента, зарегистрированный у провайдера. Обязательна для SSO. Уровни: все.' },
  { name: 'SSO_ISSUER', type: 'string', default: 'https://sso.example.com/', description: 'Издатель, которого провайдер указывает в своих токенах, ровно так, как он перечислен в его openid-configuration (включая завершающий слеш). Каждый токен выхода по обратному каналу проверяется по нему. API отказывается запускаться без неё при AUTH_MODE=sso. Обязательна для SSO. Уровни: все.' },
  { name: 'SSO_REQUIRE_HTTPS_METADATA', type: 'flag', default: 'true', description: 'Требует ли получение ключей, которыми проверяются токены выхода по обратному каналу, https в SSO_URL. Задавайте false, только если SSO_URL указывает на обычный http-адрес в частной сети. Уровни: все.' },
  { name: 'SSO_CALLBACK_URL', type: 'string', default: 'https://panel.example.com/api/portal/auth/sso/callback', description: 'URL, на который провайдер перенаправляет после входа. Обратный вызов администратора: <admin origin>/api/auth/callback. Обязательна для SSO. Уровни: все.' },
  { name: 'SSO_SERVICE_KEY', type: 'secret', default: 'CHANGE_ME', description: 'Машинные учётные данные для чтения каталога пользователей. Создайте командой openssl rand -base64 24. Обязательна для SSO. Уровни: все.' },
  { name: 'CLIENT_HOSTPANEL_SECRET', type: 'secret', default: 'CHANGE_ME', description: 'Секрет, с которым API обменивает код авторизации SPA администратора, от сервера к серверу. API отказывается запускаться без него при AUTH_MODE=sso. Обязательна для SSO. Уровни: все.' },
  { name: 'HOSTPANEL_BACKCHANNEL_LOGOUT_KEY', type: 'secret', default: 'CHANGE_ME', description: 'Общий с провайдером ключ для выхода по обратному каналу. Создайте командой openssl rand -hex 24. На развёрнутом хосте должен быть не короче 16 символов. Оставляйте пустым, только если провайдер не подключён. Обязательна для SSO. Уровни: все.' },
]" />

При обновлении до релиза, переводящего пакет единого входа на версию 5.1, сессии панели администратора один раз сбрасываются: пакет теперь хранит ключи кеша сессий отдельно для каждого продукта, поэтому сессии, сохранённые под старыми ключами, больше не читаются. После обновления каждый администратор входит заново, больше ничего делать не нужно, а старые записи устаревают сами. Пакет также сам подключает приёмник выхода по обратному каналу по адресу /api/auth/backchannel-logout, а обратный вызов входа, для которого нет соответствующей попытки (вторая вкладка, кнопка «Назад» после входа), теперь перенаправляет на страницу входа вместо ответа с телом ошибки. Ничего из этого настраивать не нужно.

### Настройки локальной сессии {#local-session-settings}

<Label type="local" /> Эти настройки действуют только при AUTH_MODE=local.

<SettingTable :settings="[
  { name: 'SESSION_ACCESS_TOKEN_MINUTES', type: 'string', default: '15', description: 'Срок жизни токена доступа в минутах. Скользящее продление выдаёт токен заново, пока человек продолжает работать. Уровни: hub, prod, dev.' },
  { name: 'SESSION_MAX_MINUTES', type: 'string', default: '720', description: 'Предельный возраст сессии, после которого продление токена прекращается и человеку нужно войти заново. Уровни: hub, prod, dev.' },
]" />

## Адреса и имена хостов {#addresses-and-host-names}

<SettingTable :settings="[
  { name: 'DOMAIN', type: 'string', default: 'yourdomain.com', description: 'Публичное имя хоста клиентского портала (например, panel.example.com). Должно отличаться от ADMIN_DOMAIN. Обязательна. Уровни: все.' },
  { name: 'ADMIN_DOMAIN', type: 'string', default: 'admin.yourdomain.com', description: 'Публичное имя хоста панели администратора. Должно отличаться от DOMAIN. Обязательна. Уровни: все.' },
  { name: 'ADMIN_BASE_URL', type: 'string', default: 'https://admin.yourdomain.com', description: 'Абсолютный URL панели администратора (со схемой, без завершающего слеша). Основа ссылок, которые API отправляет сотрудникам по почте. Обязательна в prod и hub. Уровни: prod, hub.' },
  { name: 'CLIENT_BASE_URL', type: 'string', default: 'https://yourdomain.com', description: 'Абсолютный URL клиентского портала (со схемой, без завершающего слеша). Основа ссылок, которые API отправляет клиентам по почте. Обязательна в prod и hub. Уровни: prod, hub.' },
  { name: 'NUXT_PUBLIC_BASE_URL', type: 'string', default: 'https://panel.example.com', description: 'Публичный URL, который JavaScript клиентского портала использует для вызовов API. Обязательна. Уровни: все.' },
  { name: 'ADMIN_BASE_URL_PATH', type: 'string', default: '/', description: 'Базовый путь, под которым отдаётся SPA администратора (/ или /admin). Читается контейнером администратора при запуске, а не при сборке. Уровни: hub, prod.' },
  { name: 'ALLOWED_HOSTS', type: 'string', default: 'panel.example.test;hostpanel-api;localhost', description: 'Имена хостов, на которые отвечает API, через точку с запятой. Укажите публичный хост, hostpanel-api и localhost. Обязательна. Уровни: все.' },
  { name: 'PANEL_DEV_ORIGIN', type: 'string', default: 'http://panel.example.test', description: 'Источник прокси для разработки, который API разрешает наряду с localhost (CORS). Уровни: dev.' },
  { name: 'SSH_AGENT_SOURCE_ADDRESSES', type: 'string', default: '', description: 'Публичные адреса, с которых эта установка подключается к агентам SSH-ключей, через запятую. Новое в 0.4.0. Уровни: prod, hub.' },
  { name: 'PANEL_HOST_ADDRESSES', type: 'string', default: '', description: 'Панели управления, доступные только по VPN, в виде пар hostname=address через запятую. Новое в 0.4.0. Уровни: prod, hub.' },
]" />

Раздел «Серверы → SSH» показывает точные правила межсетевого экрана, когда задан SSH_AGENT_SOURCE_ADDRESSES. См. [Агент SSH-ключей](/ru/servers/ssh-key-agent).

## Ключи и шифрование {#keys-and-encryption}

<Callout type="danger">
ENCRYPTION_KEY защищает пароли серверов, API-токены и хеши доступа. Никогда не меняйте его на работающей установке без плана перешифрования. Сохраняйте .env вместе с каждым дампом базы данных.
</Callout>

<SettingTable :settings="[
  { name: 'ENCRYPTION_KEY', type: 'secret', default: 'see .env.example', description: 'Должен декодироваться ровно в 32 байта. Защищает зашифрованные столбцы базы данных. Создание: openssl rand -base64 32. Значение из примера служит шаблоном, вне Development оно отклоняется. Обязательна в prod, hub, demo. Уровни: prod, hub, demo.' },
  { name: 'SESSION_ENCRYPTION_KEY', type: 'secret', default: '', description: 'Ключ сессий клиентского портала (AES-256-GCM). 32 случайных байта в base64. При смене все пользователи выходят из системы. Обязательна в prod, hub, demo. Уровни: prod, hub, demo.' },
  { name: 'JWT_SECRET', type: 'secret', default: 'see .env.example', description: 'Не менее 32 символов. Шаблон вне Development отклоняется. Создание: openssl rand -hex 32. Уровни: все.' },
  { name: 'JWT_ISSUER', type: 'string', default: 'innovayse-api', description: 'Утверждение об издателе, записываемое в каждый токен. Уровни: все.' },
  { name: 'JWT_AUDIENCE', type: 'string', default: 'innovayse-clients', description: 'Утверждение об аудитории, проверяемое в каждом токене. Уровни: все.' },
]" />

## База данных, очередь сообщений и кеш {#database-message-queue-and-cache}

<SettingTable :settings="[
  { name: 'POSTGRES_HOST', type: 'string', default: 'hostpanel-db', description: 'Имя хоста базы данных. Уровни: все.' },
  { name: 'POSTGRES_PORT', type: 'string', default: '5432', description: 'Порт базы данных. Уровни: все.' },
  { name: 'POSTGRES_USER', type: 'string', default: 'postgres', description: 'Пользователь базы данных. Уровни: все.' },
  { name: 'POSTGRES_PASSWORD', type: 'secret', default: 'CHANGE_ME', description: 'Пароль базы данных. Шаблон вне Development отклоняется. Обязательна. Уровни: все.' },
  { name: 'POSTGRES_DB', type: 'string', default: 'innovayse_dev', description: 'Имя базы данных. Уровни: все.' },
  { name: 'RABBITMQ_HOST', type: 'string', default: 'hostpanel-rabbitmq', description: 'Имя хоста очереди сообщений. Уровни: все.' },
  { name: 'RABBITMQ_PORT', type: 'string', default: '5672', description: 'Порт очереди сообщений. Уровни: все.' },
  { name: 'RABBITMQ_USER', type: 'string', default: 'guest', description: 'Пользователь очереди сообщений. Уровни: все.' },
  { name: 'RABBITMQ_PASSWORD', type: 'secret', default: 'CHANGE_ME', description: 'Пароль очереди сообщений. Шаблон вне Development отклоняется. Обязательна. Уровни: все.' },
  { name: 'REDIS_CONNECTION', type: 'string', default: 'hostpanel-redis:6379', description: 'Строка подключения к Redis. Используется для сессий администратора (SSO), хранилища ключей защиты данных и сессий входа в клиентский портал. Уровни: все.' },
]" />

## Почта {#mail}

<Callout type="warning">
Незаданный или неверно написанный SMTP_ENCRYPTION останавливает API при запуске и называет этот ключ в журнале.
</Callout>

<SettingTable :settings="[
  { name: 'SMTP_HOST', type: 'string', default: 'hostpanel-mailhog', description: 'Хост SMTP-ретранслятора. Установщик оставляет его пустым; пока он не задан, почта не отправляется. Обязательна в prod и hub. Уровни: все.' },
  { name: 'SMTP_PORT', type: 'string', default: '1025', description: 'Порт SMTP. Обычно 587 (STARTTLS) или 465 (неявный TLS). Уровни: все.' },
  { name: 'SMTP_USERNAME', type: 'string', default: '', description: 'Имя пользователя ретранслятора. Уровни: все.' },
  { name: 'SMTP_PASSWORD', type: 'secret', default: '', description: 'Пароль ретранслятора. Уровни: все.' },
  { name: 'SMTP_ENCRYPTION', type: 'string', default: 'none', description: 'tls (STARTTLS), ssl (неявный TLS) или none (только для локального перехватчика почты). Обязательна и проверяется при запуске. Уровни: все.' },
  { name: 'SMTP_FROM', type: 'string', default: 'noreply@example.com', description: 'Адрес отправителя. Уровни: все.' },
  { name: 'SMTP_FROM_NAME', type: 'string', default: 'Hostpanel', description: 'Отображаемое имя отправителя. Уровни: все.' },
  { name: 'EMAIL_TO', type: 'string', default: 'contact@yourdomain.com', description: 'Адрес доставки писем из формы обратной связи. Если он не задан, форма отвечает 503 CONTACT_NOT_CONFIGURED. Уровни: все.' },
  { name: 'DEPARTMENT_SUPPORT_EMAIL', type: 'string', default: 'support@example.com', description: 'Адрес отдела поддержки при первой установке. Позже редактируется в панели администратора. Уровни: все.' },
  { name: 'DEPARTMENT_BILLING_EMAIL', type: 'string', default: 'billing@example.com', description: 'Адрес отдела биллинга при первой установке. Уровни: все.' },
  { name: 'DEPARTMENT_SALES_EMAIL', type: 'string', default: 'sales@example.com', description: 'Адрес отдела продаж при первой установке. Уровни: все.' },
  { name: 'DEPARTMENT_GENERAL_EMAIL', type: 'string', default: 'hello@example.com', description: 'Общий адрес отдела при первой установке. Уровни: все.' },
  { name: 'TELEGRAM_BOT_TOKEN', type: 'secret', default: '', description: 'Токен бота Telegram для уведомлений из формы обратной связи. Обе настройки пары или ни одной: половина пары не даёт запуститься. Уровни: все.' },
  { name: 'TELEGRAM_CHAT_ID', type: 'string', default: '', description: 'Идентификатор чата Telegram, вторая часть необязательной пары. Задайте обе или ни одной. Уровни: все.' },
]" />

## Обратный прокси и доверенные прокси {#reverse-proxy-and-trusted-proxies}

<Callout type="warning">
Неверное число прокси нарушает ограничение частоты запросов и учёт IP-адресов в заказах. Слишком мало: все посетители делят один раздел ограничения, а заказы записывают неверный IP. Слишком много: посетитель может подделать свой адрес.
</Callout>

Когда установка находится за одним или несколькими прокси, API и клиентский портал должны доверять заголовкам с реальным адресом посетителя. Схема показывает путь:

```
visitor → [extra proxy] → edge (Caddy/nginx) → client portal → API
                                                    ↑
                                         RATE_LIMIT_TRUSTED_PROXY_COUNT
                                         CLIENT_TRUSTED_PROXY_COUNT
```

Каждый дополнительный прокси перед встроенным пограничным добавляет 1 к обоим значениям. В поставляемых стеках один встроенный пограничный слой, поэтому по умолчанию стоит `1`. Если вы ставите перед ним дополнительный прокси (балансировщик нагрузки, WAF, CDN), увеличивайте оба значения на 1 за каждый дополнительный слой.

<SettingTable :settings="[
  { name: 'STACK_NETWORK_SUBNET', type: 'string', default: '172.30.0.0/24', description: 'Диапазон CIDR Docker-сети, которую создаёт стек. Передаётся в API как RateLimit__KnownNetworks__0. Выберите диапазон, не занятый другой сетью на хосте. Обязательна. Уровни: hub, prod, dev.' },
  { name: 'RATE_LIMIT_TRUSTED_PROXY_COUNT', type: 'string', default: '1', description: 'Число прокси перед API (RateLimit__TrustedProxyCount). Поставляемым стекам нужно 1 (встроенный пограничный прокси). Добавляйте по одному на каждый дополнительный прокси. Уровни: hub, prod, dev.' },
  { name: 'CLIENT_TRUSTED_PROXY_COUNT', type: 'string', default: '1', description: 'Число прокси перед клиентским порталом (NUXT_TRUSTED_PROXY_COUNT). Поставляемым стекам нужно 1. Добавляйте по одному на каждый дополнительный прокси. Стек для разработки фиксирует значение 0 и эту переменную не читает. Уровни: hub, prod, demo.' },
]" />

### Настройки ограничения частоты запросов (только через переопределение compose) {#rate-limit-settings-compose-override-only}

<Callout type="note">
Следующие настройки задаются прямо на службе API в переопределяющем файле compose, а не в <code>.env.example</code>. Используйте их, чтобы настроить ограничение частоты запросов для каждой конечной точки.
</Callout>

| Настройка | По умолчанию | Описание |
|---|---|---|
| `RateLimit__Global__PermitLimit` | `1200` | Число запросов, разрешённых за окно (действует для каждого запроса) |
| `RateLimit__Global__WindowSeconds` | `60` | Длина окна в секундах |
| `RateLimit__Auth__PermitLimit` | `15` | Предел для конечных точек аутентификации за окно |
| `RateLimit__Auth__WindowSeconds` | `60` | Длина окна аутентификации в секундах |
| `RateLimit__Upstream__PermitLimit` | `60` | Предел для вышестоящего API (API панели) за окно |
| `RateLimit__Upstream__WindowSeconds` | `60` | Длина окна вышестоящего API в секундах |
| `RateLimit__Strict__PermitLimit` | `5` | Предел для строгих конечных точек за окно |
| `RateLimit__Strict__WindowSeconds` | `60` | Длина строгого окна в секундах |
| `RateLimit__SshKeys__PermitLimit` | `20` | Предел для конечных точек SSH-ключей за окно |
| `RateLimit__SshKeys__WindowSeconds` | `60` | Длина окна SSH-ключей в секундах |
| `RateLimit__ConcurrentPermitLimit` | `5` | Наибольшее число одновременных запросов с одного адреса |
| `RateLimit__PluginDefault__PermitLimit` | `120` | Предел для маршрута плагина за окно, если маршрут не называет уровень. Считается для каждого плагина и каждого вызывающего (учётная запись клиента или администратор при вызове администратора) |
| `RateLimit__PluginDefault__WindowSeconds` | `60` | Длина окна плагина по умолчанию в секундах |
| `RateLimit__PluginStrict__PermitLimit` | `20` | Предел за окно для маршрута плагина, запрашивающего строгий уровень (затратные или чувствительные маршруты, например отправляющие сообщения) |
| `RateLimit__PluginStrict__WindowSeconds` | `60` | Длина строгого окна плагина в секундах |
| `RateLimit__PluginHeavy__PermitLimit` | `6` | Предел за окно для маршрута плагина, запрашивающего тяжёлый уровень (отчёты, экспорт) |
| `RateLimit__PluginHeavy__WindowSeconds` | `60` | Длина тяжёлого окна плагина в секундах |
| `RateLimit__PluginCallback__PermitLimit` | `120` | Предел за окно для публичного входа обратных вызовов плагинов (`/api/plugins/{id}/callback/...`). Считается для каждого адреса вызывающего по всем плагинам и путям, объявленным и нет |
| `RateLimit__PluginCallback__WindowSeconds` | `60` | Длина окна обратных вызовов плагина в секундах |
| `RateLimit__PluginStreamPermitLimit` | `3` | Сколько потоковых маршрутов плагина один вызывающий может держать открытыми одновременно, для каждого плагина |
| `SignInLockout__MaxFailedAttempts` | `5` | Число неверных паролей до блокировки |
| `SignInLockout__LockoutMinutes` | `15` | Сколько минут учётная запись остаётся заблокированной (вход в заблокированную запись → 423) |

MaxFailedAttempts принимает значения от 1 до 100, LockoutMinutes от 1 до 1440. Значения вне этих диапазонов не дают запуститься.

Маршруты плагинов ограничиваются для каждого плагина и каждого вызывающего, поэтому один плагин или одна учётная запись не могут исчерпать API. Маршрут плагина объявляет свой уровень в манифесте плагина (`rateLimit`: `default`, `strict` или `heavy`); маршрут, не объявивший его, использует уровень по умолчанию. Отклонённый запрос получает ответ 429 с `Retry-After` и заголовками `X-RateLimit-Policy`, `X-RateLimit-Limit`, `X-RateLimit-Remaining` и `X-RateLimit-Reset`. Каждый уровень плагина должен быть положительным и не больше `RateLimit__Global__PermitLimit`, уровни должны быть упорядочены heavy <= strict <= default, а `RateLimit__PluginStreamPermitLimit` должен быть положительным, иначе запуск отклоняется.

Публичный вход обратных вызовов плагинов (`/api/plugins/{id}/callback/...`) ограничивается для каждого адреса вызывающего, и этот адрес определяется по пересылаемым заголовкам. Это работает как задумано, только если пересылаемые заголовки настроены: задайте `RateLimit__TrustedProxyCount` равным числу прокси перед API и перечислите их в `RateLimit__KnownProxies` или `RateLimit__KnownNetworks` (встроенные стеки делают это через `STACK_NETWORK_SUBNET`). За ненастроенным прокси все вызовы шлюза делят адрес прокси, и один всплеск отклоняет все обратные вызовы; при слишком большом числе доверенных переходов отправитель может записать собственный `X-Forwarded-For` и выбрать себе свою корзину. API игнорирует `X-Forwarded-For` от любого узла, не являющегося настроенным прокси.

## Пограничный прокси {#edge-proxy}

Пограничный слой (Caddy на hub, nginx на prod) завершает TLS и направляет запросы в API и клиентский портал.

<SettingTable :settings="[
  { name: 'EDGE_SCHEME', type: 'string', default: 'https://', description: 'https:// для автоматических сертификатов Let\'s Encrypt (по умолчанию); http://, когда TLS завершает ваш собственный прокси (тогда выберите свободные порты). Уровни: hub, prod.' },
  { name: 'EDGE_HTTP_PORT', type: 'string', default: '80', description: 'HTTP-порт, который слушает пограничный прокси. Уровни: hub, prod.' },
  { name: 'EDGE_HTTPS_PORT', type: 'string', default: '443', description: 'HTTPS-порт, который слушает пограничный прокси. Уровни: hub, prod.' },
  { name: 'ACME_EMAIL', type: 'string', default: 'admin@yourdomain.com', description: 'Контактный адрес для сертификатов Let\'s Encrypt. Задаётся установщиком. Обязательна для hub. Уровни: hub.' },
  { name: 'EDGE_MAX_BODY_SIZE', type: 'string', default: '50M', description: 'Наибольшее тело запроса на хосте клиентского портала. Используйте суффикс M (например, 50M). Уровни: hub, prod.' },
  { name: 'EDGE_ADMIN_MAX_BODY_SIZE', type: 'string', default: '1M', description: 'Наибольшее тело запроса на хосте администратора. Уровни: hub, prod.' },
  { name: 'EDGE_CLIENT_CSP_SOURCES', type: 'string', default: '', description: 'Дополнительные источники, которые разрешает CSP клиентского портала (через пробел). Добавьте источники скриптов и API, нужные оформлению заказа платёжного плагина; политика по умолчанию не называет ни одного шлюза. Уровни: hub, prod.' },
  { name: 'NGINX_HTTP_PORT', type: 'string', default: '80', description: 'HTTP-порт хоста для встроенного nginx (стек prod, без оверлея платформы). Уровни: prod.' },
  { name: 'NGINX_HTTPS_PORT', type: 'string', default: '443', description: 'HTTPS-порт хоста для встроенного nginx. Уровни: prod.' },
]" />

Письма рассылок несут личный токен в адресе пикселя открытия, отслеживаемых ссылок и ссылки отписки. Встроенный nginx пишет журнал доступа с заменой этого токена на `***`. Встроенный Caddy не пишет журнал доступа, пока вы его не включите, а собственный прокси перед панелью должен сам скрывать в своём журнале пути `/api/public/campaign-mail/` и параметр запроса `token` у `/unsubscribe` и `/api/public/unsubscribe/`.

## Образы и версии {#images-and-versions}

<SettingTable :settings="[
  { name: 'IMAGE_TAG', type: 'string', default: 'local', description: 'Запускаемый релиз, например 0.4.0 или latest. Без ведущей v. Обязательна для hub и prod. Уровни: hub, prod.' },
  { name: 'DOCKERHUB_NAMESPACE', type: 'string', default: 'innovayse', description: 'Пользователь или организация Docker Hub для образов стека hub. Уровни: hub.' },
  { name: 'IMAGE_REGISTRY', type: 'string', default: 'registry.example.com/hostpanel', description: 'Путь реестра для образов стека prod (например, registry.example.com/hostpanel). Обязательна для prod. Уровни: prod.' },
  { name: 'POSTGRES_IMAGE', type: 'string', default: 'see .env.example', description: 'Полная ссылка на образ postgres, закреплённая по дайджесту. Уровни: все.' },
  { name: 'RABBITMQ_IMAGE', type: 'string', default: 'see .env.example', description: 'Полная ссылка на образ RabbitMQ, закреплённая по дайджесту. Уровни: все.' },
  { name: 'REDIS_IMAGE', type: 'string', default: 'see .env.example', description: 'Полная ссылка на образ Redis, закреплённая по дайджесту. Уровни: все.' },
  { name: 'MAILHOG_IMAGE', type: 'string', default: 'see .env.example', description: 'Полная ссылка на образ Mailhog (стеки dev и demo). Уровни: dev, demo.' },
  { name: 'NGINX_IMAGE', type: 'string', default: 'see .env.example', description: 'Полная ссылка на образ nginx (должна быть сборка без привилегий). Уровни: prod.' },
  { name: 'CADDY_IMAGE', type: 'string', default: 'see .env.example', description: 'Полная ссылка на образ Caddy. Уровни: hub.' },
]" />

## API и порты {#api-and-ports}

<SettingTable :settings="[
  { name: 'API_PORT', type: 'string', default: '5148', description: 'Порт, который API слушает внутри своего контейнера. Подставляется в конфигурацию пограничного прокси, менять только здесь. Уровни: все.' },
  { name: 'CLIENT_PORT', type: 'string', default: '3000', description: 'Порт, который клиентский портал слушает внутри своего контейнера. Подставляется в конфигурацию пограничного прокси, менять только здесь. Уровни: все.' },
  { name: 'API_GC_SERVER', type: 'string', default: '0', description: 'Режим сборщика мусора .NET: 0 рабочая станция, 1 сервер. Уровни: prod, hub.' },
  { name: 'API_GC_CONSERVE_MEMORY', type: 'string', default: '5', description: 'Уровень экономии памяти, 0-9. Уровни: prod, hub.' },
  { name: 'API_GC_HEAP_HARD_LIMIT_PERCENT', type: 'string', default: '75', description: 'Предел кучи в процентах от памяти контейнера. Уровни: prod, hub.' },
]" />

### Настройки нумерации заказов и счетов (только через переопределение compose) {#order-numbering-and-invoice-settings-compose-override-only}

<Callout type="note">
Настройки нумерации заказов, счетов и повторных попыток не находятся в <code>.env</code>. Задавайте их прямо на службе API в переопределяющем файле compose.
</Callout>

| Настройка | По умолчанию | Описание |
|---|---|---|
| `Orders__NumberPrefix` | `ORD-` | Префикс номера заказа |
| `Orders__NumberDigits` | `4` | Число цифр с ведущими нулями (например, `0042`) |
| `Orders__InvoicePaymentTermDays` | `7` | Срок оплаты счёта в днях (0-365) |
| `Orders__RetryLeaseMinutes` | `10` | Сколько минут учитывается захват строки заказа (при принятии или при повторной попытке), прежде чем повторная попытка сможет перехватить строку (от 1 до 1440). Срок отсчитывается с момента, когда начинается действие самой строки, и страница заказа в админке предлагает повтор только там, где этот срок его разрешает. Задайте больше, чем самый медленный вызов регистратора или провижининга. |
| `InvoicePdf__MaxLines` | `500` | Наибольшее число позиций в одном PDF |
| `InvoicePdf__MaxConcurrentRenders` | `4` | Число одновременных построений PDF |
| `InvoicePdf__MaxRenderWaitSeconds` | `10` | Сколько секунд скачивание ждёт свободного места для построения, прежде чем ответить 503 с Retry-After. |

## Оформление и содержимое клиентского портала {#client-portal-look-and-content}

Все эти настройки управляют публичной витриной и имеют запасные переменные окружения NUXT_PUBLIC_PORTAL_*. После того как значение задано в разделе «Настройки» панели администратора, оно имеет приоритет над переменными окружения.

<SettingTable :settings="[
  { name: 'NUXT_PUBLIC_PORTAL_TEMPLATE', type: 'string', default: 'classic', description: 'Шаблон витрины: classic, aurora или nova. Настройки администратора переопределяют это значение. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_APPS_ENABLED', type: 'flag', default: 'false', description: 'Показывать панель запуска приложений в шапке. true/1/yes/on. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_CONTACT_EMAIL', type: 'string', default: '', description: 'Контактный адрес электронной почты в подвале. Если пусто, скрыт. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_SOCIAL_TWITTER', type: 'string', default: '', description: 'URL профиля Twitter/X. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_SOCIAL_LINKEDIN', type: 'string', default: '', description: 'URL профиля LinkedIn. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_SOCIAL_FACEBOOK', type: 'string', default: '', description: 'URL профиля Facebook. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_HOSTING_DEMO_URL', type: 'string', default: '', description: 'URL демо-сайта, на который ведёт ссылка из раздела продуктов на главной. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_CONTACT_ADDRESS', type: 'string', default: '', description: 'Почтовый адрес на странице контактов. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_CONTACT_MAP_URL', type: 'string', default: '', description: 'Ссылка на карту на странице контактов. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_STATS_CLIENTS', type: 'string', default: '', description: 'Показатель на главной странице: число клиентов (произвольный текст). Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_STATS_UPTIME', type: 'string', default: '', description: 'Показатель на главной странице: доступность. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_STATS_REGIONS', type: 'string', default: '', description: 'Показатель на главной странице: число регионов. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_HERO_BADGE', type: 'string', default: '', description: 'Короткий текст значка над заголовком главной страницы. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_SITE_NAME', type: 'string', default: '', description: 'Название сайта для заголовка браузера и превью в соцсетях. Настройки администратора имеют приоритет. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_SITE_TAGLINE', type: 'string', default: '', description: 'Слоган сайта. Настройки администратора имеют приоритет. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_THEME_DEFAULT', type: 'string', default: 'dark', description: 'Цветовая схема по умолчанию: light, dark или system. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_THEME_USER_TOGGLE', type: 'flag', default: 'true', description: 'Показывать переключатель светлой и тёмной темы. Скрывает его только буквальное false. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_LOGO', type: 'string', default: '', description: 'URL логотипа (для светлого фона). Если пусто, используется встроенный знак. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_LOGO_DARK', type: 'string', default: '', description: 'URL логотипа (для тёмного фона). Если не задан, берётся NUXT_PUBLIC_PORTAL_LOGO. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_LOGO_MARK', type: 'string', default: '', description: 'Квадратный знак для компактных мест. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_FAVICON', type: 'string', default: '', description: 'URL фавиконки. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_BRAND_PRIMARY', type: 'string', default: '', description: 'Основной цвет бренда (#rrggbb). Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_BRAND_ACCENT', type: 'string', default: '', description: 'Акцентный цвет бренда (#rrggbb). Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_BRAND_FONT_HEADING', type: 'string', default: '', description: 'Шрифт заголовков: inter, noto-sans-armenian, noto-serif-armenian или system. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_BRAND_FONT_BODY', type: 'string', default: '', description: 'Шрифт основного текста. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_GTM_ID', type: 'string', default: '', description: 'Идентификатор контейнера Google Tag Manager. Ждёт согласия на cookie. Настройки администратора имеют приоритет. Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_CHAT_BASE_URL', type: 'string', default: '', description: 'URL сервера живого чата. Также аргумент этапа сборки (встраивается в образ). Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_CHAT_WEBSITE_TOKEN', type: 'string', default: '', description: 'Токен виджета чата (язык по умолчанию). Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_CHAT_WEBSITE_TOKEN_RU', type: 'string', default: '', description: 'Токен виджета чата (русский). Уровни: все.' },
  { name: 'NUXT_PUBLIC_PORTAL_CHAT_WEBSITE_TOKEN_HY', type: 'string', default: '', description: 'Токен виджета чата (армянский). Уровни: все.' },
]" />

## Платежи и внешние ссылки {#payments-and-external-links}

<SettingTable :settings="[
  { name: 'BILLING_PORTAL_URL', type: 'string', default: '', description: 'Адрес платёжного портала (без завершающего слеша). Если пусто, ссылки передачи скрыты. Уровни: все.' },
]" />


## Плагины {#plugins}

<Callout type="note">
Настройки плагинов не находятся в .env. Задавайте их на службе API в переопределяющем файле compose.
</Callout>

| Настройка | По умолчанию | Описание |
|---|---|---|
| `Plugins__InstallPath` | *(пусто, используется `plugins-installed` рядом с приложением)* | Каталог, в который API устанавливает пакеты плагинов. |
| `Plugins__MaxEntryCount` | `1000` | Наибольшее число файлов в zip-архиве плагина. |
| `Plugins__MaxUncompressedBytes` | `209715200` | Наибольший суммарный размер архива плагина в распакованном виде в байтах (200 МиБ). |
| `Plugins__MigrationTimeoutSeconds` | `120` | Наибольшее время в секундах, которое может работать один обработчик миграции плагина после обновления (от 1 до 3600). По его истечении хост перестаёт ждать и оставляет плагин выключенным, пока одна из последующих попыток не удастся. |
| `Plugins__MaxViewBytes` | `262144` | Наибольший размер в байтах одного документа представления плагина (файла `view` страницы или слота, 256 КиБ); более крупный файл не даёт загрузить плагин при загрузке и установке. От 1 до 4194304 (4 МиБ). |
| `Plugins__TrustedPackageSha256__0`,  и так далее | н/д | Хеши SHA-256 пакетов плагинов, которым доверяют безусловно. Каждый хеш представляет собой отдельный ключ с индексом. |
| `PluginApi__MaxBodyBytes` | `1048576` | Наибольшее тело запроса, которое хост читает для маршрута плагина, в байтах (от 1 до 67108864). |
| `PluginApi__CallTimeoutSeconds` | `30` | Сколько секунд хост ждёт ответа маршрута плагина (от 1 до предела ниже). По истечении плагин отменяется через свой токен, а вызывающий получает `PLUGIN_TIMEOUT` (504). В потоковом ответе значение охватывает ожидание ответа, первого события и паузы между событиями, но не длительность потока, который продолжает отправлять данные. |
| `PluginApi__MaxCallTimeoutSeconds` | `600` | Верхний предел для ограничения отдельного маршрута (от 1 до 3600). Сейчас каждый маршрут получает `CallTimeoutSeconds`; когда в SDK плагинов появится соответствующее поле, маршрут сможет запросить более длинный предел, но не выше этого. Должен быть не меньше `CallTimeoutSeconds`. |
| `PluginApi__AbandonWaitSeconds` | `5` | Сколько секунд после предела вызова хост ждёт плагин, игнорирующий отмену, прежде чем бросить вызов (от 1 до 60). В любом случае вызывающий получает `PLUGIN_TIMEOUT`. |
| `PluginApi__MaxAbandonedCallsPerPlugin` | `8` | Наибольшее число брошенных вызовов одного плагина, которые ещё могут выполняться (от 1 до 1000). На пределе новые вызовы этого плагина сразу отклоняются с `PLUGIN_TIMEOUT`, пока один из них не завершится. |
| `PluginApi__StreamHeartbeatSeconds` | `15` | Сколько секунд потоковый ответ плагина может молчать, прежде чем хост запишет строку комментария, чтобы поддержать соединение (от 1 до 300). |
| `PluginApi__StreamEventMaxBytes` | `65536` | Наибольший объём данных одного события потокового ответа плагина в байтах (от 1 до 1048576). Плагин, отправивший более крупное событие, завершает поток событием `error`. |
| `PluginSigning__MaintainerKeys__0__KeyId`,  и так далее | *(поставляются в образе)* | Записи ключей подписи Ed25519 для официальных сопровождающих плагинов. Поставляются в образе; операторы их не задают. |
| `PluginSigning__OperatorKeys__0__KeyId` | н/д | Идентификатор ключа подписанта плагинов, которому доверяет оператор. Каждая запись представляет собой пару `__KeyId` и `__PublicKey` под одним индексом. В файлах compose задайте `PLUGIN_OPERATOR_KEY_ID`. Оставьте его и открытый ключ пустыми, чтобы не добавлять ни одного; если задан только один из двух, API не запускается. |
| `PluginSigning__OperatorKeys__0__PublicKey` | н/д | Открытый ключ Ed25519 доверенного оператором подписанта с индексом 0. Дополнительных подписантов добавляйте как `__1__KeyId` / `__1__PublicKey` и так далее. В файлах compose задайте `PLUGIN_OPERATOR_PUBLIC_KEY`. |

## Настройки только для разработки {#development-only-settings}

<Callout type="warning">
Настройки в этом разделе предназначены только для локальной разработки. Не используйте их на серверах.
</Callout>

<SettingTable :settings="[
  { name: 'POSTGRES_HOST_PORT', type: 'string', default: '5434', description: 'Порт хоста, публикуемый контейнером базы данных. Уровни: dev.' },
  { name: 'RABBITMQ_HOST_PORT', type: 'string', default: '5673', description: 'Порт хоста, публикуемый RabbitMQ. Уровни: dev.' },
  { name: 'RABBITMQ_MANAGEMENT_HOST_PORT', type: 'string', default: '15673', description: 'Порт хоста для веб-интерфейса управления RabbitMQ. Уровни: dev.' },
  { name: 'MAILHOG_SMTP_HOST_PORT', type: 'string', default: '1026', description: 'Порт хоста для SMTP Mailhog. Уровни: dev.' },
  { name: 'MAILHOG_UI_HOST_PORT', type: 'string', default: '8027', description: 'Порт хоста для веб-интерфейса Mailhog. Уровни: dev.' },
  { name: 'API_HOST_PORT', type: 'string', default: '5149', description: 'Порт хоста, публикуемый контейнером API. Уровни: dev.' },
  { name: 'CLIENT_HOST_PORT', type: 'string', default: '3001', description: 'Порт хоста, публикуемый клиентским порталом. Уровни: dev.' },
  { name: 'ADMIN_HOST_PORT', type: 'string', default: '5174', description: 'Порт хоста, публикуемый панелью администратора. Уровни: dev.' },
  { name: 'CORS_CLIENT_ORIGIN', type: 'string', default: 'http://localhost:3001', description: 'Источник браузера для клиентского портала (CORS). Уровни: dev.' },
  { name: 'CORS_ADMIN_ORIGIN', type: 'string', default: 'http://localhost:5174', description: 'Источник браузера для панели администратора (CORS). Уровни: dev.' },
  { name: 'CORS_STAGE_ORIGIN', type: 'string', default: 'https://stage-panel.example.com', description: 'Источник браузера для тестового клиентского портала (CORS). Уровни: dev.' },
  { name: 'CLIENT_DEV_ALLOWED_HOSTS', type: 'string', default: '', description: 'Дополнительные имена хостов, на которые отвечает сервер разработки клиента (через запятую). Уровни: dev.' },
  { name: 'ADMIN_DEV_ALLOWED_HOSTS', type: 'string', default: '', description: 'Дополнительные имена хостов, на которые отвечает сервер разработки администратора (через запятую). Уровни: dev.' },
  { name: 'DEV_FILE_POLLING', type: 'flag', default: 'true', description: 'Опрашивать смонтированные каталоги на изменения (true/false). Нужно для монтирований в Docker Desktop. Уровни: dev.' },
  { name: 'DEV_NUGET_AUDIT', type: 'flag', default: 'false', description: 'Включить аудит пакетов NuGet внутри контейнера API для разработки. Уровни: dev.' },
  { name: 'DEV_NUGET_SIGNATURE_VERIFICATION', type: 'flag', default: 'false', description: 'Включить проверку подписей NuGet. Уровни: dev.' },
  { name: 'DEV_SEED_ADMIN_PASSWORD', type: 'string', default: 'Admin123!', description: 'Пароль учётной записи администратора, созданной заполнителем данных для разработки. Опубликованная тестовая запись, не секрет. Уровни: dev.' },
  { name: 'DEV_SEED_CLIENT_PASSWORD', type: 'string', default: 'Test@12345', description: 'Пароль учётной записи клиента, созданной заполнителем данных для разработки. Уровни: dev.' },
  { name: 'TESTCONTAINERS_HOST_OVERRIDE', type: 'string', default: 'host.docker.internal', description: 'Хост Docker для Testcontainers в контейнере API для разработки. Уровни: dev.' },
  { name: 'ADMIN_API_BASE', type: 'string', default: '/api', description: 'Базовый путь, который сервер разработки администратора проксирует в API. Уровни: dev.' },
  { name: 'DB_CPUS', type: 'string', default: '1.0', description: 'Ограничение процессора для контейнера базы данных. Уровни: dev.' },
  { name: 'DB_MEMORY', type: 'string', default: '1G', description: 'Ограничение памяти для контейнера базы данных. Уровни: dev.' },
  { name: 'RABBITMQ_CPUS', type: 'string', default: '1.0', description: 'Ограничение процессора для RabbitMQ. Уровни: dev.' },
  { name: 'RABBITMQ_MEMORY', type: 'string', default: '512M', description: 'Ограничение памяти для RabbitMQ. Уровни: dev.' },
  { name: 'REDIS_CPUS', type: 'string', default: '0.5', description: 'Ограничение процессора для Redis. Уровни: dev.' },
  { name: 'REDIS_MEMORY', type: 'string', default: '256M', description: 'Ограничение памяти для Redis. Уровни: dev.' },
  { name: 'MAILHOG_CPUS', type: 'string', default: '0.2', description: 'Ограничение процессора для Mailhog. Уровни: dev.' },
  { name: 'MAILHOG_MEMORY', type: 'string', default: '256M', description: 'Ограничение памяти для Mailhog. Уровни: dev.' },
  { name: 'API_CPUS', type: 'string', default: '2.0', description: 'Ограничение процессора для API. Уровни: dev.' },
  { name: 'API_MEMORY', type: 'string', default: '2G', description: 'Ограничение памяти для API. Уровни: dev.' },
  { name: 'CLIENT_CPUS', type: 'string', default: '1.5', description: 'Ограничение процессора для клиентского портала. Уровни: dev.' },
  { name: 'CLIENT_MEMORY', type: 'string', default: '2G', description: 'Ограничение памяти для клиентского портала. Уровни: dev.' },
  { name: 'ADMIN_CPUS', type: 'string', default: '1.0', description: 'Ограничение процессора для панели администратора. Уровни: dev.' },
  { name: 'ADMIN_MEMORY', type: 'string', default: '1536M', description: 'Ограничение памяти для панели администратора. Уровни: dev.' },
]" />

## Демо-режим {#demo-mode}

<Label type="local" /> Демо-режиму нужен AUTH_MODE=local.

<Callout type="note">
Демо-режиму нужны AUTH_MODE=local и собственная POSTGRES_DB. Он по расписанию сбрасывает примеры данных. Чтобы включить его, задайте DEMO_ENABLED=true.
</Callout>

<SettingTable :settings="[
  { name: 'DEMO_ENABLED', type: 'flag', default: 'true', description: 'Включить демо-режим. Уровни: demo.' },
  { name: 'DEMO_RESET_INTERVAL_HOURS', type: 'string', default: '24', description: 'Часы между очистками базы данных и повторным заполнением. Уровни: demo.' },
  { name: 'DEMO_ADMIN_EMAIL', type: 'string', default: 'admin@demo.local', description: 'Адрес электронной почты для входа демо-администратора (показывается на странице входа). Уровни: demo.' },
  { name: 'DEMO_ADMIN_PASSWORD', type: 'secret', default: '', description: 'Пароль демо-администратора. Не менее 8 символов, с заглавными и строчными буквами. Значения по умолчанию нет; без него compose отказывается запускаться. Обязательна для demo. Уровни: demo.' },
  { name: 'DEMO_CLIENT_EMAIL', type: 'string', default: 'client@demo.local', description: 'Адрес электронной почты для входа демо-клиента. Уровни: demo.' },
  { name: 'DEMO_CLIENT_PASSWORD', type: 'secret', default: '', description: 'Пароль демо-клиента. Не менее 8 символов, с заглавными и строчными буквами. Обязательна для demo. Уровни: demo.' },
  { name: 'DEMO_CONTACT_EMAIL', type: 'string', default: '', description: 'Контактный адрес электронной почты в подвале демо-витрины. Если пусто, остаётся вымышленный шаблон. Уровни: demo.' },
  { name: 'DEMO_CONTACT_PHONE', type: 'string', default: '', description: 'Контактный телефон на демо-витрине. Уровни: demo.' },
  { name: 'DEMO_SMTP_PORT', type: 'string', default: '1025', description: 'Порт Mailhog демо-стека внутри сети. Уровни: demo.' },
  { name: 'DEMO_REDIS_PORT', type: 'string', default: '6379', description: 'Порт Redis демо-стека внутри сети. Уровни: demo.' },
  { name: 'DEMO_SESSION_ENCRYPTION_KEY', type: 'secret', default: '', description: 'Ключ шифрования сессий демо-портала. Используйте значение, отличное от рабочего. Обязательна для demo. Уровни: demo.' },
  { name: 'DEMO_PUBLISH_ADDRESS', type: 'string', default: '127.0.0.1', description: 'Адрес, к которому привязаны публикуемые порты демо-стека. Loopback не выпускает их в сеть. Обязательна для demo. Уровни: demo.' },
  { name: 'DEMO_SMTP_ENCRYPTION', type: 'string', default: 'none', description: 'Шифрование демо-перехватчика почты. Используйте none. Обязательна для demo. Уровни: demo.' },
  { name: 'DEMO_API_HOST_PORT', type: 'string', default: '5150', description: 'Порт хоста для демо-API. Обязательна для demo. Уровни: demo.' },
  { name: 'DEMO_CLIENT_HOST_PORT', type: 'string', default: '3002', description: 'Порт хоста для демо-клиентского портала. Обязательна для demo. Уровни: demo.' },
  { name: 'DEMO_ADMIN_HOST_PORT', type: 'string', default: '5175', description: 'Порт хоста для демо-панели администратора. Обязательна для demo. Уровни: demo.' },
  { name: 'DEMO_ADMIN_BASE_URL', type: 'string', default: 'http://localhost:5175', description: 'Абсолютный URL демо-панели администратора. Обязательна для demo. Уровни: demo.' },
  { name: 'DEMO_CLIENT_BASE_URL', type: 'string', default: 'http://localhost:3002', description: 'Абсолютный URL демо-клиентского портала. Обязательна для demo. Уровни: demo.' },
  { name: 'DEMO_MAILHOG_UI_HOST_PORT', type: 'string', default: '8028', description: 'Порт хоста для веб-интерфейса демо-Mailhog. Уровни: demo.' },
  { name: 'DEMO_NETWORK_SUBNET', type: 'string', default: '', description: 'Обязательна. Диапазон CIDR внутренней сети демо-стека. Нет в .env.example; docker-compose.demo.yml без неё отказывается запускаться. Пример: 172.28.0.0/16. Уровни: demo.' },
  { name: 'DEMO_EDGE_NETWORK_SUBNET', type: 'string', default: '', description: 'Обязательна. Диапазон CIDR пограничной сети демо-стека. Нет в .env.example; docker-compose.demo.yml без неё отказывается запускаться. Пример: 172.29.0.0/16. Уровни: demo.' },
]" />

## Отказы при запуске {#startup-refusals}

API проверяет секреты и ключевые настройки до того, как примет первый запрос. Если проверка не пройдена, он записывает в журнал точный ключ и завершает работу. Частые причины:

| Признак в журнале | Причина | Решение |
|---|---|---|
| Секрет всё ещё CHANGE_ME или слишком короткий | В JWT_SECRET, ENCRYPTION_KEY, POSTGRES_PASSWORD, RABBITMQ_PASSWORD или заданном HOSTPANEL_BACKCHANNEL_LOGOUT_KEY остался шаблон либо значение слишком короткое. | Сгенерируйте настоящее значение командой openssl rand. |
| Неверный SMTP_ENCRYPTION | Значение не задано, написано с ошибкой или не входит в tls, ssl, none. | Задайте правильное значение. |
| Задана только одна настройка необязательной пары | Задан TELEGRAM_BOT_TOKEN, а TELEGRAM_CHAT_ID пуст, или наоборот. | Задайте обе или очистите обе. |
| Нет CLIENT_HOSTPANEL_SECRET | AUTH_MODE=sso, но CLIENT_HOSTPANEL_SECRET пуст. | Задайте секрет или переключитесь на AUTH_MODE=local. |
| HOSTPANEL_BACKCHANNEL_LOGOUT_KEY слишком короткий | На развёрнутом хосте значение короче 16 символов. | Создайте командой openssl rand -hex 24. |
| Неверная настройка заказов | Orders__NumberPrefix и Orders__NumberDigits не могут дать допустимый номер заказа, Orders__InvoicePaymentTermDays вне диапазона (0-365) или Orders__RetryLeaseMinutes вне диапазона (от 1 до 1440). | Исправьте значения в переопределяющем файле compose. |
| Неверный источник CORS_* | Разрешённый источник не является допустимым абсолютным URL (например, нет схемы). | Исправьте источник в .env. |

## Коды ошибок {#error-codes}

Коды ошибок, возвращаемые клиенту, с их статусом HTTP и сообщением:

| Код | Статус | Сообщение |
|---|---|---|
| `CONTACT_NOT_CONFIGURED` | 503 | «Форма обратной связи сейчас недоступна. Пожалуйста, напишите или позвоните нам по контактам на этой странице.» |
| `SIGN_IN_ACCOUNT_LOCKED` | 423 | «Слишком много неверных паролей. Учётная запись временно заблокирована, попробуйте позже.» |

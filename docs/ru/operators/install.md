---
title: Установка Hostpanel
description: Пошаговое руководство по установке Hostpanel на Linux-сервер с помощью установщика в одну команду или docker compose.
---

# Установка Hostpanel {#install-hostpanel}

Как запустить Hostpanel на своём сервере с помощью установщика в одну команду или
вручную через docker compose.

## Перед началом {#before-you-start}

Вам понадобится:

- Linux-сервер с установленными [Docker](https://docs.docker.com/engine/install/) и
  [плагином Compose v2](https://docs.docker.com/compose/install/) и запущенным
  демоном. Установщик проверяет наличие `docker`, `docker compose`, `curl` и
  `openssl` и называет те, которых нет.
- Два DNS-имени хоста, указывающих на ваш сервер: одно для клиентского портала
  (например, `panel.example.com`) и одно для панели администратора (например, `admin.example.com`).
  Они должны различаться: панель администратора открывается с корневого пути и ей
  нужен отдельный хост.
- Адрес электронной почты для учётной записи администратора, получателя писем из формы
  обратной связи и сертификата Let's Encrypt.

Если вы не используете [`--no-tls`](#without-tls), порты **80** и **443** должны быть свободны и
доступны на сервере до запуска установщика.

## Запустите установщик {#run-the-installer}

::: code-group

```bash [install.sh]
curl -fsSL https://raw.githubusercontent.com/innovayse/hostpanel/main/install.sh | bash
```

```bash [docker compose]
# Download the three files, fill .env, then:
docker compose -f docker-compose.hub.yml pull
docker compose -f docker-compose.hub.yml up -d
```

```bash [.env]
# Required values for a manual install (generate secrets with openssl):
ASPNETCORE_ENVIRONMENT=Production
AUTH_MODE=local
IMAGE_TAG=latest
DOMAIN=panel.example.com
ADMIN_DOMAIN=admin.example.com
ADMIN_BASE_URL=https://admin.example.com
CLIENT_BASE_URL=https://panel.example.com
NUXT_PUBLIC_BASE_URL=https://panel.example.com
ALLOWED_HOSTS=panel.example.com;admin.example.com;hostpanel-api;localhost
ACME_EMAIL=you@example.com
EMAIL_TO=you@example.com
SMTP_HOST=smtp.example.com
SMTP_ENCRYPTION=tls
POSTGRES_DB=hostpanel
POSTGRES_PASSWORD=<openssl rand -hex 24>
RABBITMQ_PASSWORD=<openssl rand -hex 24>
JWT_SECRET=<openssl rand -hex 32>
ENCRYPTION_KEY=<openssl rand -base64 32>
SESSION_ENCRYPTION_KEY=<openssl rand -base64 32>
HOSTPANEL_BACKCHANNEL_LOGOUT_KEY=<openssl rand -hex 24>
```

:::

Рекомендуется установщик в одну команду: он генерирует все секреты, задаёт каждое
обязательное значение и ждёт, пока стек станет работоспособным. Вкладки docker compose и
`.env` предназначены для операторов, которым нужна скриптовая или нестандартная установка;
полные шаги описаны в разделе
[Установка вручную через docker compose](#install-by-hand-with-docker-compose).

**1.** Направьте DNS обоих имён хостов на ваш сервер и дождитесь распространения записей.

**2. Запустите установщик:**

```bash
curl -fsSL \
  https://raw.githubusercontent.com/innovayse/hostpanel/main/install.sh \
  | bash
```

**3. Ответьте на три вопроса:**

| Вопрос | Пример |
|---|---|
| `Domain of the client portal (e.g. panel.example.com)` (домен клиентского портала) | `panel.example.com` |
| `Domain of the admin panel` (домен панели администратора; по умолчанию `admin.<domain>`) | `admin.example.com` |
| `Admin e-mail (contact form target and mail sender)` (адрес администратора: получатель писем из формы и отправитель почты) | `you@example.com` |

**4.** Установщик загружает образы, запускает стек и ждёт ответа `/health`.
Закончив, он выводит оба URL и одноразовый токен настройки:

```
Hostpanel is running.

  Client portal : https://panel.example.com
  Admin panel   : https://admin.example.com
  Install dir   : /opt/hostpanel  (secrets are in .env, mode 600)

  Certificates are issued by Let's Encrypt on first request.
  DNS for both hosts must point at this server and ports 80/443
  must be open; watch it with:
    cd /opt/hostpanel
    docker compose -f docker-compose.hub.yml logs hostpanel-caddy

First admin account
  1. Open https://admin.example.com and create your account.
  2. Paste this one-time setup token when asked:

       <setup-token>

  Lost it? Read it from the API log:
    cd /opt/hostpanel
    docker compose -f docker-compose.hub.yml logs hostpanel-api \
      | grep "setup token"

Next: set a real SMTP relay in /opt/hostpanel/.env, then:
  cd /opt/hostpanel
  docker compose -f docker-compose.hub.yml up -d
Upgrade any time by running the install command again.
```

**5.** Откройте URL панели администратора, создайте учётную запись и вставьте токен настройки.
См. раздел [Первый запуск](#first-run).

## Параметры установщика {#installer-options}

Передавайте флаги в командной строке или задайте соответствующую переменную окружения
перед `bash` в конвейере:

<SettingTable :settings="[
  { name: '--dir &lt;path&gt; / HOSTPANEL_DIR', type: 'string', default: '/opt/hostpanel', description: 'Каталог, в который установщик загружает файлы и из которого запускает стек.' },
  { name: '--version &lt;x.y.z&gt; / HOSTPANEL_VERSION', type: 'string', default: 'latest', description: 'Устанавливаемый релиз, например 0.4.0. Допускается ведущая v. Если не указывать, ставится последний релиз.' },
  { name: '--no-tls / HOSTPANEL_TLS=off', type: 'flag', default: 'on', description: 'Отдавать обычный HTTP на порту 8080 за вашим собственным TLS-прокси вместо автоматических сертификатов. См. раздел Без TLS.' },
  { name: '--rollback', type: 'flag', default: 'none', description: 'Вернуться к версии и базе данных, записанным в самой свежей резервной копии другой версии. Запрашивает подтверждение. См. раздел Откат.' },
  { name: '--yes', type: 'flag', default: 'none', description: 'Заранее подтвердить откат, для скриптов.' },
  { name: '--keep &lt;n&gt; / HOSTPANEL_BACKUP_KEEP', type: 'number', default: '5', description: 'Сколько самых свежих резервных копий при обновлении хранить в папке backups. Самая старая копия каждой из самых новых версий тоже сохраняется (см. --keep-versions). Более старые удаляются.' },
  { name: '--keep-versions &lt;n&gt; / HOSTPANEL_BACKUP_KEEP_VERSIONS', type: 'number', default: '10', description: 'Для скольких самых новых версий, помимо самых свежих копий, хранится самая старая копия. Самые новые версии определяются по свежести последней копии, а не по наибольшему номеру версии. У более ранних версий копии удаляются, поэтому папка backups не растёт с каждым релизом.' },
  { name: '--skip-backup', type: 'flag', default: 'none', description: 'Обновить без резервной копии базы данных. Данные не защищены: используйте это, только когда сохранять нечего.' },
  { name: '--help / -h', type: 'flag', default: '—', description: 'Вывести справку по использованию и выйти.' },
  { name: 'HOSTPANEL_NONINTERACTIVE=1', type: 'env', default: '—', description: 'Пропустить интерактивные вопросы. Требует заданных HOSTPANEL_DOMAIN и HOSTPANEL_ADMIN_EMAIL.' },
  { name: 'HOSTPANEL_DOMAIN', type: 'env', default: '—', description: 'Публичное имя хоста клиентского портала. Обязательно при неинтерактивном запуске.' },
  { name: 'HOSTPANEL_ADMIN_DOMAIN', type: 'env', default: 'admin.&lt;domain&gt;', description: 'Публичное имя хоста панели администратора.' },
  { name: 'HOSTPANEL_ADMIN_EMAIL', type: 'env', default: '—', description: 'Адрес электронной почты администратора. Обязателен при неинтерактивном запуске.' },
]" />

**Пример неинтерактивного запуска** (CI/CD или скрипт подготовки сервера):

```bash
curl -fsSL https://raw.githubusercontent.com/innovayse/hostpanel/main/install.sh \
  | HOSTPANEL_DOMAIN=panel.example.com \
    HOSTPANEL_ADMIN_EMAIL=you@example.com \
    HOSTPANEL_NONINTERACTIVE=1 \
    bash
```

## С автоматическим TLS (по умолчанию) {#with-automatic-tls-default}

Встроенный пограничный прокси [Caddy](https://caddyserver.com/) при первом запросе
запрашивает сертификаты Let's Encrypt для обоих имён хостов. `ACME_EMAIL` получает
адрес администратора, который вы ввели.

DNS обоих хостов **должен указывать на ваш сервер** до запуска установщика, иначе
запрос сертификата завершится ошибкой. Следить за журналами прокси можно так:

```bash
cd /opt/hostpanel && docker compose -f docker-compose.hub.yml logs -f hostpanel-caddy
```

## Без TLS {#without-tls}

<Callout type="note">
Используйте <code>--no-tls</code>, если перед вашим сервером уже стоит TLS-прокси (nginx, Caddy,
балансировщик нагрузки). Он обрабатывает HTTPS, а встроенный пограничный прокси отдаёт обычный HTTP на
порту 8080.
</Callout>

```bash
curl -fsSL https://raw.githubusercontent.com/innovayse/hostpanel/main/install.sh | bash -s -- --no-tls
```

`--no-tls` задаёт в `.env` три значения:

| Настройка | Значение |
|---|---|
| `EDGE_SCHEME` | `http://` |
| `EDGE_HTTP_PORT` | `8080` |
| `EDGE_HTTPS_PORT` | `8443` |

Ваш прокси должен пересылать **оба** имени хостов на порт 8080, сохранять исходный заголовок
`Host` и добавлять `X-Forwarded-Proto: https`: для cookie входа нужен защищённый источник.

Каждый дополнительный прокси перед стеком увеличивает `RATE_LIMIT_TRUSTED_PROXY_COUNT` и
`CLIENT_TRUSTED_PROXY_COUNT` на единицу. Подробности в [справочнике по настройке](/ru/operators/configuration).

## Что записывает установщик {#what-the-installer-writes}

Установщик загружает в каталог установки три файла:

- `docker-compose.hub.yml`: описание стека
- `Caddyfile`: настройка пограничного прокси
- `.env.example`: шаблон настроек

Затем он создаёт `.env` (права `600`) из `.env.example` и задаёт в нём такие значения:

| Настройка | Записываемое значение |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `AUTH_MODE` | `local` |
| `IMAGE_TAG` | версия или `latest` |
| `DOMAIN` | домен вашего клиентского портала |
| `ADMIN_DOMAIN` | домен вашей панели администратора |
| `ADMIN_BASE_URL` | `https://<admin-domain>` |
| `CLIENT_BASE_URL` | `https://<domain>` |
| `NUXT_PUBLIC_BASE_URL` | `https://<domain>` |
| `ALLOWED_HOSTS` | `<domain>;<admin-domain>;hostpanel-api;localhost` |
| `ACME_EMAIL` | адрес электронной почты администратора |
| `SMTP_PORT` | `587` |
| `SMTP_ENCRYPTION` | `tls` |
| `SMTP_FROM` | адрес электронной почты администратора |
| `SMTP_FROM_NAME` | ваш домен |
| `EMAIL_TO` | адрес электронной почты администратора |

И такие секреты, свежесгенерированные командой `openssl rand`:

| Секрет | Как создаётся |
|---|---|
| `JWT_SECRET` | `openssl rand -hex 32` |
| `ENCRYPTION_KEY` | `openssl rand -base64 32` |
| `SESSION_ENCRYPTION_KEY` | `openssl rand -base64 32` |
| `HOSTPANEL_BACKCHANNEL_LOGOUT_KEY` | `openssl rand -hex 24` |
| `POSTGRES_PASSWORD` | `openssl rand -hex 24` |
| `RABBITMQ_PASSWORD` | `openssl rand -hex 24` |

После записи `.env` установщик загружает образы, запускает стек и до пяти минут ждёт,
пока `GET /health` вернёт `200`.

<Callout type="danger">
Храните <code>.env</code> и <code>ENCRYPTION_KEY</code> в безопасном месте. Без того же ключа восстановленная копия не сможет прочитать пароли серверов, API-токены и хеши доступа, хранящиеся в базе данных. Сохраняйте <code>.env</code> вместе с каждым дампом базы данных. См. <a href="/ru/operators/backup">Резервное копирование и восстановление</a>.
</Callout>

## Установка вручную через docker compose {#install-by-hand-with-docker-compose}

Если вы не хотите использовать установочный скрипт:

**1. Загрузите три файла в пустой каталог:**

```bash
BASE=https://raw.githubusercontent.com/innovayse/hostpanel/main
mkdir -p /opt/hostpanel && cd /opt/hostpanel
curl -fsSL "$BASE/docker/docker-compose.hub.yml" \
  -o docker-compose.hub.yml
curl -fsSL "$BASE/docker/caddy/Caddyfile" -o Caddyfile
curl -fsSL "$BASE/docker/.env.example" -o .env.example
```

**2. Скопируйте `.env.example` в `.env` с ограниченными правами:**

```bash
cp .env.example .env && chmod 600 .env
```

**3. Отредактируйте `.env`.** В примере указаны `IMAGE_TAG=local`, `ALLOWED_HOSTS=panel.example.test`,
`POSTGRES_DB=innovayse_dev`, шаблонный `ENCRYPTION_KEY` и `SMTP_HOST=hostpanel-mailhog`.
Если оставить их без изменений, загрузка образов завершится ошибкой, а при запуске значения будут отклонены.
Задайте как минимум:

| Настройка | Пример значения |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `IMAGE_TAG` | `latest` |
| `DOMAIN` | `panel.example.com` |
| `ADMIN_DOMAIN` | `admin.example.com` |
| `ADMIN_BASE_URL` | `https://admin.example.com` |
| `CLIENT_BASE_URL` | `https://panel.example.com` |
| `NUXT_PUBLIC_BASE_URL` | `https://panel.example.com` |
| `ALLOWED_HOSTS` | `panel.example.com;admin.example.com;hostpanel-api;localhost` |
| `ACME_EMAIL` | `you@example.com` |
| `EMAIL_TO` | `you@example.com` |
| `SMTP_HOST` | `smtp.example.com` |
| `SMTP_ENCRYPTION` | `tls` |
| `POSTGRES_DB` | `hostpanel` |

Сгенерируйте обязательные секреты:

```bash
openssl rand -hex 32     # paste as JWT_SECRET
openssl rand -base64 32  # paste as ENCRYPTION_KEY
openssl rand -base64 32  # paste as SESSION_ENCRYPTION_KEY
openssl rand -hex 24     # paste as HOSTPANEL_BACKCHANNEL_LOGOUT_KEY
openssl rand -hex 24     # paste as POSTGRES_PASSWORD
openssl rand -hex 24     # paste as RABBITMQ_PASSWORD
```

Все настройки описаны в [справочнике по настройке](/ru/operators/configuration).

**4. Загрузите образы и запустите стек:**

```bash
docker compose -f docker-compose.hub.yml pull
docker compose -f docker-compose.hub.yml up -d
```

## Настройте почту {#set-up-mail}

<Callout type="warning">
Установщик намеренно оставляет <code>SMTP_HOST</code> пустым. Пока вы не укажете настоящий почтовый
ретранслятор, письма с подтверждением, сбросом пароля и счетами не отправляются.
</Callout>

Отредактируйте `/opt/hostpanel/.env` и задайте:

| Настройка | Описание |
|---|---|
| `SMTP_HOST` | Хост вашего ретранслятора, например `smtp.example.com` |
| `SMTP_PORT` | Обычно `587` (STARTTLS) или `465` (неявный TLS) |
| `SMTP_USERNAME` | Имя пользователя ретранслятора |
| `SMTP_PASSWORD` | Пароль ретранслятора |
| `SMTP_ENCRYPTION` | `tls` (STARTTLS), `ssl` (неявный TLS) или `none` |
| `SMTP_FROM` | Адрес отправителя, например `noreply@example.com` |
| `SMTP_FROM_NAME` | Отображаемое имя отправителя, например `Example Hosting` |

Затем перезапустите стек, чтобы изменения вступили в силу:

```bash
cd /opt/hostpanel && docker compose -f docker-compose.hub.yml up -d
```

При запуске API проверяет `SMTP_ENCRYPTION` и, если значение неверное, называет ключ в журнале.
См. [Устранение неполадок](#troubleshooting).

## Первый запуск {#first-run}

<Label type="local" /> <Label type="admin" />

Если роль администратора пока никому не принадлежит, вместо страницы входа панель администратора
показывает экран настройки из двух шагов с заголовком **Начальная настройка**.

**Шаг 1 из 2: создайте первую учётную запись в этой установке**

Заполните поля **Имя**, **Фамилия**, **Адрес электронной почты** и **Пароль**, затем
нажмите **Создать учётную запись**.

**Шаг 2 из 2: получите роль администратора**

Вы уже вошли в систему. На экране показано «Вы вошли как `<ваш адрес>`.» и поле
**Токен настройки** (подсказка: «Вставьте токен из журнала сервера»).

Установщик вывел токен в конце своего вывода. Если вы закрыли тот терминал, прочитайте
токен в журнале API:

```bash
cd /opt/hostpanel
docker compose -f docker-compose.hub.yml logs hostpanel-api \
  | grep "setup token"
```

Строка выглядит так:

```
FIRST-RUN SETUP IS OUTSTANDING. Nobody holds the Admin role yet.
Open the admin panel, sign in, and paste this setup token when
it asks for one: <token>
```

<Callout type="tip">
Пока настройка не завершена, при каждом перезапуске API снова выводит токен настройки в свой журнал.
</Callout>

Вставьте токен и нажмите **Получить роль администратора**. Откроется панель управления
администратора, а экран настройки больше не появится.

**Сообщения об ошибках на этом экране:**

| Ошибка | Что она означает |
|---|---|
| «Этот токен настройки не совпадает с выданным...» | Неверный или устаревший токен. Перезапустите API и возьмите самый свежий из журнала. |
| «Эта установка уже настроена, и роль администратора уже кому-то принадлежит.» | Настройка завершена. Войдите как обычно или попросите доступ у действующего администратора. |
| «Неверный адрес электронной почты или пароль.» | Неверные учётные данные на странице входа после завершения настройки. |

Проверить, не завершена ли настройка, можно и не открывая панель администратора:

```bash
curl https://admin.example.com/api/auth/setup-required
```

Возвращает `true`, пока роль администратора не получена, и `false`, когда настройка завершена.

## После установки {#after-the-install}

Убедитесь, что всё работает:

- Оба URL, клиентского портала и панели администратора, открываются в браузере.
- Вы можете войти в панель администратора.
- `GET /health` возвращает `200`.

## Устранение неполадок {#troubleshooting}

**API не запускается**

При запуске API проверяет секреты и настройки и пишет в журнал точный ключ, не прошедший
проверку. Прочитайте журнал:

```bash
cd /opt/hostpanel && docker compose -f docker-compose.hub.yml logs hostpanel-api
```

Частые причины:

| Признак в журнале | Причина |
|---|---|
| Секрет всё ещё `CHANGE_ME` или слишком короткий | В `JWT_SECRET`, `ENCRYPTION_KEY`, `SESSION_ENCRYPTION_KEY` или `HOSTPANEL_BACKCHANNEL_LOGOUT_KEY` остался шаблон. Сгенерируйте настоящее значение командой `openssl rand`. |
| Неверный `SMTP_ENCRYPTION` | Значение не задано, написано с ошибкой или не входит в `tls`, `ssl`, `none`. |
| Задана только одна настройка необязательной пары | Например, задан `TELEGRAM_BOT_TOKEN`, а `TELEGRAM_CHAT_ID` пуст. Задайте обе или очистите обе. |

**Сертификаты не выпускаются**

DNS обоих хостов должен указывать на этот сервер до первого запроса. Проверьте
командой `dig panel.example.com` и запустите установщик заново, когда DNS будет готов.

**API не стал работоспособным вовремя**

Установщик ждёт до пяти минут. Если время вышло, проверьте журнал API на ошибки
запуска (см. выше) и журнал базы данных:

```bash
cd /opt/hostpanel && docker compose -f docker-compose.hub.yml logs hostpanel-db
```

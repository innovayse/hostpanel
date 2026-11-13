---
title: Локальный запуск Hostpanel
description: Запуск полного стека разработки на вашем компьютере с помощью Docker Compose.
---

# Локальный запуск Hostpanel {#run-hostpanel-locally}

## Что вам понадобится {#what-you-need}

- Контейнерный движок с поддержкой Compose
- Около 8 ГБ свободной памяти
- Свободные порты `3001`, `5174` и `5149` на хосте

## Клонирование репозитория {#clone-the-repository}

SDK плагинов подключён как git-субмодуль. Клонируйте с `--recurse-submodules`, чтобы получить его за один шаг:

```bash
git clone --recurse-submodules https://github.com/innovayse/hostpanel
```

Если вы уже клонировали без этого флага:

```bash
git submodule update --init
```

## Запуск стека {#start-the-stack}

<Steps>
<li>

Перейдите в каталог docker:

```bash
cd docker
```

</li>
<li>

Скопируйте пример файла окружения:

```bash
cp .env.example .env
```

</li>
<li>

Запустите все сервисы:

```bash
docker compose -f docker-compose.shared.yml -f docker-compose.dev.yml up -d
```

Будут запущены сервисы: `hostpanel-db`, `hostpanel-api`, `hostpanel-client`, `hostpanel-admin`, а также RabbitMQ, Redis и MailHog.

</li>
<li>

Следите за журналом API, чтобы увидеть запуск и миграции:

```bash
./scripts/manage.sh logs api
```

Дождитесь строки «Now listening on», прежде чем открывать приложения.

</li>
</Steps>

## Откройте приложения {#open-it}

<SettingTable :settings="[
  { name: 'CLIENT_HOST_PORT', type: 'number', default: '3001', description: 'Клиентский портал: http://localhost:3001' },
  { name: 'ADMIN_HOST_PORT', type: 'number', default: '5174', description: 'Панель администратора: http://localhost:5174' },
  { name: 'API_HOST_PORT', type: 'number', default: '5149', description: 'API и страница OpenAPI: http://localhost:5149/swagger' },
  { name: 'MAILHOG_UI_HOST_PORT', type: 'number', default: '8027', description: 'Интерфейс локального почтового ящика: http://localhost:8027' },
]" />

Начальные данные для разработки создают такие учётные записи:

| Учётная запись | Email | Переменная окружения с паролем |
|---------|-------|-----------------|
| Администратор | `admin@example.test` | `DEV_SEED_ADMIN_PASSWORD` (по умолчанию: `Admin123!`) |
| Клиент | `owner@example.test` и другие | `DEV_SEED_CLIENT_PASSWORD` (по умолчанию: `Test@12345`) |

## Горячая перезагрузка {#hot-reload}

Контейнер API монтирует дерево исходников и пересобирается при изменении файлов. Опрос файлов (polling) включён по умолчанию в `.env.example`, чтобы покрыть файловые системы хоста, которые ненадёжно отправляют события отслеживания.

<Callout type="warning">
Никогда не запускайте <code>dotnet build</code>, <code>dotnet restore</code>, <code>npm install</code> и подобное на хосте, пока работает стек разработки. Контейнер и хост будут бороться за одни и те же файлы.
</Callout>

## Без контейнеров {#without-containers}

Если вы предпочитаете запускать стек напрямую, вам понадобятся .NET 9 SDK, Node.js 22+, yarn и PostgreSQL 17.

```bash
cd backend && dotnet run --project src/Innovayse.Hostpanel.API   # http://localhost:5148
cd client  && yarn install && yarn dev                            # http://localhost:3000
cd admin   && npm install && npm run dev                          # http://localhost:5173
```

Задайте строку подключения API, настройки RabbitMQ и SMTP через переменные окружения, пользовательские секреты (user secrets) или `appsettings.Development.json` (он игнорируется git).

## Сброс данных {#reset-the-data}

Используйте скрипт управления из каталога `docker/`:

```bash
./scripts/manage.sh db reset
```

Скрипт запросит подтверждение, удалит базу данных и применит все миграции. Чтобы после этого наполнить базу данными для разработки:

```bash
./scripts/manage.sh db seed
```

## Устранение неполадок {#troubleshooting}

| Проблема | Решение |
|---------|-----|
| Порт уже занят | Измените `API_HOST_PORT`, `CLIENT_HOST_PORT` или `ADMIN_HOST_PORT` в `.env` |
| Конфликт подсетей с другой сетью | Измените `STACK_NETWORK_SUBNET` в `.env` (по умолчанию `172.30.0.0/24`) |

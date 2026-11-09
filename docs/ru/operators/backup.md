---
title: Резервное копирование и восстановление
description: Как создать резервную копию базы данных и томов Hostpanel и как восстановить их.
---

# Резервное копирование и восстановление {#back-up-and-restore}

Как сделать дамп базы данных, сохранить тома с загруженными файлами и восстановить
их на том же или на другом сервере.

## Что копировать {#what-to-back-up}

| Что | Зачем |
|---|---|
| `/opt/hostpanel/.env` | Содержит `ENCRYPTION_KEY`. Без него зашифрованные столбцы после восстановления прочитать нельзя. |
| Том `hostpanel_pgdata` (или SQL-дамп) | Все данные приложения: клиенты, заказы, счета, услуги, настройки. |
| Том `hostpanel_hostpanel_branding` | Логотипы, фавиконки и другие файлы брендинга, загруженные в панели администратора. |
| Том `hostpanel_hostpanel_plugins` | Плагины, установленные через панель администратора. |

Тома Redis и RabbitMQ хранят временные данные (сессии, сообщения в очереди),
их копировать не нужно.

Том `hostpanel_caddydata` хранит выпущенные TLS-сертификаты. Если его потерять,
сертификаты будут выпущены заново при следующем запуске, и можно упереться в
ограничения Let's Encrypt. Сохраняйте его, если хотите избежать повторного выпуска,
но для восстановления данных он не обязателен.

<Callout type="danger">
Без того же <code>ENCRYPTION_KEY</code> из <code>.env</code> восстановленную базу данных
не прочитать: пароли серверов, API-токены и хеши доступа хранятся в зашифрованном виде.
Всегда сохраняйте <code>.env</code> вместе с каждым дампом.
</Callout>

Потеря `SESSION_ENCRYPTION_KEY` менее опасна: она лишь завершает сессии всех
пользователей клиентского портала.

## Сделайте дамп базы данных {#take-a-database-dump}

**1. Запустите `pg_dump` в работающем контейнере `hostpanel-db`:**

```bash
umask 077
cd /opt/hostpanel
docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
  sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' \
  > backup-$(date +%Y-%m-%d-%H%M%S).sql
```

**2. Скопируйте дамп и `.env` во внешнее хранилище** (объектное хранилище, сервис резервного копирования вне сервера).

Можно также остановить стек и сделать снимок Docker-тома `hostpanel_pgdata`
целиком: так сохранятся исходные файлы данных.

## Восстановите дамп {#restore-a-dump}

<Callout type="danger">
Восстановление дампа безвозвратно удаляет всё, что было записано после его создания: заказы, платежи, тикеты и любые другие данные. Перед началом убедитесь, что выбран нужный файл дампа.
</Callout>

**1. Остановите контейнеры приложения** (базу данных оставьте работать):

```bash
cd /opt/hostpanel
docker compose -f docker-compose.hub.yml stop \
  hostpanel-api hostpanel-client hostpanel-admin hostpanel-caddy
```

**2. Снимите дамп текущего состояния базы** до того, как перезаписывать её:

```bash
umask 077
docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
  sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' \
  > pre-restore-$(date +%Y-%m-%d-%H%M%S).sql
```

**3. Проверьте файл резервной копии, затем удалите базу и восстановите её:**

Замените `backup-2024-01-15-030000.sql` настоящим именем файла.

```bash
if ! test -s backup-2024-01-15-030000.sql; then
  echo "Backup file is empty or missing, aborting."
else
  docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
    sh -c 'dropdb -U "$POSTGRES_USER" "$POSTGRES_DB" && \
           createdb -U "$POSTGRES_USER" "$POSTGRES_DB"'
  docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
    sh -c 'psql -v ON_ERROR_STOP=1 --single-transaction \
           -U "$POSTGRES_USER" "$POSTGRES_DB"' < backup-2024-01-15-030000.sql
fi
```

**4.** Убедитесь, что в `.env` указан тот же `ENCRYPTION_KEY`, который использовался при создании дампа.

**5. Восстановите тома branding и plugins:**

```bash
# Restore branding files:
docker run --rm \
  -v hostpanel_hostpanel_branding:/data \
  -v /path/to/your/backups:/backup \
  alpine sh -c 'cd /data && tar xzf /backup/branding-2024-01-15.tar.gz'

# Restore plugins:
docker run --rm \
  -v hostpanel_hostpanel_plugins:/data \
  -v /path/to/your/backups:/backup \
  alpine sh -c 'cd /data && tar xzf /backup/plugins-2024-01-15.tar.gz'

# Fix ownership so the API can read the restored files:
cd /opt/hostpanel
docker compose -f docker-compose.hub.yml run --rm --no-deps \
  --user 0 --cap-add CHOWN --entrypoint sh hostpanel-api \
  -c 'chown -R "$APP_UID" /app/wwwroot/uploads /app/plugins-installed'
```

**6. Запустите весь стек:**

```bash
docker compose -f docker-compose.hub.yml up -d
```

**7.** Проверьте, что `GET /health` возвращает `200` и что вы можете войти в систему.

## Настройте регулярное копирование {#schedule-backups}

Проще всего запускать копирование каждую ночь заданием cron на хосте.

**1. Создайте каталог для копий:**

```bash
mkdir -p /var/backups/hostpanel && chmod 700 /var/backups/hostpanel
```

**2. Создайте `/usr/local/bin/hostpanel-backup`:**

```bash
#!/bin/bash
set -eu
umask 077
DEST=/var/backups/hostpanel/db-$(date +%Y-%m-%d-%H%M%S).sql
cd /opt/hostpanel

# Database dump
docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
  sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' > "$DEST"
if ! test -s "$DEST"; then
  echo "pg_dump produced an empty file, aborting" >&2
  rm -f "$DEST"
  exit 1
fi
chmod 600 "$DEST"

# Branding and plugins volumes
BRAND_ARCHIVE=/var/backups/hostpanel/branding-$(date +%Y-%m-%d).tar.gz
PLUGIN_ARCHIVE=/var/backups/hostpanel/plugins-$(date +%Y-%m-%d).tar.gz

docker run --rm \
  -v hostpanel_hostpanel_branding:/data \
  -v /var/backups/hostpanel:/backup \
  alpine tar czf /backup/branding-$(date +%Y-%m-%d).tar.gz -C /data .
chmod 600 "$BRAND_ARCHIVE"

docker run --rm \
  -v hostpanel_hostpanel_plugins:/data \
  -v /var/backups/hostpanel:/backup \
  alpine tar czf /backup/plugins-$(date +%Y-%m-%d).tar.gz -C /data .
chmod 600 "$PLUGIN_ARCHIVE"

# Remove backups older than 30 days
find /var/backups/hostpanel -name 'db-*.sql' -mtime +30 -delete
find /var/backups/hostpanel -name 'branding-*.tar.gz' -mtime +30 -delete
find /var/backups/hostpanel -name 'plugins-*.tar.gz' -mtime +30 -delete
```

```bash
chmod +x /usr/local/bin/hostpanel-backup
```

**3. Создайте `/etc/cron.d/hostpanel-backup`:**

```
0 3 * * * root /usr/local/bin/hostpanel-backup
```

После каждого запуска копируйте дампы и `.env` во внешнее хранилище.

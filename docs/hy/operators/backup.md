---
title: Պահուստավորում և վերականգնում
description: Ինչպես պահուստավորել Hostpanel-ի տվյալների բազան և ծավալները (volumes), և ինչպես վերականգնել դրանք։
---

# Պահուստավորում և վերականգնում {#back-up-and-restore}

Ինչպես վերցնել տվյալների բազայի դամփ, պահուստավորել վերբեռնված ֆայլերը պահող ծավալները և
վերականգնել դրանք նույն կամ մեկ այլ սերվերում։

## Ինչ պահուստավորել {#what-to-back-up}

| Տարր | Ինչու |
|---|---|
| `/opt/hostpanel/.env` | Պարունակում է `ENCRYPTION_KEY`-ը. առանց դրա գաղտնագրված սյունակները վերականգնումից հետո չեն կարդացվի։ |
| `hostpanel_pgdata` ծավալ (կամ SQL դամփ) | Հավելվածի ամբողջ տվյալները՝ հաճախորդներ, պատվերներ, հաշիվ-ապրանքագրեր, ծառայություններ, կարգավորումներ։ |
| `hostpanel_hostpanel_branding` ծավալ | Ադմինի վահանակում վերբեռնված լոգոներ, favicon-ներ և բրենդինգի այլ ֆայլեր։ |
| `hostpanel_hostpanel_plugins` ծավալ | Ադմինի վահանակով տեղադրված պլագինները։ |

Redis-ի և RabbitMQ-ի ծավալները պահում են ժամանակավոր տվյալներ (սեսիաներ, հերթում գտնվող
հաղորդագրություններ) և պահուստավորման կարիք չունեն։

`hostpanel_caddydata` ծավալը պահում է տրված TLS վկայականները։ Այն կորցնելը հաջորդ գործարկման ժամանակ
առաջացնում է վերատրում, որը կարող է հասնել Let's Encrypt-ի սահմանափակումներին։ Պահուստավորեք այն,
եթե ուզում եք խուսափել վերատրումից, բայց տվյալների վերականգնման համար այն պարտադիր չէ։

<Callout type="danger">
Առանց <code>.env</code>-ից նույն <code>ENCRYPTION_KEY</code>-ի վերականգնված տվյալների բազան չի կարդացվի. սերվերների գաղտնաբառերը, API թոքենները և հասանելիության հեշերը պահվում են գաղտնագրված։
Միշտ պահուստավորեք <code>.env</code>-ը յուրաքանչյուր դամփի հետ միասին։
</Callout>

`SESSION_ENCRYPTION_KEY`-ը կորցնելն ավելի թեթև հետևանք ունի. այն միայն բոլորին դուրս է բերում
հաճախորդի պորտալից։

## Վերցրեք տվյալների բազայի դամփ {#take-a-database-dump}

**1. Գործարկեք `pg_dump`-ը աշխատող `hostpanel-db` կոնտեյների դեմ՝**

```bash
umask 077
cd /opt/hostpanel
docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
  sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' \
  > backup-$(date +%Y-%m-%d-%H%M%S).sql
```

**2. Դամփը և `.env`-ը պատճենեք արտաքին պահոցում** (օբյեկտային պահոց, արտաքին պահուստավորման ծառայություն)։

Որպես այլընտրանք՝ կանգնեցրեք համակարգը և անմիջապես պատճենեք `hostpanel_pgdata` Docker ծավալը,
ինչը պահպանում է տվյալների չմշակված ֆայլերը։

## Վերականգնեք դամփը {#restore-a-dump}

<Callout type="danger">
Դամփի վերականգնումը վերջնականապես հեռացնում է դամփը վերցնելուց հետո գրված ամեն ինչ՝ պատվերներ, վճարումներ, աջակցության հայտեր և ցանկացած այլ տվյալ։ Շարունակելուց առաջ համոզվեք, որ ճիշտ դամփի ֆայլ ունեք։
</Callout>

**1. Կանգնեցրեք հավելվածի կոնտեյները** (տվյալների բազան թողեք աշխատող)՝

```bash
cd /opt/hostpanel
docker compose -f docker-compose.hub.yml stop \
  hostpanel-api hostpanel-client hostpanel-admin hostpanel-caddy
```

**2. Վերագրելուց առաջ վերցրեք տվյալների բազայի ընթացիկ վիճակի դամփը՝**

```bash
umask 077
docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
  sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' \
  > pre-restore-$(date +%Y-%m-%d-%H%M%S).sql
```

**3. Ստուգեք պահուստային ֆայլը, ապա ջնջեք և վերականգնեք՝**

`backup-2024-01-15-030000.sql`-ը փոխարինեք փաստացի ֆայլի անունով։

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

**4.** Համոզվեք, որ `.env`-ը պարունակում է նույն `ENCRYPTION_KEY`-ը, որն օգտագործվել էր դամփը վերցնելիս։

**5. Վերականգնեք branding և plugins ծավալները՝**

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

**6. Գործարկեք ամբողջ համակարգը՝**

```bash
docker compose -f docker-compose.hub.yml up -d
```

**7.** Ստուգեք, որ `GET /health`-ը վերադարձնում է `200` և որ կարող եք մուտք գործել։

## Պահուստավորման ժամանակացույց {#schedule-backups}

Հոսթի վրա գիշերային cron աշխատանքը ամենապարզ եղանակն է։

**1. Ստեղծեք պահուստային պատճենների թղթապանակը՝**

```bash
mkdir -p /var/backups/hostpanel && chmod 700 /var/backups/hostpanel
```

**2. Ստեղծեք `/usr/local/bin/hostpanel-backup`՝**

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

**3. Ստեղծեք `/etc/cron.d/hostpanel-backup`՝**

```
0 3 * * * root /usr/local/bin/hostpanel-backup
```

Յուրաքանչյուր գործարկումից հետո դամփերը և `.env`-ը պատճենեք արտաքին պահոցում։

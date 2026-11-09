---
title: Hostpanel-ի տեղադրում
description: Hostpanel-ը Linux սերվերում տեղադրելու քայլ առ քայլ ուղեցույց՝ մեկ հրամանով տեղադրիչի կամ docker compose-ի միջոցով։
---

# Hostpanel-ի տեղադրում {#install-hostpanel}

Ինչպես գործարկել Hostpanel-ը ձեր սերվերում՝ մեկ հրամանով տեղադրիչով կամ ձեռքով՝
docker compose-ի միջոցով։

## Սկսելուց առաջ {#before-you-start}

Ձեզ անհրաժեշտ է՝

- Linux սերվեր, որտեղ տեղադրված են [Docker](https://docs.docker.com/engine/install/)-ը և
  [Compose v2 plugin](https://docs.docker.com/compose/install/)-ը, և daemon-ը աշխատում է։
  Տեղադրիչը ստուգում է `docker`, `docker compose`, `curl` և `openssl` գործիքների
  առկայությունը և անվանում է բացակայողներին։
- Երկու DNS հոսթ-անուն, որոնք ցույց են տալիս ձեր սերվերը՝ մեկը հաճախորդի պորտալի համար
  (օրինակ՝ `panel.example.com`), մյուսը՝ ադմինի վահանակի համար (օրինակ՝ `admin.example.com`)։
  Դրանք պետք է տարբեր լինեն. ադմինի վահանակը սպասարկվում է արմատային ուղուց և իր սեփական
  հոսթն է պահանջում։
- Էլ. փոստի հասցե ադմինիստրատորի հաշվի, կապի ձևի ստացողի և Let's Encrypt
  վկայականի համար։

Եթե չեք օգտագործում [`--no-tls`](#without-tls)-ը, տեղադրիչը գործարկելուց առաջ սերվերի
**80** և **443** նավահանգիստները պետք է ազատ լինեն և հասանելի դրսից։

## Գործարկեք տեղադրիչը {#run-the-installer}

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

Խորհուրդ ենք տալիս մեկ հրամանով տեղադրիչը. այն ինքն է ստեղծում բոլոր գաղտնիքները,
սահմանում է յուրաքանչյուր պարտադիր արժեք և սպասում է, մինչև համակարգը դառնա առողջ։
docker compose-ի և `.env`-ի ներդիրները նախատեսված են այն օպերատորների համար, ովքեր
սկրիպտավորված կամ անհատական տեղադրում են անում. ամբողջ քայլերը տե՛ս
[Ձեռքով տեղադրում docker compose-ով](#install-by-hand-with-docker-compose) բաժնում։

**1.** Երկու հոսթ-անունների DNS-ը ուղղեք ձեր սերվերին և սպասեք, մինչև փոփոխությունը տարածվի։

**2. Գործարկեք տեղադրիչը՝**

```bash
curl -fsSL \
  https://raw.githubusercontent.com/innovayse/hostpanel/main/install.sh \
  | bash
```

**3. Պատասխանեք երեք հարցերին՝**

| Հարց | Օրինակ |
|---|---|
| `Domain of the client portal (e.g. panel.example.com)` | `panel.example.com` |
| `Domain of the admin panel` (լռելյայն՝ `admin.<domain>`) | `admin.example.com` |
| `Admin e-mail (contact form target and mail sender)` | `you@example.com` |

**4.** Տեղադրիչը ներբեռնում է պատկերները (images), գործարկում է համակարգը և սպասում է
`/health`-ին։ Ավարտելիս այն տպում է երկու URL-ները և մեկանգամյա կարգավորման թոքեն՝

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

**5.** Բացեք ադմինի վահանակի URL-ը, ստեղծեք ձեր հաշիվը և տեղադրեք կարգավորման թոքենը։
Տե՛ս [Առաջին գործարկում](#first-run)։

## Տեղադրիչի ընտրանքներ {#installer-options}

Դրոշակները փոխանցեք հրամանի տողում կամ համապատասխան միջավայրի փոփոխականը սահմանեք
խողովակի առջև՝ `bash`-ից առաջ.

<SettingTable :settings="[
  { name: '--dir &lt;path&gt; / HOSTPANEL_DIR', type: 'string', default: '/opt/hostpanel', description: 'Տեղադրիչի ֆայլերը ներբեռնելու և համակարգը գործարկելու թղթապանակը։' },
  { name: '--version &lt;x.y.z&gt; / HOSTPANEL_VERSION', type: 'string', default: 'latest', description: 'Տեղադրվող տարբերակը, օրինակ՝ 0.4.0։ Առաջատար v տառը թույլատրվում է։ Վերջին տարբերակի համար բաց թողեք։' },
  { name: '--no-tls / HOSTPANEL_TLS=off', type: 'flag', default: 'on', description: 'Սպասարկել պարզ HTTP 8080 նավահանգստով՝ ձեր սեփական TLS պրոքսիի հետևում, ավտոմատ վկայականներ ստանալու փոխարեն։ Տե՛ս Առանց TLS-ի։' },
  { name: '--rollback', type: 'flag', default: 'none', description: 'Վերադառնալ այլ տարբերակի ամենաթարմ պահուստային պատճենի հետ գրանցված տարբերակին և տվյալների բազային։ Հարցնում է հաստատում։ Տե՛ս Հետգրում։' },
  { name: '--yes', type: 'flag', default: 'none', description: 'Հետգրումը նախապես հաստատել՝ սկրիպտների համար։' },
  { name: '--keep &lt;n&gt; / HOSTPANEL_BACKUP_KEEP', type: 'number', default: '5', description: 'Թարմացման պահուստային պատճեններից քանի՞ ամենաթարմը պահել backups թղթապանակում։ Ամենաթարմ տարբերակներից յուրաքանչյուրի ամենահին պահուստային պատճենը նույնպես պահվում է (տե՛ս --keep-versions)։ Ավելի հները ջնջվում են։' },
  { name: '--keep-versions &lt;n&gt; / HOSTPANEL_BACKUP_KEEP_VERSIONS', type: 'number', default: '10', description: 'Ամենաթարմ քանի՞ տարբերակի համար է պահվում ամենահին պահուստային պատճենը՝ ամենաթարմների կողքին։ Ամենաթարմ տարբերակները նրանք են, որոնք ունեն ամենավերջին պահուստային պատճենը, ոչ թե տարբերակի ամենաբարձր համարը։ Ավելի հին տարբերակները կորցնում են իրենցը, ուստի backups թղթապանակը չի մեծանում յուրաքանչյուր թողարկման հետ։' },
  { name: '--skip-backup', type: 'flag', default: 'none', description: 'Թարմացում առանց տվյալների բազայի պահուստային պատճենի։ Տվյալները պաշտպանված չեն. օգտագործեք միայն այն դեպքում, երբ պահելու բան չկա։' },
  { name: '--help / -h', type: 'flag', default: '—', description: 'Տպել օգտագործման ձևը և դուրս գալ։' },
  { name: 'HOSTPANEL_NONINTERACTIVE=1', type: 'env', default: '—', description: 'Բաց թողնել ինտերակտիվ հարցերը։ Պահանջում է սահմանված HOSTPANEL_DOMAIN և HOSTPANEL_ADMIN_EMAIL։' },
  { name: 'HOSTPANEL_DOMAIN', type: 'env', default: '—', description: 'Հաճախորդի պորտալի հանրային հոսթ-անունը։ Պարտադիր է ոչ ինտերակտիվ գործարկման համար։' },
  { name: 'HOSTPANEL_ADMIN_DOMAIN', type: 'env', default: 'admin.&lt;domain&gt;', description: 'Ադմինի վահանակի հանրային հոսթ-անունը։' },
  { name: 'HOSTPANEL_ADMIN_EMAIL', type: 'env', default: '—', description: 'Ադմինիստրատորի էլ. փոստի հասցեն։ Պարտադիր է ոչ ինտերակտիվ գործարկման համար։' },
]" />

**Ոչ ինտերակտիվ օրինակ** (CI/CD կամ պրովիժնինգի սկրիպտ).

```bash
curl -fsSL https://raw.githubusercontent.com/innovayse/hostpanel/main/install.sh \
  | HOSTPANEL_DOMAIN=panel.example.com \
    HOSTPANEL_ADMIN_EMAIL=you@example.com \
    HOSTPANEL_NONINTERACTIVE=1 \
    bash
```

## Ավտոմատ TLS-ով (լռելյայն) {#with-automatic-tls-default}

Ներկառուցված [Caddy](https://caddyserver.com/) եզրային պրոքսին առաջին հարցման ժամանակ
Let's Encrypt-ից վկայականներ է պահանջում երկու հոսթ-անունների համար։ `ACME_EMAIL`-ը
սահմանվում է ձեր մուտքագրած ադմինի էլ. փոստով։

Տեղադրիչը գործարկելուց առաջ երկու հոսթերի DNS-ը **պետք է ցույց տա ձեր սերվերը**, այլապես
վկայականի հարցումը կձախողվի։ Եզրային պրոքսիի մատյանները դիտեք այսպես՝

```bash
cd /opt/hostpanel && docker compose -f docker-compose.hub.yml logs -f hostpanel-caddy
```

## Առանց TLS-ի {#without-tls}

<Callout type="note">
Օգտագործեք <code>--no-tls</code>, երբ ձեր սերվերի առջև արդեն կա TLS պրոքսի (nginx, Caddy, բեռի հավասարակշռիչ)։ Այդ պրոքսին է սպասարկում HTTPS-ը, իսկ ներկառուցված եզրային պրոքսին սպասարկում է պարզ HTTP 8080 նավահանգստով։
</Callout>

```bash
curl -fsSL https://raw.githubusercontent.com/innovayse/hostpanel/main/install.sh | bash -s -- --no-tls
```

`--no-tls`-ը `.env`-ում սահմանում է երեք արժեք՝

| Կարգավորում | Արժեք |
|---|---|
| `EDGE_SCHEME` | `http://` |
| `EDGE_HTTP_PORT` | `8080` |
| `EDGE_HTTPS_PORT` | `8443` |

Ձեր պրոքսին պետք է **երկու** հոսթ-անունները փոխանցի 8080 նավահանգստին, պահի սկզբնական
`Host` վերնագիրը և ավելացնի `X-Forwarded-Proto: https`. մուտքի cookie-ները պահանջում են
անվտանգ origin։

Համակարգի առջև դրված յուրաքանչյուր լրացուցիչ պրոքսի `RATE_LIMIT_TRUSTED_PROXY_COUNT`-ը և
`CLIENT_TRUSTED_PROXY_COUNT`-ը մեկով ավելացնում է։ Մանրամասները տե՛ս
[կարգավորումների տեղեկատուում](/hy/operators/configuration)։

## Ինչ է գրում տեղադրիչը {#what-the-installer-writes}

Տեղադրիչը տեղադրման թղթապանակ է ներբեռնում երեք ֆայլ՝

- `docker-compose.hub.yml` — համակարգի նկարագրությունը
- `Caddyfile` — եզրային պրոքսիի կարգավորումը
- `.env.example` — կարգավորումների ձևանմուշը

Այնուհետև `.env.example`-ից ստեղծում է `.env` (ռեժիմ `600`)՝ հետևյալ սահմանված արժեքներով՝

| Կարգավորում | Գրվող արժեք |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `AUTH_MODE` | `local` |
| `IMAGE_TAG` | տարբերակը կամ `latest` |
| `DOMAIN` | հաճախորդի պորտալի դոմենը |
| `ADMIN_DOMAIN` | ադմինի վահանակի դոմենը |
| `ADMIN_BASE_URL` | `https://<admin-domain>` |
| `CLIENT_BASE_URL` | `https://<domain>` |
| `NUXT_PUBLIC_BASE_URL` | `https://<domain>` |
| `ALLOWED_HOSTS` | `<domain>;<admin-domain>;hostpanel-api;localhost` |
| `ACME_EMAIL` | ձեր ադմինի էլ. փոստը |
| `SMTP_PORT` | `587` |
| `SMTP_ENCRYPTION` | `tls` |
| `SMTP_FROM` | ձեր ադմինի էլ. փոստը |
| `SMTP_FROM_NAME` | ձեր դոմենը |
| `EMAIL_TO` | ձեր ադմինի էլ. փոստը |

Եվ հետևյալ գաղտնիքները՝ նոր ստեղծված `openssl rand`-ով՝

| Գաղտնիք | Ինչպես է ստեղծվում |
|---|---|
| `JWT_SECRET` | `openssl rand -hex 32` |
| `ENCRYPTION_KEY` | `openssl rand -base64 32` |
| `SESSION_ENCRYPTION_KEY` | `openssl rand -base64 32` |
| `HOSTPANEL_BACKCHANNEL_LOGOUT_KEY` | `openssl rand -hex 24` |
| `POSTGRES_PASSWORD` | `openssl rand -hex 24` |
| `RABBITMQ_PASSWORD` | `openssl rand -hex 24` |

`.env`-ը գրելուց հետո տեղադրիչը ներբեռնում է պատկերները, գործարկում է համակարգը և
մինչև հինգ րոպե սպասում է, որ `GET /health`-ը վերադարձնի `200`։

<Callout type="danger">
Պահեք <code>.env</code>-ը և <code>ENCRYPTION_KEY</code>-ը անվտանգ վայրում։ Առանց նույն բանալու վերականգնումը չի կարող կարդալ տվյալների բազայում պահված սերվերների գաղտնաբառերը, API թոքենները և հասանելիության հեշերը։ <code>.env</code>-ի պահուստային պատճենը պահեք տվյալների բազայի յուրաքանչյուր դամփի կողքին։ Տե՛ս <a href="/hy/operators/backup">Պահուստավորում և վերականգնում</a>։
</Callout>

## Ձեռքով տեղադրում docker compose-ով {#install-by-hand-with-docker-compose}

Եթե չեք ցանկանում օգտագործել տեղադրման սկրիպտը՝

**1. Երեք ֆայլը ներբեռնեք դատարկ թղթապանակ՝**

```bash
BASE=https://raw.githubusercontent.com/innovayse/hostpanel/main
mkdir -p /opt/hostpanel && cd /opt/hostpanel
curl -fsSL "$BASE/docker/docker-compose.hub.yml" \
  -o docker-compose.hub.yml
curl -fsSL "$BASE/docker/caddy/Caddyfile" -o Caddyfile
curl -fsSL "$BASE/docker/.env.example" -o .env.example
```

**2. `.env.example`-ը պատճենեք `.env` անվամբ՝ սահմանափակ իրավունքներով՝**

```bash
cp .env.example .env && chmod 600 .env
```

**3. Խմբագրեք `.env`-ը։** Օրինակը գալիս է `IMAGE_TAG=local`, `ALLOWED_HOSTS=panel.example.test`,
`POSTGRES_DB=innovayse_dev`, `ENCRYPTION_KEY`-ի տեղապահ արժեքով և
`SMTP_HOST=hostpanel-mailhog`-ով. դրանք անփոփոխ թողնելու դեպքում պատկերների ներբեռնումը
կձախողվի կամ գործարկման ժամանակ կմերժվի։ Սահմանեք առնվազն հետևյալը՝

| Կարգավորում | Օրինակելի արժեք |
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

Ստեղծեք պարտադիր գաղտնիքները՝

```bash
openssl rand -hex 32     # paste as JWT_SECRET
openssl rand -base64 32  # paste as ENCRYPTION_KEY
openssl rand -base64 32  # paste as SESSION_ENCRYPTION_KEY
openssl rand -hex 24     # paste as HOSTPANEL_BACKCHANNEL_LOGOUT_KEY
openssl rand -hex 24     # paste as POSTGRES_PASSWORD
openssl rand -hex 24     # paste as RABBITMQ_PASSWORD
```

Բոլոր կարգավորումները տե՛ս [կարգավորումների տեղեկատուում](/hy/operators/configuration)։

**4. Ներբեռնեք պատկերները և գործարկեք համակարգը՝**

```bash
docker compose -f docker-compose.hub.yml pull
docker compose -f docker-compose.hub.yml up -d
```

## Փոստի կարգավորում {#set-up-mail}

<Callout type="warning">
Տեղադրիչը <code>SMTP_HOST</code>-ը միտումնավոր դատարկ է թողնում։ Մինչև իրական ռելե չսահմանեք, ոչ մի հաստատման, գաղտնաբառի վերականգնման կամ հաշիվ-ապրանքագրի նամակ չի ուղարկվի։
</Callout>

Խմբագրեք `/opt/hostpanel/.env`-ը և սահմանեք՝

| Կարգավորում | Նկարագրություն |
|---|---|
| `SMTP_HOST` | Ձեր ռելեի հոսթը, օրինակ՝ `smtp.example.com` |
| `SMTP_PORT` | Սովորաբար `587` (STARTTLS) կամ `465` (implicit TLS) |
| `SMTP_USERNAME` | Ռելեի օգտանունը |
| `SMTP_PASSWORD` | Ռելեի գաղտնաբառը |
| `SMTP_ENCRYPTION` | `tls` (STARTTLS), `ssl` (implicit TLS) կամ `none` |
| `SMTP_FROM` | Ուղարկողի հասցեն, օրինակ՝ `noreply@example.com` |
| `SMTP_FROM_NAME` | Ուղարկողի ցուցադրվող անունը, օրինակ՝ `Example Hosting` |

Փոփոխությունը կիրառելու համար վերագործարկեք համակարգը՝

```bash
cd /opt/hostpanel && docker compose -f docker-compose.hub.yml up -d
```

API-ն գործարկման ժամանակ ստուգում է `SMTP_ENCRYPTION`-ը և սխալի դեպքում մատյանում
նշում է բանալու անունը. տե՛ս [Խնդիրների լուծում](#troubleshooting)։

## Առաջին գործարկում {#first-run}

<Label type="local" /> <Label type="admin" />

Երբ դեռ ոչ ոք չունի Admin դեր, ադմինի վահանակը մուտքի էջի փոխարեն ցույց է տալիս
երկքայլ կարգավորման էկրան՝ **Սկզբնական կարգավորում** վերնագրով։

**Քայլ 1՝ 2-ից — ստեղծեք առաջին հաշիվը այս տեղադրման մեջ**

Լրացրեք **Անուն**, **Ազգանուն**, **Էլ. փոստի հասցե** և **Գաղտնաբառ** դաշտերը, ապա
սեղմեք **Ստեղծել հաշիվ**։

**Քայլ 2՝ 2-ից — ստացեք ադմինիստրատորի դերը**

Դուք արդեն մուտք եք գործել։ Էկրանը ցույց է տալիս «Դուք մուտք եք գործել որպես `<your email>`։»
տողը և **Կարգավորման թոքեն** դաշտը (տեղապահ՝ «Տեղադրեք թոքենը սերվերի մատյանից»)։

Տեղադրիչը թոքենը տպել է իր ելքի վերջում։ Եթե փակել եք տերմինալը, կարդացեք այն API-ի
մատյանից՝

```bash
cd /opt/hostpanel
docker compose -f docker-compose.hub.yml logs hostpanel-api \
  | grep "setup token"
```

Տողն այսպիսին է՝

```
FIRST-RUN SETUP IS OUTSTANDING. Nobody holds the Admin role yet.
Open the admin panel, sign in, and paste this setup token when
it asks for one: <token>
```

<Callout type="tip">
API-ի վերագործարկումը թոքենը նորից տպում է մատյանում, քանի դեռ կարգավորումն ավարտված չէ։
</Callout>

Տեղադրեք թոքենը և սեղմեք **Ստանալ ադմինիստրատորի դերը**։ Ադմինի ամփոփագիրը բացվում է,
և կարգավորման էկրանն այլևս չի երևում։

**Այս էկրանի սխալների հաղորդագրությունները՝**

| Սխալ | Նշանակություն |
|---|---|
| «Այդ կարգավորման նշանը չի համընկնում տրվածին...» | Սխալ կամ հնացած թոքեն։ Վերագործարկեք API-ն և մատյանից կարդացեք ամենանորը։ |
| «Այս տեղադրումը արդեն կարգավորված է, և ադմինիստրատորի դերն արդեն անցել է մեկուն...» | Կարգավորումն ավարտված է։ Մուտք գործեք սովորական կարգով կամ հասանելիություն խնդրեք գործող ադմինիստրատորից։ |
| «Էլ. փոստը կամ գաղտնաբառը սխալ է։» | Կարգավորումն ավարտելուց հետո մուտքի էջում սխալ տվյալներ են մուտքագրվել։ |

Կարող եք ստուգել՝ կարգավորումը դեռ սպասվո՞ւմ է, առանց ադմինի վահանակը բացելու՝

```bash
curl https://admin.example.com/api/auth/setup-required
```

Վերադարձնում է `true`, քանի դեռ Admin դերը ոչ ոք չի ստացել, և `false`, երբ կարգավորումն ավարտված է։

## Տեղադրումից հետո {#after-the-install}

Համոզվեք, որ ամեն ինչ աշխատում է՝

- Հաճախորդի պորտալի և ադմինի վահանակի URL-ները բացվում են բրաուզերում։
- Կարող եք մուտք գործել ադմինի վահանակ։
- `GET /health`-ը վերադարձնում է `200`։

## Խնդիրների լուծում {#troubleshooting}

**API-ն չի գործարկվում**

API-ն գործարկման ժամանակ ստուգում է գաղտնիքներն ու կարգավորումները և մատյանում գրում է
ձախողված բանալին։ Կարդացեք մատյանը՝

```bash
cd /opt/hostpanel && docker compose -f docker-compose.hub.yml logs hostpanel-api
```

Տարածված պատճառներ՝

| Նշան մատյանում | Պատճառ |
|---|---|
| Գաղտնիքը դեռ `CHANGE_ME` է կամ չափազանց կարճ | `JWT_SECRET`-ը, `ENCRYPTION_KEY`-ը, `SESSION_ENCRYPTION_KEY`-ը կամ `HOSTPANEL_BACKCHANNEL_LOGOUT_KEY`-ը տեղապահ արժեք ունի։ Իրական արժեք ստեղծեք `openssl rand`-ով։ |
| `SMTP_ENCRYPTION`-ը անվավեր է | Արժեքը սահմանված չէ, սխալ է գրված կամ `tls`, `ssl`, `none` արժեքներից մեկը չէ։ |
| Կամընտիր զույգից սահմանված է միայն մեկը | օրինակ՝ `TELEGRAM_BOT_TOKEN`-ը սահմանված է, բայց `TELEGRAM_CHAT_ID`-ը դատարկ է։ Սահմանեք երկուսն էլ կամ մաքրեք երկուսն էլ։ |

**Վկայականները չեն տրվում**

Առաջին հարցումից առաջ երկու հոսթերի DNS-ը պետք է ցույց տա այս սերվերը։ Ստուգեք
`dig panel.example.com`-ով և DNS-ը պատրաստ լինելուց հետո նորից գործարկեք տեղադրիչը։

**API-ն ժամանակին չդարձավ առողջ**

Տեղադրիչը սպասում է մինչև հինգ րոպե։ Եթե ժամանակը սպառվի, ստուգեք API-ի մատյանը՝
գործարկման սխալների համար (տե՛ս վերևում), և տվյալների բազայի մատյանը՝

```bash
cd /opt/hostpanel && docker compose -f docker-compose.hub.yml logs hostpanel-db
```

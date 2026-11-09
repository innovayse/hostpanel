---
title: Գործարկեք Hostpanel-ը տեղային
description: Գործարկեք մշակման ամբողջ միջավայրը ձեր համակարգչում Docker Compose-ով։
---

# Գործարկեք Hostpanel-ը տեղային {#run-hostpanel-locally}

## Ինչ է պետք {#what-you-need}

- Կոնտեյների շարժիչ՝ Compose-ի աջակցությամբ
- Մոտ 8 ԳԲ ազատ հիշողություն
- Հոստի վրա ազատ `3001`, `5174` և `5149` պորտերը

## Կլոնավորեք պահոցը {#clone-the-repository}

Պլագինների SDK-ն git submodule է։ Այն մեկ քայլով ստանալու համար կլոնավորեք `--recurse-submodules`-ով.

```bash
git clone --recurse-submodules https://github.com/innovayse/hostpanel
```

Եթե արդեն կլոնավորել եք առանց դրա.

```bash
git submodule update --init
```

## Գործարկեք միջավայրը {#start-the-stack}

<Steps>
<li>

Անցեք docker թղթապանակ.

```bash
cd docker
```

</li>
<li>

Պատճենեք միջավայրի օրինակ ֆայլը.

```bash
cp .env.example .env
```

</li>
<li>

Գործարկեք բոլոր ծառայությունները.

```bash
docker compose -f docker-compose.shared.yml -f docker-compose.dev.yml up -d
```

Գործարկված ծառայությունները՝ `hostpanel-db`, `hostpanel-api`, `hostpanel-client`, `hostpanel-admin`, ինչպես նաև RabbitMQ, Redis և MailHog։

</li>
<li>

Հետևեք API-ի մատյանին՝ գործարկումը և միգրացիաները դիտելու համար.

```bash
./scripts/manage.sh logs api
```

Հավելվածները բացելուց առաջ սպասեք «Now listening on» գրառմանը։

</li>
</Steps>

## Բացեք այն {#open-it}

<SettingTable :settings="[
  { name: 'CLIENT_HOST_PORT', type: 'number', default: '3001', description: 'Հաճախորդի պորտալ՝ http://localhost:3001' },
  { name: 'ADMIN_HOST_PORT', type: 'number', default: '5174', description: 'Ադմինի վահանակ՝ http://localhost:5174' },
  { name: 'API_HOST_PORT', type: 'number', default: '5149', description: 'API և OpenAPI էջ՝ http://localhost:5149/swagger' },
  { name: 'MAILHOG_UI_HOST_PORT', type: 'number', default: '8027', description: 'Տեղային փոստարկղի ինտերֆեյս՝ http://localhost:8027' },
]" />

Մշակման սկզբնական տվյալները ստեղծում են հետևյալ հաշիվները.

| Հաշիվ | Էլ. փոստ | Գաղտնաբառի միջավայրի փոփոխական |
|-------|----------|-------------------------------|
| Ադմին | `admin@example.test` | `DEV_SEED_ADMIN_PASSWORD` (կանխադրված՝ `Admin123!`) |
| Հաճախորդ | `owner@example.test` և այլք | `DEV_SEED_CLIENT_PASSWORD` (կանխադրված՝ `Test@12345`) |

## Թեժ վերբեռնում {#hot-reload}

API կոնտեյները միացնում է սկզբնական կոդի ծառը և վերակառուցվում է ֆայլերի փոփոխության դեպքում։ Ֆայլերի պարբերական ստուգումը կանխադրված միացված է `.env.example`-ում՝ հոստի այն ֆայլային համակարգերի համար, որոնք անվստահելիորեն են ուղարկում փոփոխությունների իրադարձություններ։

<Callout type="warning">
Երբ մշակման միջավայրը գործարկված է, հոստի վրա երբեք մի կատարեք <code>dotnet build</code>, <code>dotnet restore</code>, <code>npm install</code> կամ նմանատիպ հրամաններ։ Կոնտեյները և հոստը կպայքարեն նույն ֆայլերի համար։
</Callout>

## Առանց կոնտեյների {#without-containers}

Եթե նախընտրում եք միջավայրը գործարկել անմիջապես, ձեզ պետք են .NET 9 SDK, Node.js 22+, yarn և PostgreSQL 17։

```bash
cd backend && dotnet run --project src/Innovayse.Hostpanel.API   # http://localhost:5148
cd client  && yarn install && yarn dev                            # http://localhost:3000
cd admin   && npm install && npm run dev                          # http://localhost:5173
```

API-ի միացման տողը, RabbitMQ-ի և SMTP-ի կարգավորումները սահմանեք միջավայրի փոփոխականներով, user secrets-ով կամ `appsettings.Development.json`-ով (git-ը անտեսում է այն)։

## Վերականգնեք տվյալները սկզբնական վիճակին {#reset-the-data}

Օգտագործեք կառավարման սկրիպտը `docker/` թղթապանակից.

```bash
./scripts/manage.sh db reset
```

Սա հաստատում է խնդրում, ջնջում է տվյալների բազան և կիրառում է բոլոր միգրացիաները։ Դրանից հետո մշակման տվյալներ ավելացնելու համար.

```bash
./scripts/manage.sh db seed
```

## Խնդիրների լուծում {#troubleshooting}

| Խնդիր | Լուծում |
|-------|---------|
| Պորտն արդեն զբաղված է | Փոխեք `API_HOST_PORT`, `CLIENT_HOST_PORT` կամ `ADMIN_HOST_PORT` արժեքը `.env`-ում |
| Ենթացանցի բախում մեկ այլ ցանցի հետ | Փոխեք `STACK_NETWORK_SUBNET` արժեքը `.env`-ում (կանխադրված՝ `172.30.0.0/24`) |

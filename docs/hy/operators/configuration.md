---
title: Կարգավորումների տեղեկատու
description: Hostpanel-ի կարդացած յուրաքանչյուր .env կարգավորում՝ լռելյայն արժեքներով, պարտադիրության նշաններով և compose մակարդակով, որին պատկանում է կարգավորումը։
---

# Կարգավորումների տեղեկատու {#configuration-reference}

## Ինչպես է աշխատում կարգավորումը {#how-configuration-works}

Կարգավորումները գտնվում են `docker/.env.example` ֆայլում, որը պատճենվում է `.env` անվամբ compose ֆայլի կողքին։ Compose ֆայլը կարդում է `.env`-ը և արժեքները ծառայություններին փոխանցում է որպես միջավայրի փոփոխականներ։ Որոշ կարգավորումներ ծառայություններին հասնում են այլ անունով, օրինակ՝ `STACK_NETWORK_SUBNET`-ը API-ին հասնում է որպես `RateLimit__KnownNetworks__0`։

Ոչ բոլոր կարգավորումներն են `.env.example`-ում։ Հարցումների սահմանափակման և պատվերների համարակալման մի քանի կարգավորում սահմանվում է անմիջապես API ծառայության վրա՝ compose override ֆայլում (ամբողջական ցանկը տե՛ս [Հակադարձ պրոքսի և վստահելի պրոքսիներ](#reverse-proxy-and-trusted-proxies) և [API և նավահանգիստներ](#api-and-ports) բաժիններում)։

## Compose ֆայլեր {#compose-files}

Յուրաքանչյուր compose ֆայլ նախատեսված է տեղակայման առանձին թիրախի համար։ Ստորև բերված կարգավորումների աղյուսակները ցույց են տալիս, թե որ մակարդակին է պատկանում յուրաքանչյուր կարգավորում։

### Ինչպես է արժեքը հասնում API-ին {#how-a-value-reaches-the-api}

Նույն կարգավորումը կարող է գտնվել տարբեր տեղերում՝ կախված նրանից, թե այն `.env.example`-ում է, թե սահմանված է compose override-ում։ Ահա օրինակ՝ վստահելի պրոքսիների և պատվերների կարգավորումներով.

::: code-group

```bash [.env]
# Most settings live here:
RATE_LIMIT_TRUSTED_PROXY_COUNT=1
STACK_NETWORK_SUBNET=172.30.0.0/24
```

```yaml [compose override]
# Settings not in .env are set directly on the service:
services:
  hostpanel-api:
    environment:
      Orders__NumberPrefix: "ORD-"
      Orders__NumberDigits: 4
```

```text [As the API sees it]
# .env variable                   → API configuration key
STACK_NETWORK_SUBNET              → RateLimit__KnownNetworks__0
RATE_LIMIT_TRUSTED_PROXY_COUNT    → RateLimit__TrustedProxyCount
Orders__NumberPrefix (override)   → Orders__NumberPrefix (no rename)
```

:::

| Ֆայլ | Նպատակ | Մակարդակ |
|---|---|---|
| `docker-compose.hub.yml` | Ինքնուրույն self-host, օգտագործվում է `install.sh`-ի կողմից | hub |
| `docker-compose.shared.yml` + `docker-compose.dev.yml` | Տեղական մշակում | dev |
| `docker-compose.prod.yml` | Սկզբնական կոդից կառուցված տեղակայում | prod |
| `docker-compose.demo.yml` | Դեմո համակարգ | demo |
| `docker-compose.platform.yml` | Գոյություն ունեցող ցանցին միանալը | n/a |

## Պարտադիր գաղտնիքներ {#required-secrets}

<Callout type="danger">
Յուրաքանչյուր <code>CHANGE_ME</code> տեղապահ է, ոչ թե իրական լռելյայն արժեք։ Եթե գաղտնիքը տեղապահ է պարունակում կամ չափազանց կարճ է, API-ն Development-ից դուրս չի գործարկվում։ Արտադրական միջավայրի համար միշտ նոր արժեքներ ստեղծեք։
</Callout>

<Callout type="tip">
Անվտանգ պատահական արժեքներ ստեղծեք <code>openssl</code>-ով՝
<ul>
<li><strong>JWT_SECRET</strong> (առնվազն 32 նիշ)՝ <code>openssl rand -hex 32</code></li>
<li><strong>ENCRYPTION_KEY</strong> (ուղիղ 32 բայթ, base64)՝ <code>openssl rand -base64 32</code></li>
<li><strong>SESSION_ENCRYPTION_KEY</strong> (ուղիղ 32 բայթ, base64)՝ <code>openssl rand -base64 32</code></li>
<li><strong>HOSTPANEL_BACKCHANNEL_LOGOUT_KEY</strong> (առնվազն 16 նիշ)՝ <code>openssl rand -hex 24</code></li>
<li><strong>POSTGRES_PASSWORD</strong>՝ <code>openssl rand -hex 24</code></li>
<li><strong>RABBITMQ_PASSWORD</strong>՝ <code>openssl rand -hex 24</code></li>
</ul>
</Callout>

## Ընդհանուր {#general}

<Callout type="warning">
<code>Development</code> ռեժիմում թողնված սերվերը գործարկում է DevDataSeeder-ը և յուրաքանչյուր վերագործարկման ժամանակ նորից լցնում թեստային տվյալները։ Տեղակայված հոսթում միշտ սահմանեք <code>ASPNETCORE_ENVIRONMENT=Production</code>։
</Callout>

<SettingTable :settings="[
  { name: 'ASPNETCORE_ENVIRONMENT', type: 'string', default: 'Development', description: 'Գործարկման ռեժիմը՝ Production կամ Development։ Development-ը նաև գործարկում է DevDataSeeder-ը, որը յուրաքանչյուր վերագործարկման ժամանակ նորից լցնում է թեստային տվյալները։ Սերվերը պետք է օգտագործի Production։ Պարտադիր է hub-ում և prod-ում։ Մակարդակներ՝ hub, prod, dev։' },
  { name: 'AUTH_APP_NAME', type: 'string', default: 'hostpanel', description: 'Նույնականացման շերտում գրանցված հավելվածի անունը (cookie-ների և հաճախորդի անվանում)։ Մակարդակներ՝ hub, prod, dev։' },
  { name: 'DISABLE_RELEASE_UPDATE_CHECKS', type: 'flag', default: 'false', description: 'Սահմանեք true՝ GitHub-ում նոր թողարկման օրական ստուգումն անջատելու համար։ Անջատեք, երբ տեղադրումը դուրս գնացող ինտերնետ հասանելիություն չունի։ Համապատասխանում է ScheduledJobs__DisableReleaseUpdateChecks-ին։ Մակարդակներ՝ hub, prod։' },
  { name: 'DISABLE_EXCHANGE_RATE_UPDATES', type: 'flag', default: 'false', description: 'Սահմանեք true՝ արժույթների փոխարժեքների օրական թարմացումն (03:00 UTC) անջատելու համար։ Անջատեք, երբ տեղադրումը դուրս գնացող ինտերնետ հասանելիություն չունի․ այդ դեպքում փոխարժեքները փոխվում են միայն ձեռքով կամ «Թարմացնել փոխարժեքները» կոճակով։ Համապատասխանում է ScheduledJobs__DisableExchangeRateUpdates-ին։ Մակարդակներ՝ hub, prod։' },
]" />

### Ժամային գոտի {#time-zone}

Օրացուցային օրերը կտրվում են API պրոցեսի ժամային գոտով. վահանակի գրաֆիկները և «Օրական արդյունավետություն» հաշվետվությունը յուրաքանչյուր օրը սկսում և ավարտում են այդ գոտու կեսգիշերին, իսկ «այսօրը» այն օրն է, որը ցույց է տալիս այդ գոտու ժամացույցը։ Տվյալների բազայում պահվող պահերը, UTC-ին կապված պլանային աշխատանքները (օրինակ՝ փոխարժեքների թարմացումը 03:00 UTC-ին) և մյուս հաշվետվությունները չեն ազդվում։ Գոտին `hostpanel-api` կոնտեյների համակարգային ժամային գոտին է. UTC, քանի դեռ սերվիսի վրա չեք սահմանել ստանդարտ `TZ` փոփոխականը IANA անունով, օրինակ՝ compose override-ում։ Պատկերը պարունակում է ժամային գոտիների տվյալների բազան, ուստի ցանկացած IANA անուն հարմար է։

```yaml [compose override]
services:
  hostpanel-api:
    environment:
      TZ: "Asia/Yerevan"
```

## Մուտքի ռեժիմներ {#sign-in-modes}

Hostpanel-ը աջակցում է նույնականացման երկու ռեժիմ՝ local (օգտատերերին կառավարում է հենց Hostpanel-ը) և SSO (օգտատերերը մուտք են գործում արտաքին OpenID Connect մատակարարի միջոցով)։

<SettingTable :settings="[
  { name: 'AUTH_MODE', type: 'string', default: 'local', description: 'local՝ Hostpanel-ը նույնականացնում է իր օգտատերերին։ sso՝ մուտքն ուղղվում է արտաքին OpenID Connect մատակարարին։ Պարտադիր է։ Մակարդակներ՝ բոլորը։' },
]" />

### SSO-ի կարգավորումներ {#sso-settings}

<Label type="sso" /> Այս կարգավորումները պարտադիր են միայն AUTH_MODE=sso դեպքում։

<SettingTable :settings="[
  { name: 'SSO_URL', type: 'string', default: 'https://sso.example.com', description: 'Սերվերից սերվեր հասցե, որով API-ն դիմում է մատակարարին։ SSO-ի համար պարտադիր է։ Մակարդակներ՝ բոլորը։' },
  { name: 'SSO_PUBLIC_URL', type: 'string', default: 'https://sso.example.com', description: 'Հասցեն, ուր բրաուզերն ուղարկվում է մուտք գործելու համար (մատակարարի հանրային հոսթ-անունը, issuer)։ SSO-ի համար պարտադիր է։ Մակարդակներ՝ բոլորը։' },
  { name: 'SSO_CLIENT_ID', type: 'string', default: 'hostpanel', description: 'Մատակարարի մոտ գրանցված հաճախորդի նույնացուցիչը։ SSO-ի համար պարտադիր է։ Մակարդակներ՝ բոլորը։' },
  { name: 'SSO_ISSUER', type: 'string', default: 'https://sso.example.com/', description: 'Issuer-ը, որը մատակարարը դրոշմում է իր թոքենների մեջ՝ ճիշտ այնպես, ինչպես թվարկված է իր openid-configuration-ում (վերջի շեղ գծով հանդերձ)։ Back-channel logout-ը յուրաքանչյուր logout թոքեն ստուգում է դրա դեմ։ AUTH_MODE=sso դեպքում առանց դրա API-ն չի գործարկվում։ SSO-ի համար պարտադիր է։ Մակարդակներ՝ բոլորը։' },
  { name: 'SSO_REQUIRE_HTTPS_METADATA', type: 'flag', default: 'true', description: 'Պահանջո՞ւմ է արդյոք բանալիների ստացումը, որով ստուգվում են back-channel logout թոքենները, որ SSO_URL-ը լինի https։ Սահմանեք false միայն այն դեպքում, երբ SSO_URL-ը մասնավոր ցանցի պարզ http հասցե է։ Մակարդակներ՝ բոլորը։' },
  { name: 'SSO_CALLBACK_URL', type: 'string', default: 'https://panel.example.com/api/portal/auth/sso/callback', description: 'URL, ուր մատակարարը վերաուղղորդում է մուտքից հետո։ Ադմինի callback-ը <admin origin>/api/auth/callback է։ SSO-ի համար պարտադիր է։ Մակարդակներ՝ բոլորը։' },
  { name: 'SSO_SERVICE_KEY', type: 'secret', default: 'CHANGE_ME', description: 'Մեքենայական հավատարմագիր օգտատերերի ցուցակը կարդալու համար։ Ստեղծեք openssl rand -base64 24-ով։ SSO-ի համար պարտադիր է։ Մակարդակներ՝ բոլորը։' },
  { name: 'CLIENT_HOSTPANEL_SECRET', type: 'secret', default: 'CHANGE_ME', description: 'Գաղտնիք, որով API-ն սերվերից սերվեր փոխանակում է ադմինի SPA-ի authorization code-ը։ AUTH_MODE=sso դեպքում առանց դրա API-ն չի գործարկվում։ SSO-ի համար պարտադիր է։ Մակարդակներ՝ բոլորը։' },
  { name: 'HOSTPANEL_BACKCHANNEL_LOGOUT_KEY', type: 'secret', default: 'CHANGE_ME', description: 'Back-channel logout-ի համար կիսվում է մատակարարի հետ։ Ստեղծեք openssl rand -hex 24-ով։ Տեղակայված հոսթում պետք է լինի առնվազն 16 նիշ։ Դատարկ թողեք միայն այնտեղ, որտեղ մատակարար միացված չէ։ SSO-ի համար պարտադիր է։ Մակարդակներ՝ բոլորը։' },
]" />

Թողարկումը, որը sign-on հաճախորդի փաթեթը տեղափոխում է 5.1 տարբերակի, մեկ անգամ զրոյացնում է ադմինի վահանակի սեսիաները. փաթեթն այժմ իր սեսիայի քեշի բանալիները պահում է ըստ ապրանքի, ուստի հին բանալիներով պահված սեսիաներն այլևս չեն կարդացվում։ Թարմացումից հետո յուրաքանչյուր ադմինիստրատոր նորից մուտք է գործում, ուրիշ ոչինչ անել պետք չէ, իսկ հին գրառումները սպառվում են ինքնուրույն։ Փաթեթը նաև ինքն է կապում back-channel logout ստացողը /api/auth/backchannel-logout հասցեով, և մուտքի callback-ը, որն համապատասխան փորձ չունի (երկրորդ ներդիր, «Հետ» սեղմելը մուտքից հետո), այժմ վերաուղղորդում է մուտքի էջ՝ սխալի մարմին վերադարձնելու փոխարեն։ Երկուսի համար էլ ոչինչ կարգավորել պետք չէ։

### Տեղական սեսիայի կարգավորումներ {#local-session-settings}

<Label type="local" /> Այս կարգավորումները կիրառվում են միայն AUTH_MODE=local դեպքում։

<SettingTable :settings="[
  { name: 'SESSION_ACCESS_TOKEN_MINUTES', type: 'string', default: '15', description: 'Access թոքենի կյանքի տևողությունը րոպեներով։ Սահող երկարացումը նոր թոքեն է տալիս, քանի դեռ մարդը շարունակում է աշխատել։ Մակարդակներ՝ hub, prod, dev։' },
  { name: 'SESSION_MAX_MINUTES', type: 'string', default: '720', description: 'Սեսիայի բացարձակ տարիքը, որից հետո թոքենի երկարացումը դադարում է, և մարդը պետք է նորից մուտք գործի։ Մակարդակներ՝ hub, prod, dev։' },
]" />

## Հասցեներ և հոսթ-անուններ {#addresses-and-host-names}

<SettingTable :settings="[
  { name: 'DOMAIN', type: 'string', default: 'yourdomain.com', description: 'Հաճախորդի պորտալի հանրային հոսթ-անունը (օրինակ՝ panel.example.com)։ Պետք է տարբերվի ADMIN_DOMAIN-ից։ Պարտադիր է։ Մակարդակներ՝ բոլորը։' },
  { name: 'ADMIN_DOMAIN', type: 'string', default: 'admin.yourdomain.com', description: 'Ադմինի վահանակի հանրային հոսթ-անունը։ Պետք է տարբերվի DOMAIN-ից։ Պարտադիր է։ Մակարդակներ՝ բոլորը։' },
  { name: 'ADMIN_BASE_URL', type: 'string', default: 'https://admin.yourdomain.com', description: 'Ադմինի վահանակի բացարձակ URL-ը (սխեմայով, առանց վերջի շեղ գծի)։ API-ի՝ աշխատակիցներին փոստով ուղարկած հղումների հիմքը։ Պարտադիր է prod-ում և hub-ում։ Մակարդակներ՝ prod, hub։' },
  { name: 'CLIENT_BASE_URL', type: 'string', default: 'https://yourdomain.com', description: 'Հաճախորդի պորտալի բացարձակ URL-ը (սխեմայով, առանց վերջի շեղ գծի)։ API-ի՝ հաճախորդներին փոստով ուղարկած հղումների հիմքը։ Պարտադիր է prod-ում և hub-ում։ Մակարդակներ՝ prod, hub։' },
  { name: 'NUXT_PUBLIC_BASE_URL', type: 'string', default: 'https://panel.example.com', description: 'Հանրային URL, որն օգտագործում է հաճախորդի պորտալի JavaScript-ը API կանչերի համար։ Պարտադիր է։ Մակարդակներ՝ բոլորը։' },
  { name: 'ADMIN_BASE_URL_PATH', type: 'string', default: '/', description: 'Բազային ուղի, որով սպասարկվում է ադմինի SPA-ն (/ կամ /admin)։ Կարդացվում է ադմինի կոնտեյների գործարկման պահին, ոչ թե build-ի ժամանակ։ Մակարդակներ՝ hub, prod։' },
  { name: 'ALLOWED_HOSTS', type: 'string', default: 'panel.example.test;hostpanel-api;localhost', description: 'Հոսթ-անուններ, որոնց API-ն պատասխանում է, բաժանված կետ-ստորակետով։ Ներառեք հանրային հոսթը, hostpanel-api-ն և localhost-ը։ Պարտադիր է։ Մակարդակներ՝ բոլորը։' },
  { name: 'PANEL_DEV_ORIGIN', type: 'string', default: 'http://panel.example.test', description: 'Dev-պրոքսիի origin-ը, որը API-ն թույլատրում է localhost-ի կողքին (CORS)։ Մակարդակներ՝ dev։' },
  { name: 'SSH_AGENT_SOURCE_ADDRESSES', type: 'string', default: '', description: 'Հանրային հասցե(ներ), որտեղից այս տեղադրումը միանում է SSH բանալիների գործակալներին, բաժանված ստորակետով։ Նոր է 0.4.0-ում։ Մակարդակներ՝ prod, hub։' },
  { name: 'PANEL_HOST_ADDRESSES', type: 'string', default: '', description: 'Կառավարման վահանակներ, որոնք հասանելի են միայն VPN-ով, hostname=address զույգերով՝ բաժանված ստորակետով։ Նոր է 0.4.0-ում։ Մակարդակներ՝ prod, hub։' },
]" />

SSH_AGENT_SOURCE_ADDRESSES-ը սահմանելուց հետո Servers → SSH էջը ցույց է տալիս firewall-ի ճշգրիտ կանոնները։ Տե՛ս [SSH բանալիների գործակալ](/hy/servers/ssh-key-agent)։

## Բանալիներ և գաղտնագրում {#keys-and-encryption}

<Callout type="danger">
ENCRYPTION_KEY-ը պաշտպանում է սերվերների գաղտնաբառերը, API թոքենները և հասանելիության հեշերը։ Աշխատող տեղադրման վրա երբեք մի փոխեք այն՝ առանց կրկնագաղտնագրման պլանի։ .env-ի պահուստային պատճենը պահեք տվյալների բազայի յուրաքանչյուր դամփի հետ։
</Callout>

<SettingTable :settings="[
  { name: 'ENCRYPTION_KEY', type: 'secret', default: 'see .env.example', description: 'Պետք է վերծանվի ուղիղ 32 բայթի։ Պաշտպանում է տվյալների բազայի գաղտնագրված սյունակները։ Ստեղծեք՝ openssl rand -base64 32։ Օրինակի արժեքը տեղապահ է և Development-ից դուրս մերժվում է։ Պարտադիր է prod-ում, hub-ում, demo-ում։ Մակարդակներ՝ prod, hub, demo։' },
  { name: 'SESSION_ENCRYPTION_KEY', type: 'secret', default: '', description: 'Հաճախորդի պորտալի սեսիաների բանալին (AES-256-GCM)։ 32 պատահական բայթ, base64։ Փոխելը բոլորին դուրս է բերում համակարգից։ Պարտադիր է prod-ում, hub-ում, demo-ում։ Մակարդակներ՝ prod, hub, demo։' },
  { name: 'JWT_SECRET', type: 'secret', default: 'see .env.example', description: 'Առնվազն 32 նիշ։ Տեղապահը Development-ից դուրս մերժվում է։ Ստեղծեք՝ openssl rand -hex 32։ Մակարդակներ՝ բոլորը։' },
  { name: 'JWT_ISSUER', type: 'string', default: 'innovayse-api', description: 'Issuer claim-ը, որը դրոշմվում է յուրաքանչյուր թոքենի մեջ։ Մակարդակներ՝ բոլորը։' },
  { name: 'JWT_AUDIENCE', type: 'string', default: 'innovayse-clients', description: 'Audience claim-ը, որը ստուգվում է յուրաքանչյուր թոքենի վրա։ Մակարդակներ՝ բոլորը։' },
]" />

## Տվյալների բազա, հաղորդագրությունների հերթ և քեշ {#database-message-queue-and-cache}

<SettingTable :settings="[
  { name: 'POSTGRES_HOST', type: 'string', default: 'hostpanel-db', description: 'Տվյալների բազայի հոսթ-անունը։ Մակարդակներ՝ բոլորը։' },
  { name: 'POSTGRES_PORT', type: 'string', default: '5432', description: 'Տվյալների բազայի նավահանգիստը։ Մակարդակներ՝ բոլորը։' },
  { name: 'POSTGRES_USER', type: 'string', default: 'postgres', description: 'Տվյալների բազայի օգտատերը։ Մակարդակներ՝ բոլորը։' },
  { name: 'POSTGRES_PASSWORD', type: 'secret', default: 'CHANGE_ME', description: 'Տվյալների բազայի գաղտնաբառը։ Տեղապահը Development-ից դուրս մերժվում է։ Պարտադիր է։ Մակարդակներ՝ բոլորը։' },
  { name: 'POSTGRES_DB', type: 'string', default: 'innovayse_dev', description: 'Տվյալների բազայի անունը։ Մակարդակներ՝ բոլորը։' },
  { name: 'RABBITMQ_HOST', type: 'string', default: 'hostpanel-rabbitmq', description: 'Հաղորդագրությունների հերթի հոսթ-անունը։ Մակարդակներ՝ բոլորը։' },
  { name: 'RABBITMQ_PORT', type: 'string', default: '5672', description: 'Հաղորդագրությունների հերթի նավահանգիստը։ Մակարդակներ՝ բոլորը։' },
  { name: 'RABBITMQ_USER', type: 'string', default: 'guest', description: 'Հաղորդագրությունների հերթի օգտատերը։ Մակարդակներ՝ բոլորը։' },
  { name: 'RABBITMQ_PASSWORD', type: 'secret', default: 'CHANGE_ME', description: 'Հաղորդագրությունների հերթի գաղտնաբառը։ Տեղապահը Development-ից դուրս մերժվում է։ Պարտադիր է։ Մակարդակներ՝ բոլորը։' },
  { name: 'REDIS_CONNECTION', type: 'string', default: 'hostpanel-redis:6379', description: 'Redis-ի միացման տողը։ Օգտագործվում է ադմինի սեսիաների (SSO), տվյալների պաշտպանության բանալիների օղակի և հաճախորդի պորտալի մուտքի սեսիաների համար։ Մակարդակներ՝ բոլորը։' },
]" />

## Փոստ {#mail}

<Callout type="warning">
SMTP_ENCRYPTION-ի բացակայությունը կամ սխալ գրված լինելը գործարկման պահին կանգնեցնում է API-ն և մատյանում նշում այս բանալին։
</Callout>

<SettingTable :settings="[
  { name: 'SMTP_HOST', type: 'string', default: 'hostpanel-mailhog', description: 'SMTP ռելեի հոսթը։ Տեղադրիչը սա դատարկ է թողնում. մինչև սահմանելը փոստ չի ուղարկվում։ Պարտադիր է prod-ում և hub-ում։ Մակարդակներ՝ բոլորը։' },
  { name: 'SMTP_PORT', type: 'string', default: '1025', description: 'SMTP նավահանգիստը։ Սովորաբար 587 (STARTTLS) կամ 465 (implicit TLS)։ Մակարդակներ՝ բոլորը։' },
  { name: 'SMTP_USERNAME', type: 'string', default: '', description: 'Ռելեի օգտանունը։ Մակարդակներ՝ բոլորը։' },
  { name: 'SMTP_PASSWORD', type: 'secret', default: '', description: 'Ռելեի գաղտնաբառը։ Մակարդակներ՝ բոլորը։' },
  { name: 'SMTP_ENCRYPTION', type: 'string', default: 'none', description: 'tls (STARTTLS), ssl (implicit TLS) կամ none (միայն տեղական նամակ ընդունիչի համար)։ Պարտադիր է և ստուգվում է գործարկման ժամանակ։ Մակարդակներ՝ բոլորը։' },
  { name: 'SMTP_FROM', type: 'string', default: 'noreply@example.com', description: 'Ուղարկողի հասցեն։ Մակարդակներ՝ բոլորը։' },
  { name: 'SMTP_FROM_NAME', type: 'string', default: 'Hostpanel', description: 'Ուղարկողի ցուցադրվող անունը։ Մակարդակներ՝ բոլորը։' },
  { name: 'EMAIL_TO', type: 'string', default: 'contact@yourdomain.com', description: 'Կապի ձևի ստացողի հասցեն։ Եթե սահմանված չէ, ձևը պատասխանում է 503 CONTACT_NOT_CONFIGURED։ Մակարդակներ՝ բոլորը։' },
  { name: 'DEPARTMENT_SUPPORT_EMAIL', type: 'string', default: 'support@example.com', description: 'Աջակցության բաժնի էլ. փոստը առաջին տեղադրման ժամանակ։ Հետո խմբագրվում է ադմինի վահանակում։ Մակարդակներ՝ բոլորը։' },
  { name: 'DEPARTMENT_BILLING_EMAIL', type: 'string', default: 'billing@example.com', description: 'Վճարումների բաժնի էլ. փոստը առաջին տեղադրման ժամանակ։ Մակարդակներ՝ բոլորը։' },
  { name: 'DEPARTMENT_SALES_EMAIL', type: 'string', default: 'sales@example.com', description: 'Վաճառքի բաժնի էլ. փոստը առաջին տեղադրման ժամանակ։ Մակարդակներ՝ բոլորը։' },
  { name: 'DEPARTMENT_GENERAL_EMAIL', type: 'string', default: 'hello@example.com', description: 'Ընդհանուր բաժնի էլ. փոստը առաջին տեղադրման ժամանակ։ Մակարդակներ՝ բոլորը։' },
  { name: 'TELEGRAM_BOT_TOKEN', type: 'secret', default: '', description: 'Telegram բոտի թոքենը կապի ձևի ծանուցումների համար։ Կամ երկուսը, կամ ոչ մեկը. զույգի կեսը մերժում է գործարկումը։ Մակարդակներ՝ բոլորը։' },
  { name: 'TELEGRAM_CHAT_ID', type: 'string', default: '', description: 'Telegram չատի ID-ն, կամընտիր զույգ։ Սահմանեք երկուսը կամ ոչ մեկը։ Մակարդակներ՝ բոլորը։' },
]" />

## Հակադարձ պրոքսի և վստահելի պրոքսիներ {#reverse-proxy-and-trusted-proxies}

<Callout type="warning">
Պրոքսիների քանակի սխալ արժեքը խանգարում է հարցումների սահմանափակմանը և պատվերների IP-ի հետևմանը։ Շատ փոքր արժեքի դեպքում բոլոր այցելուները կիսում են հարցումների սահմանափակման մեկ բաժին, և պատվերները սխալ IP են գրանցում։ Շատ մեծ արժեքի դեպքում այցելուն կարող է կեղծել իր հասցեն։
</Callout>

Երբ տեղադրումը գտնվում է մեկ կամ մի քանի պրոքսիի հետևում, API-ն և հաճախորդի պորտալը պետք է վստահեն իրական այցելուի հասցեի մասին վերնագրերին։ Սխեման ցույց է տալիս ճանապարհը՝

```
visitor → [extra proxy] → edge (Caddy/nginx) → client portal → API
                                                    ↑
                                         RATE_LIMIT_TRUSTED_PROXY_COUNT
                                         CLIENT_TRUSTED_PROXY_COUNT
```

Ներկառուցված եզրային պրոքսիի առջև դրված յուրաքանչյուր լրացուցիչ պրոքսի երկու քանակներին էլ ավելացնում է 1։ Մատակարարվող համակարգերն ունեն մեկ ներկառուցված եզրային շերտ, ուստի լռելյայն արժեքը `1` է։ Եթե առջևում լրացուցիչ պրոքսի եք դնում (բեռի հավասարակշռիչ, WAF, CDN), յուրաքանչյուր լրացուցիչ շերտի համար երկու քանակն էլ մեծացրեք 1-ով։

<SettingTable :settings="[
  { name: 'STACK_NETWORK_SUBNET', type: 'string', default: '172.30.0.0/24', description: 'Docker ցանցի CIDR տիրույթը, որը ստեղծում է համակարգը։ API-ին փոխանցվում է որպես RateLimit__KnownNetworks__0։ Ընտրեք տիրույթ, որը հոսթի ոչ մի այլ ցանց չի օգտագործում։ Պարտադիր է։ Մակարդակներ՝ hub, prod, dev։' },
  { name: 'RATE_LIMIT_TRUSTED_PROXY_COUNT', type: 'string', default: '1', description: 'API-ի առջևի պրոքսիների քանակը (RateLimit__TrustedProxyCount)։ Մատակարարվող համակարգերին պետք է 1 (ներկառուցված եզրային պրոքսին)։ Ավելացրեք մեկական՝ յուրաքանչյուր լրացուցիչ պրոքսիի համար։ Մակարդակներ՝ hub, prod, dev։' },
  { name: 'CLIENT_TRUSTED_PROXY_COUNT', type: 'string', default: '1', description: 'Հաճախորդի պորտալի առջևի պրոքսիների քանակը (NUXT_TRUSTED_PROXY_COUNT)։ Մատակարարվող համակարգերին պետք է 1։ Ավելացրեք մեկական՝ յուրաքանչյուր լրացուցիչ պրոքսիի համար։ Dev համակարգը սա ֆիքսում է 0-ի վրա և այս փոփոխականը չի կարդում։ Մակարդակներ՝ hub, prod, demo։' },
]" />

### Հարցումների սահմանափակման կարգավորումներ (միայն compose override-ում) {#rate-limit-settings-compose-override-only}

<Callout type="note">
Հետևյալ կարգավորումները սահմանվում են անմիջապես API ծառայության վրա՝ compose override ֆայլում, ոչ թե <code>.env.example</code>-ում։ Օգտագործեք դրանք յուրաքանչյուր endpoint-ի համար հարցումների սահմանափակումը կարգավորելու համար։
</Callout>

| Կարգավորում | Լռելյայն | Նկարագրություն |
|---|---|---|
| `RateLimit__Global__PermitLimit` | `1200` | Պատուհանում թույլատրված հարցումներ (կիրառվում է յուրաքանչյուր հարցման վրա) |
| `RateLimit__Global__WindowSeconds` | `60` | Պատուհանի երկարությունը վայրկյաններով |
| `RateLimit__Auth__PermitLimit` | `15` | Նույնականացման endpoint-ների սահմանաչափը պատուհանում |
| `RateLimit__Auth__WindowSeconds` | `60` | Նույնականացման պատուհանի երկարությունը վայրկյաններով |
| `RateLimit__Upstream__PermitLimit` | `60` | Upstream-ի (վահանակի API) սահմանաչափը պատուհանում |
| `RateLimit__Upstream__WindowSeconds` | `60` | Upstream պատուհանի երկարությունը վայրկյաններով |
| `RateLimit__Strict__PermitLimit` | `5` | Խիստ endpoint-ների սահմանաչափը պատուհանում |
| `RateLimit__Strict__WindowSeconds` | `60` | Խիստ պատուհանի երկարությունը վայրկյաններով |
| `RateLimit__SshKeys__PermitLimit` | `20` | SSH բանալիների endpoint-ների սահմանաչափը պատուհանում |
| `RateLimit__SshKeys__WindowSeconds` | `60` | SSH բանալիների պատուհանի երկարությունը վայրկյաններով |
| `RateLimit__ConcurrentPermitLimit` | `5` | Մեկ հասցեից միաժամանակյա հարցումների առավելագույն քանակը |
| `RateLimit__PluginDefault__PermitLimit` | `120` | Պլագինի երթուղու սահմանաչափը պատուհանում այն երթուղու համար, որը մակարդակ չի նշում։ Հաշվվում է ըստ պլագինի և ըստ կանչողի (հաճախորդի հաշիվ, կամ ադմինիստրատոր՝ ադմինի կանչի դեպքում) |
| `RateLimit__PluginDefault__WindowSeconds` | `60` | Պլագինի լռելյայն պատուհանի երկարությունը վայրկյաններով |
| `RateLimit__PluginStrict__PermitLimit` | `20` | Սահմանաչափը պատուհանում այն պլագինի երթուղու համար, որը պահանջում է խիստ մակարդակ (թանկ կամ զգայուն երթուղիներ, օրինակ՝ հաղորդագրություններ ուղարկողը) |
| `RateLimit__PluginStrict__WindowSeconds` | `60` | Պլագինի խիստ պատուհանի երկարությունը վայրկյաններով |
| `RateLimit__PluginHeavy__PermitLimit` | `6` | Սահմանաչափը պատուհանում այն պլագինի երթուղու համար, որը պահանջում է ծանր մակարդակ (հաշվետվություններ, արտահանումներ) |
| `RateLimit__PluginHeavy__WindowSeconds` | `60` | Պլագինի ծանր պատուհանի երկարությունը վայրկյաններով |
| `RateLimit__PluginCallback__PermitLimit` | `120` | Սահմանաչափը պատուհանում պլագինի հանրային callback դռան համար (`/api/plugins/{id}/callback/...`)։ Հաշվվում է ըստ կանչողի հասցեի՝ բոլոր պլագինների և ուղիների վրա, հայտարարված թե չհայտարարված |
| `RateLimit__PluginCallback__WindowSeconds` | `60` | Պլագինի callback պատուհանի երկարությունը վայրկյաններով |
| `RateLimit__PluginStreamPermitLimit` | `3` | Պլագինի հոսքային երթուղիներ, որոնք մեկ կանչողը կարող է միաժամանակ բաց պահել՝ մեկ պլագինի համար |
| `SignInLockout__MaxFailedAttempts` | `5` | Սխալ գաղտնաբառեր մինչև արգելափակումը |
| `SignInLockout__LockoutMinutes` | `15` | Րոպեներ, որոնց ընթացքում հաշիվը մնում է արգելափակված (արգելափակված մուտք → 423) |

MaxFailedAttempts-ը ընդունում է 1-100, LockoutMinutes-ը՝ 1-1440։ Այս տիրույթներից դուրս արժեքները մերժում են գործարկումը։

Պլագինի երթուղիները սահմանափակվում են ըստ պլագինի և ըստ կանչողի, որպեսզի մեկ պլագին կամ մեկ հաշիվ չսպառի API-ն։ Պլագինի երթուղին իր մակարդակը հայտարարում է պլագինի մանիֆեստում (`rateLimit`՝ `default`, `strict` կամ `heavy`). մակարդակ չհայտարարող երթուղին օգտագործում է լռելյայնը։ Մերժված հարցմանը պատասխանվում է 429-ով՝ `Retry-After` և `X-RateLimit-Policy`, `X-RateLimit-Limit`, `X-RateLimit-Remaining` ու `X-RateLimit-Reset` վերնագրերով։ Պլագինի յուրաքանչյուր մակարդակ պետք է դրական լինի և չգերազանցի `RateLimit__Global__PermitLimit`-ը, մակարդակները պետք է դասավորված լինեն heavy <= strict <= default, իսկ `RateLimit__PluginStreamPermitLimit`-ը պետք է դրական լինի, այլապես գործարկումը մերժվում է։

Պլագինի հանրային callback դուռը (`/api/plugins/{id}/callback/...`) սահմանափակվում է ըստ կանչողի հասցեի, և այդ հասցեն այն է, որին հանգեցնում են փոխանցված վերնագրերը։ Այն աշխատում է ըստ նպատակի միայն այն դեպքում, երբ փոխանցված վերնագրերը կարգավորված են՝ `RateLimit__TrustedProxyCount`-ը սահմանեք API-ի առջևի պրոքսիների քանակին և թվարկեք դրանք `RateLimit__KnownProxies`-ում կամ `RateLimit__KnownNetworks`-ում (ներկառուցված համակարգերը դա անում են `STACK_NETWORK_SUBNET`-ով)։ Չկարգավորված պրոքսիի հետևում դարպասի բոլոր կանչերը կիսում են պրոքսիի հասցեն, և մեկ ալիքը մերժում է բոլոր callback-ները. չափից շատ վստահելի հանգույցների դեպքում ուղարկողը կարող է ինքը գրել իր `X-Forwarded-For`-ը և ընտրել իր սեփական զամբյուղը։ API-ն անտեսում է `X-Forwarded-For`-ը ցանկացած հանգույցից, որը կարգավորված պրոքսի չէ։

## Եզրային պրոքսի {#edge-proxy}

Եզրային շերտը (Caddy՝ hub-ում, nginx՝ prod-ում) ավարտում է TLS-ը և երթուղավորում դեպի API և հաճախորդի պորտալ։

<SettingTable :settings="[
  { name: 'EDGE_SCHEME', type: 'string', default: 'https://', description: 'https://՝ Let\'s Encrypt-ի ավտոմատ վկայականների համար (լռելյայն). http://՝ երբ TLS-ն ավարտում է ձեր սեփական պրոքսին (այդ դեպքում ընտրեք ազատ նավահանգիստներ)։ Մակարդակներ՝ hub, prod։' },
  { name: 'EDGE_HTTP_PORT', type: 'string', default: '80', description: 'HTTP նավահանգիստը, որը լսում է եզրային պրոքսին։ Մակարդակներ՝ hub, prod։' },
  { name: 'EDGE_HTTPS_PORT', type: 'string', default: '443', description: 'HTTPS նավահանգիստը, որը լսում է եզրային պրոքսին։ Մակարդակներ՝ hub, prod։' },
  { name: 'ACME_EMAIL', type: 'string', default: 'admin@yourdomain.com', description: 'Կապի հասցե Let\'s Encrypt վկայականների համար։ Սահմանվում է տեղադրիչով։ Պարտադիր է hub-ում։ Մակարդակներ՝ hub։' },
  { name: 'EDGE_MAX_BODY_SIZE', type: 'string', default: '50M', description: 'Հարցման ամենամեծ մարմինը հաճախորդի պորտալի հոսթում։ Օգտագործեք M վերջածանցը (օրինակ՝ 50M)։ Մակարդակներ՝ hub, prod։' },
  { name: 'EDGE_ADMIN_MAX_BODY_SIZE', type: 'string', default: '1M', description: 'Հարցման ամենամեծ մարմինը ադմինի հոսթում։ Մակարդակներ՝ hub, prod։' },
  { name: 'EDGE_CLIENT_CSP_SOURCES', type: 'string', default: '', description: 'Լրացուցիչ origin-ներ, որոնց թույլ է տալիս հաճախորդի պորտալի CSP-ն (բացատով բաժանված)։ Ավելացրեք սկրիպտի և API origin-ները, որոնք պետք են վճարային պլագինի checkout-ին. լռելյայն քաղաքականությունը ոչ մի վճարային համակարգ չի անվանում։ Մակարդակներ՝ hub, prod։' },
  { name: 'NGINX_HTTP_PORT', type: 'string', default: '80', description: 'Ներկառուցված nginx-ի հոսթի HTTP նավահանգիստը (prod համակարգ, առանց platform overlay-ի)։ Մակարդակներ՝ prod։' },
  { name: 'NGINX_HTTPS_PORT', type: 'string', default: '443', description: 'Ներկառուցված nginx-ի հոսթի HTTPS նավահանգիստը։ Մակարդակներ՝ prod։' },
]" />

Նամակագրերի նամակներն իրենց բացման պիքսելի, հետևվող հղումների և ապաբաժանորդագրման հղման հասցեում կրում են անձնական թոքեն։ Ներկառուցված nginx-ը գրում է մուտքի մատյանը այդ թոքենը `***`-ով փոխարինած։ Ներկառուցված Caddy-ն մուտքի մատյան չի գրում, քանի դեռ չեք միացրել, իսկ վահանակի առջևի սեփական պրոքսին պետք է իր մատյանում ինքը թաքցնի `/api/public/campaign-mail/` ուղիները և `/unsubscribe`-ի ու `/api/public/unsubscribe/`-ի `token` հարցման պարամետրը։

## Պատկերներ և տարբերակներ {#images-and-versions}

<SettingTable :settings="[
  { name: 'IMAGE_TAG', type: 'string', default: 'local', description: 'Գործարկվող թողարկումը, օրինակ՝ 0.4.0 կամ latest։ Առանց առաջատար v-ի։ Պարտադիր է hub-ի և prod-ի համար։ Մակարդակներ՝ hub, prod։' },
  { name: 'DOCKERHUB_NAMESPACE', type: 'string', default: 'innovayse', description: 'Docker Hub-ի օգտատեր/կազմակերպություն hub համակարգի պատկերների համար։ Մակարդակներ՝ hub։' },
  { name: 'IMAGE_REGISTRY', type: 'string', default: 'registry.example.com/hostpanel', description: 'Registry-ի ուղին prod համակարգի պատկերների համար (օրինակ՝ registry.example.com/hostpanel)։ Պարտադիր է prod-ի համար։ Մակարդակներ՝ prod։' },
  { name: 'POSTGRES_IMAGE', type: 'string', default: 'see .env.example', description: 'postgres պատկերի լրիվ հղումը՝ ամրացված digest-ով։ Մակարդակներ՝ բոլորը։' },
  { name: 'RABBITMQ_IMAGE', type: 'string', default: 'see .env.example', description: 'RabbitMQ պատկերի լրիվ հղումը՝ ամրացված digest-ով։ Մակարդակներ՝ բոլորը։' },
  { name: 'REDIS_IMAGE', type: 'string', default: 'see .env.example', description: 'Redis պատկերի լրիվ հղումը՝ ամրացված digest-ով։ Մակարդակներ՝ բոլորը։' },
  { name: 'MAILHOG_IMAGE', type: 'string', default: 'see .env.example', description: 'Mailhog պատկերի լրիվ հղումը (dev և demo համակարգեր)։ Մակարդակներ՝ dev, demo։' },
  { name: 'NGINX_IMAGE', type: 'string', default: 'see .env.example', description: 'nginx պատկերի լրիվ հղումը (պետք է լինի unprivileged տարբերակը)։ Մակարդակներ՝ prod։' },
  { name: 'CADDY_IMAGE', type: 'string', default: 'see .env.example', description: 'Caddy պատկերի լրիվ հղումը։ Մակարդակներ՝ hub։' },
]" />

## API և նավահանգիստներ {#api-and-ports}

<SettingTable :settings="[
  { name: 'API_PORT', type: 'string', default: '5148', description: 'Նավահանգիստը, որը API-ն լսում է իր կոնտեյների ներսում։ Ներդրվում է եզրային պրոքսիի կարգավորման մեջ, փոխեք միայն այստեղ։ Մակարդակներ՝ բոլորը։' },
  { name: 'CLIENT_PORT', type: 'string', default: '3000', description: 'Նավահանգիստը, որը հաճախորդի պորտալը լսում է իր կոնտեյների ներսում։ Ներդրվում է եզրային պրոքսիի կարգավորման մեջ, փոխեք միայն այստեղ։ Մակարդակներ՝ բոլորը։' },
  { name: 'API_GC_SERVER', type: 'string', default: '0', description: '.NET GC ռեժիմը՝ 0 workstation, 1 server։ Մակարդակներ՝ prod, hub։' },
  { name: 'API_GC_CONSERVE_MEMORY', type: 'string', default: '5', description: 'Հիշողության խնայման մակարդակը, 0-9։ Մակարդակներ՝ prod, hub։' },
  { name: 'API_GC_HEAP_HARD_LIMIT_PERCENT', type: 'string', default: '75', description: 'Heap-ի սահմանաչափը՝ կոնտեյների հիշողության տոկոսով։ Մակարդակներ՝ prod, hub։' },
]" />

### Պատվերների համարակալման և հաշիվ-ապրանքագրերի կարգավորումներ (միայն compose override-ում) {#order-numbering-and-invoice-settings-compose-override-only}

<Callout type="note">
Պատվերների համարակալման, հաշիվ-ապրանքագրերի և կրկնակի փորձերի կարգավորումները <code>.env</code>-ում չեն։ Սահմանեք դրանք անմիջապես API ծառայության վրա՝ compose override-ում։
</Callout>

| Կարգավորում | Լռելյայն | Նկարագրություն |
|---|---|---|
| `Orders__NumberPrefix` | `ORD-` | Պատվերի համարի նախածանցը |
| `Orders__NumberDigits` | `4` | Զրոներով լրացված թվանշաններ (օրինակ՝ `0042`) |
| `Orders__InvoicePaymentTermDays` | `7` | Հաշիվ-ապրանքագրի վճարման ժամկետը օրերով (0-365) |
| `Orders__RetryLeaseMinutes` | `10` | Րոպեներ, որոնց ընթացքում պատվերի տողի վրա դրված պահանջը (ընդունման կամ կրկնակի փորձի) հարգվում է, մինչև կրկնակի փորձը կարող է վերցնել տողը (1-ից 1440)։ Ժամկետը հաշվվում է այն պահից, երբ սկսվում է հենց տողի գործողությունը, և ադմինի պատվերի էջը կրկնակի փորձ առաջարկում է միայն այնտեղ, որտեղ այս ժամկետը թույլ է տալիս։ Սահմանեք ավելի երկար, քան ամենադանդաղ գրանցողի կամ պրովիժնինգի կանչը։ |
| `InvoicePdf__MaxLines` | `500` | PDF-ում տողերի առավելագույն քանակը |
| `InvoicePdf__MaxConcurrentRenders` | `4` | PDF-ի միաժամանակյա ռենդերներ |
| `InvoicePdf__MaxRenderWaitSeconds` | `10` | Վայրկյաններ, որոնք ներբեռնումը սպասում է ռենդերի ազատ տեղի՝ նախքան 503-ով և Retry-After-ով պատասխանելը։ |

## Հաճախորդի պորտալի տեսքը և բովանդակությունը {#client-portal-look-and-content}

Այս բոլոր կարգավորումները կառավարում են հանրային կայքը և ունեն NUXT_PUBLIC_PORTAL_* միջավայրի փոփոխականներով պահեստային տարբերակ։ Երբ ադմինի վահանակի Settings բաժնում արժեք է սահմանվում, այն գերակայում է միջավայրի փոփոխականների նկատմամբ։

<SettingTable :settings="[
  { name: 'NUXT_PUBLIC_PORTAL_TEMPLATE', type: 'string', default: 'classic', description: 'Հանրային կայքի ձևանմուշը՝ classic, aurora կամ nova։ Ադմինի Settings-ը գերակայում է։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_APPS_ENABLED', type: 'flag', default: 'false', description: 'Ցույց տալ հավելվածների գործարկիչը վերնագրում։ true/1/yes/on։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_CONTACT_EMAIL', type: 'string', default: '', description: 'Էջատակի կապի էլ. փոստը։ Դատարկ լինելու դեպքում թաքցվում է։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_SOCIAL_TWITTER', type: 'string', default: '', description: 'Twitter/X պրոֆիլի URL։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_SOCIAL_LINKEDIN', type: 'string', default: '', description: 'LinkedIn պրոֆիլի URL։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_SOCIAL_FACEBOOK', type: 'string', default: '', description: 'Facebook պրոֆիլի URL։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_HOSTING_DEMO_URL', type: 'string', default: '', description: 'Դեմո կայքի URL, որին հղում կա գլխավոր էջի ապրանքների բաժնից։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_CONTACT_ADDRESS', type: 'string', default: '', description: 'Փոստային հասցե, որը ցուցադրվում է կապի էջում։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_CONTACT_MAP_URL', type: 'string', default: '', description: 'Քարտեզի հղումը կապի էջում։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_STATS_CLIENTS', type: 'string', default: '', description: 'Գլխավոր էջի վիճակագրություն՝ հաճախորդների քանակը (ազատ տեքստ)։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_STATS_UPTIME', type: 'string', default: '', description: 'Գլխավոր էջի վիճակագրություն՝ անխափան աշխատանքի ցուցանիշը։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_STATS_REGIONS', type: 'string', default: '', description: 'Գլխավոր էջի վիճակագրություն՝ տարածաշրջանների քանակը։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_HERO_BADGE', type: 'string', default: '', description: 'Կարճ նշանակի տեքստը գլխավոր էջի վերնագրի վերևում։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_SITE_NAME', type: 'string', default: '', description: 'Կայքի անունը բրաուզերի վերնագրի և սոցիալական նախադիտումների համար։ Ադմինի Settings-ը գերակայում է։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_SITE_TAGLINE', type: 'string', default: '', description: 'Կայքի նշանաբանը։ Ադմինի Settings-ը գերակայում է։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_THEME_DEFAULT', type: 'string', default: 'dark', description: 'Գույնի լռելյայն ռեժիմը՝ light, dark կամ system։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_THEME_USER_TOGGLE', type: 'flag', default: 'true', description: 'Ցույց տալ light/dark փոխարկիչը։ Թաքցնում է միայն բառացի false արժեքը։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_LOGO', type: 'string', default: '', description: 'Լոգոյի URL (բաց ֆոնի համար)։ Դատարկի դեպքում օգտագործվում է ներկառուցված նշանը։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_LOGO_DARK', type: 'string', default: '', description: 'Լոգոյի URL (մուգ ֆոնի համար)։ Պահեստային տարբերակը NUXT_PUBLIC_PORTAL_LOGO-ն է։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_LOGO_MARK', type: 'string', default: '', description: 'Քառակուսի նշան փոքր տարածքների համար։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_FAVICON', type: 'string', default: '', description: 'Favicon-ի URL։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_BRAND_PRIMARY', type: 'string', default: '', description: 'Բրենդի հիմնական գույնը (#rrggbb)։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_BRAND_ACCENT', type: 'string', default: '', description: 'Բրենդի շեշտադրման գույնը (#rrggbb)։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_BRAND_FONT_HEADING', type: 'string', default: '', description: 'Վերնագրերի տառատեսակը՝ inter, noto-sans-armenian, noto-serif-armenian կամ system։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_BRAND_FONT_BODY', type: 'string', default: '', description: 'Հիմնական տեքստի տառատեսակը։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_GTM_ID', type: 'string', default: '', description: 'Google Tag Manager կոնտեյների ID-ն։ Սպասում է cookie-ների համաձայնությանը։ Ադմինի Settings-ը գերակայում է։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_CHAT_BASE_URL', type: 'string', default: '', description: 'Կենդանի չատի սերվերի URL։ Նաև build-ի ժամանակվա ARG է (ներառվում է պատկերի մեջ)։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_CHAT_WEBSITE_TOKEN', type: 'string', default: '', description: 'Չատի վիջեթի թոքենը (լռելյայն լեզու)։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_CHAT_WEBSITE_TOKEN_RU', type: 'string', default: '', description: 'Չատի վիջեթի թոքենը (ռուսերեն)։ Մակարդակներ՝ բոլորը։' },
  { name: 'NUXT_PUBLIC_PORTAL_CHAT_WEBSITE_TOKEN_HY', type: 'string', default: '', description: 'Չատի վիջեթի թոքենը (հայերեն)։ Մակարդակներ՝ բոլորը։' },
]" />

## Վճարումներ և արտաքին հղումներ {#payments-and-external-links}

<SettingTable :settings="[
  { name: 'BILLING_PORTAL_URL', type: 'string', default: '', description: 'Վճարումների պորտալի հասցեն (առանց վերջի շեղ գծի)։ Դատարկ թողնելու դեպքում փոխանցման հղումները թաքցվում են։ Մակարդակներ՝ բոլորը։' },
]" />


## Պլագիններ {#plugins}

<Callout type="note">
Պլագինների կարգավորումները <code>.env</code>-ում չեն։ Սահմանեք դրանք API ծառայության վրա՝ compose override-ում։
</Callout>

| Կարգավորում | Լռելյայն | Նկարագրություն |
|---|---|---|
| `Plugins__InstallPath` | *(դատարկ, պահեստային տարբերակը՝ `plugins-installed` հավելվածի կողքին)* | Թղթապանակ, որտեղ API-ն տեղադրում է պլագինների փաթեթները։ |
| `Plugins__MaxEntryCount` | `1000` | Պլագինի zip արխիվում ֆայլերի առավելագույն քանակը։ |
| `Plugins__MaxUncompressedBytes` | `209715200` | Պլագինի արխիվի ընդհանուր չսեղմված չափի առավելագույնը բայթերով (200 MiB)։ |
| `Plugins__MigrationTimeoutSeconds` | `120` | Ամենաերկար ժամանակը վայրկյաններով, որ կարող է աշխատել պլագինի մեկ միգրացիայի hook-ը թարմացումից հետո (1-ից 3600)։ Դրանից հետո հոսթը դադարում է սպասել և պլագինը թողնում է անջատված, մինչև հաջորդ փորձը հաջողվի։ |
| `Plugins__MaxViewBytes` | `262144` | Պլագինի մեկ view փաստաթղթի առավելագույն չափը բայթերով (էջի կամ slot-ի `view` ֆայլ, 256 KiB). ավելի մեծ ֆայլը պլագինը մերժում է բեռնելիս և տեղադրելիս։ 1-ից 4194304 (4 MiB)։ |
| `Plugins__TrustedPackageSha256__0`,  և այլն | n/a | Պլագինների փաթեթների SHA-256 հեշեր, որոնց պետք է վստահել անվերապահորեն։ Յուրաքանչյուր հեշ առանձին ինդեքսավորված բանալի է։ |
| `PluginApi__MaxBodyBytes` | `1048576` | Հարցման ամենամեծ մարմինը, որ հոսթը կարդում է պլագինի երթուղու համար, բայթերով (1-ից 67108864)։ |
| `PluginApi__CallTimeoutSeconds` | `30` | Վայրկյաններ, որոնք հոսթը սպասում է պլագինի երթուղու պատասխանին (1-ից մինչև ստորև նշված առաստաղը)։ Դրանից հետո պլագինը չեղարկվում է իր թոքենով, և կանչողը ստանում է `PLUGIN_TIMEOUT` (504)։ Հոսքային պատասխանի դեպքում այն ներառում է պատասխանի ու առաջին իրադարձության սպասումը և իրադարձությունների միջև լռությունը, բայց ոչ երբեք շարունակ ուղարկող հոսքի երկարությունը։ |
| `PluginApi__MaxCallTimeoutSeconds` | `600` | Երթուղու առանձին սահմանաչափի առաստաղը (1-ից 3600)։ Այսօր յուրաքանչյուր երթուղի ստանում է `CallTimeoutSeconds`-ը. երթուղին կկարողանա ավելի երկար սահմանաչափ խնդրել՝ մինչև սա, երբ պլագինների SDK-ում դրա համար դաշտ լինի։ Պետք է լինի առնվազն `CallTimeoutSeconds`։ |
| `PluginApi__AbandonWaitSeconds` | `5` | Վայրկյաններ, որոնք հոսթը կանչի սահմանաչափից հետո սպասում է չեղարկումն անտեսող պլագինին, մինչև լքի կանչը (1-ից 60)։ Կանչողը երկու դեպքում էլ ստանում է `PLUGIN_TIMEOUT`։ |
| `PluginApi__MaxAbandonedCallsPerPlugin` | `8` | Մեկ պլագինի լքված կանչերի առավելագույն քանակը, որոնք դեռ կարող են աշխատել (1-ից 1000)։ Սահմանաչափին հասնելիս այդ պլագինի նոր կանչերը անմիջապես մերժվում են `PLUGIN_TIMEOUT`-ով, մինչև դրանցից մեկը ավարտվի։ |
| `PluginApi__StreamHeartbeatSeconds` | `15` | Վայրկյաններ, որոնք հոսքային պլագինի պատասխանը կարող է լուռ մնալ, մինչև հոսթը կապը կենդանի պահելու համար մեկնաբանության տող գրի (1-ից 300)։ |
| `PluginApi__StreamEventMaxBytes` | `65536` | Հոսքային պլագինի պատասխանի մեկ իրադարձության տվյալների ամենամեծ չափը բայթերով (1-ից 1048576)։ Ավելի մեծ իրադարձություն ուղարկող պլագինը հոսքն ավարտում է `error` իրադարձությամբ։ |
| `PluginSigning__MaintainerKeys__0__KeyId`,  և այլն | *(ներառված է պատկերում)* | Ed25519 ստորագրման բանալիների գրառումներ պաշտոնական պլագինների պահպանողների համար։ Ներառված են պատկերում, օպերատորները դրանք չեն սահմանում։ |
| `PluginSigning__OperatorKeys__0__KeyId` | n/a | Օպերատորի վստահած պլագինի ստորագրողի բանալու ID-ն։ Յուրաքանչյուր գրառում `__KeyId` և `__PublicKey` զույգ է նույն ինդեքսի տակ։ Compose ֆայլերի հետ սահմանեք `PLUGIN_OPERATOR_KEY_ID`։ Երկուսն էլ դատարկ թողեք՝ ոչինչ չավելացնելու համար. երկուսից միայն մեկը սահմանելը կանգնեցնում է API-ն գործարկման պահին։ |
| `PluginSigning__OperatorKeys__0__PublicKey` | n/a | 0 ինդեքսով օպերատորի վստահած ստորագրողի Ed25519 հանրային բանալին։ Ավելի շատ ստորագրողներ ավելացրեք որպես `__1__KeyId` / `__1__PublicKey` և այլն։ Compose ֆայլերի հետ սահմանեք `PLUGIN_OPERATOR_PUBLIC_KEY`։ |

## Միայն մշակման կարգավորումներ {#development-only-settings}

<Callout type="warning">
Այս բաժնի կարգավորումները նախատեսված են միայն տեղական մշակման համար։ Մի օգտագործեք դրանք սերվերների վրա։
</Callout>

<SettingTable :settings="[
  { name: 'POSTGRES_HOST_PORT', type: 'string', default: '5434', description: 'Հոսթի նավահանգիստը, որը հրապարակում է տվյալների բազայի կոնտեյները։ Մակարդակներ՝ dev։' },
  { name: 'RABBITMQ_HOST_PORT', type: 'string', default: '5673', description: 'Հոսթի նավահանգիստը, որը հրապարակում է RabbitMQ-ն։ Մակարդակներ՝ dev։' },
  { name: 'RABBITMQ_MANAGEMENT_HOST_PORT', type: 'string', default: '15673', description: 'Հոսթի նավահանգիստը RabbitMQ-ի կառավարման ինտերֆեյսի համար։ Մակարդակներ՝ dev։' },
  { name: 'MAILHOG_SMTP_HOST_PORT', type: 'string', default: '1026', description: 'Հոսթի նավահանգիստը Mailhog SMTP-ի համար։ Մակարդակներ՝ dev։' },
  { name: 'MAILHOG_UI_HOST_PORT', type: 'string', default: '8027', description: 'Հոսթի նավահանգիստը Mailhog-ի վեբ ինտերֆեյսի համար։ Մակարդակներ՝ dev։' },
  { name: 'API_HOST_PORT', type: 'string', default: '5149', description: 'Հոսթի նավահանգիստը, որը հրապարակում է API կոնտեյները։ Մակարդակներ՝ dev։' },
  { name: 'CLIENT_HOST_PORT', type: 'string', default: '3001', description: 'Հոսթի նավահանգիստը, որը հրապարակում է հաճախորդի պորտալը։ Մակարդակներ՝ dev։' },
  { name: 'ADMIN_HOST_PORT', type: 'string', default: '5174', description: 'Հոսթի նավահանգիստը, որը հրապարակում է ադմինի վահանակը։ Մակարդակներ՝ dev։' },
  { name: 'CORS_CLIENT_ORIGIN', type: 'string', default: 'http://localhost:3001', description: 'Բրաուզերի origin-ը հաճախորդի պորտալի համար (CORS)։ Մակարդակներ՝ dev։' },
  { name: 'CORS_ADMIN_ORIGIN', type: 'string', default: 'http://localhost:5174', description: 'Բրաուզերի origin-ը ադմինի վահանակի համար (CORS)։ Մակարդակներ՝ dev։' },
  { name: 'CORS_STAGE_ORIGIN', type: 'string', default: 'https://stage-panel.example.com', description: 'Բրաուզերի origin-ը staging հաճախորդի պորտալի համար (CORS)։ Մակարդակներ՝ dev։' },
  { name: 'CLIENT_DEV_ALLOWED_HOSTS', type: 'string', default: '', description: 'Լրացուցիչ հոսթ-անուններ, որոնց պատասխանում է հաճախորդի dev սերվերը (ստորակետով բաժանված)։ Մակարդակներ՝ dev։' },
  { name: 'ADMIN_DEV_ALLOWED_HOSTS', type: 'string', default: '', description: 'Լրացուցիչ հոսթ-անուններ, որոնց պատասխանում է ադմինի dev սերվերը (ստորակետով բաժանված)։ Մակարդակներ՝ dev։' },
  { name: 'DEV_FILE_POLLING', type: 'flag', default: 'true', description: 'Պարբերաբար ստուգել bind mount-երի փոփոխությունները (true/false)։ Պետք է Docker Desktop-ի mount-երի վրա։ Մակարդակներ՝ dev։' },
  { name: 'DEV_NUGET_AUDIT', type: 'flag', default: 'false', description: 'Միացնել NuGet փաթեթների աուդիտը dev API կոնտեյների ներսում։ Մակարդակներ՝ dev։' },
  { name: 'DEV_NUGET_SIGNATURE_VERIFICATION', type: 'flag', default: 'false', description: 'Միացնել NuGet ստորագրության ստուգումը։ Մակարդակներ՝ dev։' },
  { name: 'DEV_SEED_ADMIN_PASSWORD', type: 'string', default: 'Admin123!', description: 'Ադմինի հաշվի գաղտնաբառը, որը ստեղծում է dev տվյալների լցիչը։ Հրապարակված թեստային արժեք է, գաղտնիք չէ։ Մակարդակներ՝ dev։' },
  { name: 'DEV_SEED_CLIENT_PASSWORD', type: 'string', default: 'Test@12345', description: 'Հաճախորդի հաշվի գաղտնաբառը, որը ստեղծում է dev տվյալների լցիչը։ Մակարդակներ՝ dev։' },
  { name: 'TESTCONTAINERS_HOST_OVERRIDE', type: 'string', default: 'host.docker.internal', description: 'Docker հոսթը Testcontainers-ի համար dev API կոնտեյների ներսում։ Մակարդակներ՝ dev։' },
  { name: 'ADMIN_API_BASE', type: 'string', default: '/api', description: 'Բազային ուղի, որը ադմինի dev սերվերը պրոքսավորում է դեպի API։ Մակարդակներ՝ dev։' },
  { name: 'DB_CPUS', type: 'string', default: '1.0', description: 'CPU-ի սահմանաչափը տվյալների բազայի կոնտեյների համար։ Մակարդակներ՝ dev։' },
  { name: 'DB_MEMORY', type: 'string', default: '1G', description: 'Հիշողության սահմանաչափը տվյալների բազայի կոնտեյների համար։ Մակարդակներ՝ dev։' },
  { name: 'RABBITMQ_CPUS', type: 'string', default: '1.0', description: 'CPU-ի սահմանաչափը RabbitMQ-ի համար։ Մակարդակներ՝ dev։' },
  { name: 'RABBITMQ_MEMORY', type: 'string', default: '512M', description: 'Հիշողության սահմանաչափը RabbitMQ-ի համար։ Մակարդակներ՝ dev։' },
  { name: 'REDIS_CPUS', type: 'string', default: '0.5', description: 'CPU-ի սահմանաչափը Redis-ի համար։ Մակարդակներ՝ dev։' },
  { name: 'REDIS_MEMORY', type: 'string', default: '256M', description: 'Հիշողության սահմանաչափը Redis-ի համար։ Մակարդակներ՝ dev։' },
  { name: 'MAILHOG_CPUS', type: 'string', default: '0.2', description: 'CPU-ի սահմանաչափը Mailhog-ի համար։ Մակարդակներ՝ dev։' },
  { name: 'MAILHOG_MEMORY', type: 'string', default: '256M', description: 'Հիշողության սահմանաչափը Mailhog-ի համար։ Մակարդակներ՝ dev։' },
  { name: 'API_CPUS', type: 'string', default: '2.0', description: 'CPU-ի սահմանաչափը API-ի համար։ Մակարդակներ՝ dev։' },
  { name: 'API_MEMORY', type: 'string', default: '2G', description: 'Հիշողության սահմանաչափը API-ի համար։ Մակարդակներ՝ dev։' },
  { name: 'CLIENT_CPUS', type: 'string', default: '1.5', description: 'CPU-ի սահմանաչափը հաճախորդի պորտալի համար։ Մակարդակներ՝ dev։' },
  { name: 'CLIENT_MEMORY', type: 'string', default: '2G', description: 'Հիշողության սահմանաչափը հաճախորդի պորտալի համար։ Մակարդակներ՝ dev։' },
  { name: 'ADMIN_CPUS', type: 'string', default: '1.0', description: 'CPU-ի սահմանաչափը ադմինի վահանակի համար։ Մակարդակներ՝ dev։' },
  { name: 'ADMIN_MEMORY', type: 'string', default: '1536M', description: 'Հիշողության սահմանաչափը ադմինի վահանակի համար։ Մակարդակներ՝ dev։' },
]" />

## Դեմո ռեժիմ {#demo-mode}

<Label type="local" /> Դեմո ռեժիմը պահանջում է AUTH_MODE=local։

<Callout type="note">
Դեմո ռեժիմը պահանջում է AUTH_MODE=local և իր սեփական POSTGRES_DB-ն։ Այն ըստ ժամանակացույցի զրոյացնում է նմուշային տվյալները։ Ակտիվացնելու համար սահմանեք DEMO_ENABLED=true։
</Callout>

<SettingTable :settings="[
  { name: 'DEMO_ENABLED', type: 'flag', default: 'true', description: 'Միացնել դեմո ռեժիմը։ Մակարդակներ՝ demo։' },
  { name: 'DEMO_RESET_INTERVAL_HOURS', type: 'string', default: '24', description: 'Ժամեր տվյալների բազայի մաքրումների և նոր լցումների միջև։ Մակարդակներ՝ demo։' },
  { name: 'DEMO_ADMIN_EMAIL', type: 'string', default: 'admin@demo.local', description: 'Դեմո ադմինի մուտքի էլ. փոստը (ցուցադրվում է մուտքի էջում)։ Մակարդակներ՝ demo։' },
  { name: 'DEMO_ADMIN_PASSWORD', type: 'secret', default: '', description: 'Դեմո ադմինի գաղտնաբառը։ Առնվազն 8 նիշ՝ մեծ և փոքր տառերով։ Լռելյայն արժեք չկա. առանց դրա compose-ը չի գործարկվում։ Պարտադիր է demo-ի համար։ Մակարդակներ՝ demo։' },
  { name: 'DEMO_CLIENT_EMAIL', type: 'string', default: 'client@demo.local', description: 'Դեմո հաճախորդի մուտքի էլ. փոստը։ Մակարդակներ՝ demo։' },
  { name: 'DEMO_CLIENT_PASSWORD', type: 'secret', default: '', description: 'Դեմո հաճախորդի գաղտնաբառը։ Առնվազն 8 նիշ՝ մեծ և փոքր տառերով։ Պարտադիր է demo-ի համար։ Մակարդակներ՝ demo։' },
  { name: 'DEMO_CONTACT_EMAIL', type: 'string', default: '', description: 'Կապի էլ. փոստը, որը ցուցադրվում է դեմո հանրային կայքի էջատակում։ Դատարկի դեպքում մնում է մտացածին տեղապահը։ Մակարդակներ՝ demo։' },
  { name: 'DEMO_CONTACT_PHONE', type: 'string', default: '', description: 'Կապի հեռախոսահամարը, որը ցուցադրվում է դեմո հանրային կայքում։ Մակարդակներ՝ demo։' },
  { name: 'DEMO_SMTP_PORT', type: 'string', default: '1025', description: 'Դեմոյի Mailhog-ի նավահանգիստը ցանցի ներսում։ Մակարդակներ՝ demo։' },
  { name: 'DEMO_REDIS_PORT', type: 'string', default: '6379', description: 'Դեմոյի Redis-ի նավահանգիստը ցանցի ներսում։ Մակարդակներ՝ demo։' },
  { name: 'DEMO_SESSION_ENCRYPTION_KEY', type: 'secret', default: '', description: 'Սեսիայի գաղտնագրման բանալին դեմո պորտալի համար։ Օգտագործեք արտադրականից տարբեր արժեք։ Պարտադիր է demo-ի համար։ Մակարդակներ՝ demo։' },
  { name: 'DEMO_PUBLISH_ADDRESS', type: 'string', default: '127.0.0.1', description: 'Հասցեն, որին կապվում են դեմոյի հրապարակված նավահանգիստները։ Loopback-ը դրանք դուրս է պահում ցանցից։ Պարտադիր է demo-ի համար։ Մակարդակներ՝ demo։' },
  { name: 'DEMO_SMTP_ENCRYPTION', type: 'string', default: 'none', description: 'Դեմո նամակ ընդունիչի գաղտնագրումը։ Օգտագործեք none։ Պարտադիր է demo-ի համար։ Մակարդակներ՝ demo։' },
  { name: 'DEMO_API_HOST_PORT', type: 'string', default: '5150', description: 'Հոսթի նավահանգիստը դեմո API-ի համար։ Պարտադիր է demo-ի համար։ Մակարդակներ՝ demo։' },
  { name: 'DEMO_CLIENT_HOST_PORT', type: 'string', default: '3002', description: 'Հոսթի նավահանգիստը դեմո հաճախորդի պորտալի համար։ Պարտադիր է demo-ի համար։ Մակարդակներ՝ demo։' },
  { name: 'DEMO_ADMIN_HOST_PORT', type: 'string', default: '5175', description: 'Հոսթի նավահանգիստը դեմո ադմինի վահանակի համար։ Պարտադիր է demo-ի համար։ Մակարդակներ՝ demo։' },
  { name: 'DEMO_ADMIN_BASE_URL', type: 'string', default: 'http://localhost:5175', description: 'Դեմո ադմինի վահանակի բացարձակ URL-ը։ Պարտադիր է demo-ի համար։ Մակարդակներ՝ demo։' },
  { name: 'DEMO_CLIENT_BASE_URL', type: 'string', default: 'http://localhost:3002', description: 'Դեմո հաճախորդի պորտալի բացարձակ URL-ը։ Պարտադիր է demo-ի համար։ Մակարդակներ՝ demo։' },
  { name: 'DEMO_MAILHOG_UI_HOST_PORT', type: 'string', default: '8028', description: 'Հոսթի նավահանգիստը դեմո Mailhog-ի ինտերֆեյսի համար։ Մակարդակներ՝ demo։' },
  { name: 'DEMO_NETWORK_SUBNET', type: 'string', default: '', description: 'Պարտադիր է։ Դեմո համակարգի ներքին ցանցի CIDR տիրույթը։ .env.example-ում չկա. docker-compose.demo.yml-ը առանց դրա չի գործարկվում։ Օրինակ՝ 172.28.0.0/16։ Մակարդակներ՝ demo։' },
  { name: 'DEMO_EDGE_NETWORK_SUBNET', type: 'string', default: '', description: 'Պարտադիր է։ Դեմո եզրային ցանցի CIDR տիրույթը։ .env.example-ում չկա. docker-compose.demo.yml-ը առանց դրա չի գործարկվում։ Օրինակ՝ 172.29.0.0/16։ Մակարդակներ՝ demo։' },
]" />

## Գործարկման մերժումներ {#startup-refusals}

API-ն ստուգում է գաղտնիքները և հիմնական կարգավորումները, նախքան որևէ հարցում ընդունելը։ Եթե ստուգումը ձախողվում է, այն մատյանում գրում է ճշգրիտ բանալին և դուրս է գալիս։ Տարածված պատճառներ՝

| Նշան մատյանում | Պատճառ | Լուծում |
|---|---|---|
| Secret still CHANGE_ME or too short | JWT_SECRET-ը, ENCRYPTION_KEY-ը, POSTGRES_PASSWORD-ը, RABBITMQ_PASSWORD-ը կամ սահմանված HOSTPANEL_BACKCHANNEL_LOGOUT_KEY-ը տեղապահ է պարունակում կամ չափազանց կարճ է։ | Ստեղծեք իրական արժեք openssl rand-ով։ |
| SMTP_ENCRYPTION invalid | Արժեքը սահմանված չէ, սխալ է գրված կամ tls, ssl, none արժեքներից մեկը չէ։ | Սահմանեք ճիշտ արժեքը։ |
| Only one of an optional pair is set | TELEGRAM_BOT_TOKEN-ը սահմանված է, բայց TELEGRAM_CHAT_ID-ը դատարկ է, կամ հակառակը։ | Սահմանեք երկուսն էլ կամ մաքրեք երկուսն էլ։ |
| CLIENT_HOSTPANEL_SECRET missing | AUTH_MODE=sso է, բայց CLIENT_HOSTPANEL_SECRET-ը դատարկ է։ | Սահմանեք գաղտնիքը կամ անցեք AUTH_MODE=local։ |
| HOSTPANEL_BACKCHANNEL_LOGOUT_KEY too short | Արժեքը տեղակայված հոսթում 16 նիշից կարճ է։ | Ստեղծեք openssl rand -hex 24-ով։ |
| Order setting invalid | Orders__NumberPrefix-ը և Orders__NumberDigits-ը չեն կարող ստեղծել վավեր պատվերի համար, Orders__InvoicePaymentTermDays-ը տիրույթից դուրս է (0-365), կամ Orders__RetryLeaseMinutes-ը տիրույթից դուրս է (1-ից 1440)։ | Ուղղեք արժեքները compose override-ում։ |
| CORS_* origin invalid | Թույլատրված origin-ը ճիշտ բացարձակ URL չէ (օրինակ՝ բացակայում է սխեման)։ | Ուղղեք origin-ը .env-ում։ |

## Սխալների կոդեր {#error-codes}

Հաճախորդին վերադարձվող սխալների կոդերը՝ իրենց HTTP կարգավիճակով և հաղորդագրությամբ՝

| Կոդ | Կարգավիճակ | Հաղորդագրություն |
|---|---|---|
| `CONTACT_NOT_CONFIGURED` | 503 | «Կապի ձևը այս պահին հասանելի չէ։ Խնդրում ենք գրել կամ զանգահարել այս էջում նշված կոնտակտներով։» |
| `SIGN_IN_ACCOUNT_LOCKED` | 423 | «Չափազանց շատ սխալ գաղտնաբառեր։ Հաշիվը ժամանակավորապես արգելափակված է, փորձեք ավելի ուշ։» |

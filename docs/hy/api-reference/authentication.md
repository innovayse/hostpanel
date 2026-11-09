---
title: Նույնականացում
description: "Մուտք գործեք Hostpanel REST API և փոխանցեք մուտքի թոքենը կամ սեսիայի cookie-ն։ Երկգործոն մուտք, թոքենի կյանքի տևողություն և արտաքին մուտքի ռեժիմ։"
keywords: "hostpanel api նույնականացում, bearer թոքեն, սեսիայի cookie, երկգործոն մուտք, rest api"
---

# Նույնականացում {#authentication}

Մուտքը միանգամից երկու բան է տալիս՝ սեսիայի cookie բրաուզերների համար և մուտքի թոքեն պատասխանի մարմնում՝ մնացած ամեն ինչի համար։ Փոխանցեք դրանցից որևէ մեկը այն կանչերում, որոնք դա պահանջում են։ Սկրիպտները և այլ սերվերներն օգտագործում են թոքենը։

<Callout type="note">
Երկարակյաց API թոքեններ առայժմ չկան։ Յուրաքանչյուր թոքեն տրվում է մուտքի ժամանակ, գործում է 15 րոպե և երկարաձգվում է միայն այնքան ժամանակ, քանի դեռ դուք աշխատում եք։ Երբ սեսիան հասնում է առավելագույն տարիքի, սկրիպտը նորից մուտք է գործում։
</Callout>

## Մուտք {#sign-in}

<Label type="local" /> Միայն տեղական հաշիվներ՝ արտաքին մուտքի ծառայության դեպքում այս ուղիները պատասխանում են 404, տես [Մուտքի ռեժիմ](#sign-on-mode)։

```
POST /api/auth/login
```

Ուղարկեք էլ. փոստի հասցեն և գաղտնաբառը։ Պատասխանում է `200`՝ թոքենով, և նույն պատասխանում սահմանվում է սեսիայի cookie-ն՝

<SettingTable :settings="[
  { name: 'email', type: 'string', default: 'Չկա', description: 'Հաշվի էլ. փոստի հասցեն։' },
  { name: 'password', type: 'string', default: 'Չկա', description: 'Հաշվի գաղտնաբառը։' },
]" />

Պատասխանը թոքենն է և նրա կյանքի տևողությունը՝

<SettingTable :settings="[
  { name: 'accessToken', type: 'string', default: 'Չկա', description: 'Ստորագրված թոքենը։ Փոխանցեք այն Authorization վերնագրում։' },
  { name: 'expiresIn', type: 'number', default: 'Չկա', description: 'Թոքենի կյանքի տևողությունը վայրկյաններով։ 900, այսինքն՝ 15 րոպե։' },
]" />

Սխալ հավաստագրերը տալիս են `SIGN_IN_UNAUTHORIZED` (401)։ Անընդմեջ չափազանց շատ սխալ գաղտնաբառերից հետո հաշիվը արգելափակվում է և պատասխանում է `SIGN_IN_ACCOUNT_LOCKED` (423), տես [Հարցումների սահմանափակումներ](/hy/api-reference/rate-limits#sign-in-lockout)։

## Bearer թոքեն {#bearer-token}

Փոխանցեք թոքենը `Authorization` վերնագրում՝

```
Authorization: Bearer <token>
```

Թոքենը գործում է 15 րոպե։ Քանի դեռ շարունակում եք կանչերը, API-ն տալիս է թարմ թոքեն՝ կյանքի վերջին երրորդում հաջող պատասխանը նոր թոքենը տեղափոխում է `X-Renewed-Access-Token` վերնագրում։ Օգտագործեք այն հաջորդ կանչերում։ Սեսիան երբեք չի երկարաձգվում առավելագույն տարիքից այն կողմ, որը հաշվվում է մուտքից, և դրանից հետո պետք է նորից մուտք գործել։ Տեղադրումը այդ տարիքը սահմանում է `SESSION_MAX_MINUTES`-ով, լռելյայն՝ 720 րոպե։

<SettingTable :settings="[
  { name: 'Authorization', type: 'հարցման վերնագիր', default: 'Չկա', description: 'Bearer, որին հաջորդում է մուտքի թոքենը։ Երբ այն առկա է, cookie-ն անտեսվում է։' },
  { name: 'X-Renewed-Access-Token', type: 'պատասխանի վերնագիր', default: 'Չկա', description: 'Թարմ մուտքի թոքեն։ Ուղարկվում է, երբ ձեր օգտագործած թոքենը իր կյանքի վերջին երրորդում է, և կանչը հաջող է անցել։' },
]" />

## Սեսիայի cookie {#session-cookie}

Մուտքի պատասխանը նաև սահմանում է սեսիայի cookie-ն՝ `hostpanel_session`։ Այն `HttpOnly` է, ուստի էջի սկրիպտը չի կարող կարդալ այն, և `SameSite=Strict`, ուստի բրաուզերը երբեք չի ուղարկում այն մեկ այլ կայքից։ Այն `Secure` է ամենուր, բացի տեղական մշակման հոսթից։ Այն պահող բրաուզերին սեփական կանչերի համար ուրիշ ոչինչ պետք չէ։ API-ն նորից թողարկում է cookie-ն, երբ երկարաձգում է սեսիան։

Վիճակը փոխող հարցումը, այսինքն՝ `GET`-ից, `HEAD`-ից, `OPTIONS`-ից և `TRACE`-ից բացի ցանկացած մեթոդ, որն ուղեկցվում է սեսիայի cookie-ով, պետք է ունենա նաև այս վերնագիրը, այլապես այն մերժվում է `CSRF_HEADER_MISSING` (403) պատասխանով՝

<SettingTable :settings="[
  { name: 'X-Requested-With', type: 'հարցման վերնագիր', default: 'XMLHttpRequest', description: 'Պարտադիր է վիճակը փոխող հարցման համար, երբ հավաստագիրը սեսիայի cookie-ն է։ Մեկ այլ կայքի էջը չի կարող ավելացնել այս վերնագիրը առանց միջդոմենային թույլտվության, որը այս API-ն չի տալիս։' },
]" />

Bearer թոքենով նույնականացվող հարցմանը սա պետք չէ։ Պլագինների ուղիները՝ `/api/admin/plugins/{id}/ext/...` և `/api/client/plugins/{id}/...`, վիճակը փոխող յուրաքանչյուր հարցման համար պահանջում են վերնագիրը կամ bearer թոքեն։

## Ելք {#sign-out}

```
DELETE /api/auth/logout
```

Մաքրում է սեսիայի cookie-ն և պատասխանում է `200`՝ անկախ նրանից՝ սեսիա եղել է, թե ոչ։ Թոքենը վիճակ չի պահում՝ սերվերում չկա ինչ չեղարկել, ուստի թոքենի պատճենը մնում է վավեր մինչև ժամկետի ավարտը։ Գաղտնաբառի վերականգնումը, երկգործոն նույնականացումն անջատելը և էլ. փոստի հասցեի փոփոխությունը միանգամից ավարտում են հաշվի բոլոր սեսիաները։

## Երկգործոն նույնականացում {#two-factor-authentication}

Երբ հաշվի վրա միացված է երկգործոն նույնականացումը, մուտքը երկու կանչ է պահանջում։ Առաջինը պատասխանում է `200`՝ մուտքի թոքենի փոխարեն սպասող թոքենով և cookie չի սահմանում՝

```json
{
  "twoFactorRequired": true,
  "pendingToken": "..."
}
```

Ուղարկեք սպասող թոքենը՝ նույնականացման հավելվածի կոդի հետ միասին՝

```
POST /api/auth/2fa-login
```

<SettingTable :settings="[
  { name: 'pendingToken', type: 'string', default: 'Չկա', description: 'Սպասող թոքենը առաջին կանչից։ Այն կարճակյաց է։' },
  { name: 'code', type: 'string', default: 'Չկա', description: 'Նույնականացման հավելվածի կոդը։' },
]" />

Հաջողության դեպքում պատասխանը նույնն է, ինչ երկգործոն նույնականացումից դուրս մուտքի դեպքում՝ մուտքի թոքենը և սեսիայի cookie-ն։ Սխալ կոդը տալիս է `TWO_FACTOR_CODE_UNAUTHORIZED` (401)։ Վնասված, ժամկետանց կամ արդեն օգտագործված սպասող թոքենը տալիս է `PENDING_TOKEN_UNAUTHORIZED` (401)։

## Ինչ է պետք յուրաքանչյուր լսարանին {#what-each-audience-needs}

Յուրաքանչյուր endpoint էջում պահանջը նշված է **Access** բաժնում։ Պահանջները հետևյալն են՝

<SettingTable :settings="[
  { name: 'Anonymous', type: '', default: 'Չկա', description: 'Հավաստագիր պետք չէ։ Մուտքի ուղիները և հանրային կայքի հանրային ուղիները։' },
  { name: 'Any signed-in user', type: '', default: 'Չկա', description: 'Վավեր սեսիա՝ հաճախորդի կամ աշխատակցի։' },
  { name: 'Client', type: '', default: 'Չկա', description: 'Մուտք գործած հաճախորդ-օգտատեր, որը գործում է իր հաշվով։ Աշխատակցի մուտքը հաճախորդի հաշիվ չունի, և նման կանչը մերժվում է։' },
  { name: 'Client permission', type: '', default: 'Չկա', description: 'Հաճախորդի որոշ ուղիներին պետք է նաև իրավասություն, որը հաշվի սեփականատերը տվել է օգտատիրոջը, օրինակ՝ ViewPayInvoices։ Առանց դրա օգտատերը ստանում է CLIENT_PERMISSION_DENIED (403)։' },
  { name: 'Admin', type: '', default: 'Չկա', description: 'Մուտք գործած աշխատակից Admin դերով։' },
]" />

Իրավասությունները, որոնք կարող է ունենալ հաճախորդ-օգտատերը, թվարկված են հաճախորդների ուղեցույցի [Օգտատերեր և իրավասություններ](/hy/customers/account#permissions) բաժնում։

Հավաստագրից զուրկ կամ ժամկետանց հավաստագրով կանչը տալիս է `UNAUTHORIZED` (401)։ Հավաստագրով կանչը, որին այդտեղ թույլատրված չէ, տալիս է `FORBIDDEN` (403)։

## Մուտքի ռեժիմ {#sign-on-mode}

<Label type="sso" /> Տեղադրումը կարող է մուտքը հանձնել արտաքին ծառայության՝ սահմանելով `AUTH_MODE=sso`։ Այդ ռեժիմում API-ի տեղական ուղիները՝ `login`, `2fa-login`, `register`, `forgot-password`, `reset-password` և `confirm-email`, պատասխանում են 404, և API-ն սեփական թոքեններ չի թողարկում՝ սեսիաներն ու թոքենները գալիս են մուտքի ծառայությունից։ `GET /api/auth/mode`-ը հաճախորդին ասում է, թե տեղադրումը որ ռեժիմում է։ Կարգավորումները տես [Մուտքի ռեժիմներ](/hy/operators/configuration#sign-in-modes) բաժնում։

## Ամբողջական օրինակ {#a-full-example}

Այս երեք կանչերը մուտք են գործում, կանչում հաճախորդի ուղին և կանչում ադմինի ուղին։ Հաճախորդի ուղուն պետք է հաճախորդ-օգտատիրոջ մուտք, իսկ ադմինի ուղուն՝ աշխատակից, ուստի օրինակում մուտքը կատարվում է երկու անգամ։

**Քայլ 1։ Մուտք գործեք որպես հաճախորդ-օգտատեր և կարդացեք թոքենը։**

::: code-group

```bash [curl]
curl -X POST 'https://panel.example.com/api/auth/login' \
  -H 'Content-Type: application/json' \
  -d '{"email": "client@example.com", "password": "your-password"}'
```

```js [JavaScript]
const signIn = await fetch('https://panel.example.com/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email: 'client@example.com', password: 'your-password' }),
})
const { accessToken } = await signIn.json()
```

:::

Պատասխանը պարունակում է թոքենը՝

```json
{
  "accessToken": "eyJhbGciOi...",
  "expiresIn": 900
}
```

**Քայլ 2։ Կանչեք հաճախորդի ուղին թոքենով։** Սա վերադարձնում է մուտք գործած հաճախորդի հաշիվ-ապրանքագրերը։

::: code-group

```bash [curl]
curl 'https://panel.example.com/api/me/invoices' \
  -H 'Authorization: Bearer <token>'
```

```js [JavaScript]
const invoices = await fetch('https://panel.example.com/api/me/invoices', {
  headers: { Authorization: `Bearer ${accessToken}` },
})
console.log(await invoices.json())
```

:::

**Քայլ 3։ Կանչեք ադմինի ուղին։** Մուտք գործեք որպես աշխատակից նույն կերպ, ապա այդ թոքենով կարդացեք հաճախորդների մի էջ՝

::: code-group

```bash [curl]
curl 'https://admin.example.com/api/clients?Page=1&PageSize=20' \
  -H 'Authorization: Bearer <staff-token>'
```

```js [JavaScript]
const clients = await fetch('https://admin.example.com/api/clients?Page=1&PageSize=20', {
  headers: { Authorization: `Bearer ${staffToken}` },
})
console.log(await clients.json())
```

:::

Եթե պատասխանը պարունակում է `X-Renewed-Access-Token`, հաջորդ կանչերում ուղարկեք այդ թոքենը։ Երրորդ կանչի պատասխանը արդյունքների մի էջ է, տես [Էջավորում](/hy/api-reference/pagination)։

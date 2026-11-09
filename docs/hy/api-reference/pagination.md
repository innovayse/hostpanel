---
title: Էջավորում
description: "Ինչպես են Hostpanel-ի ցուցակային endpoint-ները տվյալները վերադարձնում մեկ էջով՝ Page և PageSize հարցման պարամետրերը և PaginatedList պատասխանի մարմինը։"
keywords: "api էջավորում, page, pagesize, paginatedlist"
---

# Էջավորում {#pagination}

Ցուցակային endpoint-ները, որոնք կարող են շատ տողեր վերադարձնել, պատասխանում են մեկ էջով։ Endpoint էջերը դա նշում են՝ նրանց հարցման պարամետրերի մեջ կան `Page` և `PageSize`, իսկ պատասխանի մարմինը `PaginatedList` է։

## Հարցում {#request}

<SettingTable :settings="[
  { name: 'Page', type: 'number', default: 'Չկա', description: 'Կարդացվող էջի համարը՝ հաշվարկը 1-ից։ Անունը համեմատվում է առանց տառաչափի հաշվառման, ուստի page-ն էլ է աշխատում։ Ցուցակների մեծ մասում 1-ից փոքր արժեքը մերժվում է վավերացման սխալով։' },
  { name: 'PageSize', type: 'number', default: 'Չկա', description: 'Քանի տարր է տեղավորվում մեկ էջում։ Ամենամեծ չափը կախված է ցուցակից՝ 100 սովորական ցուցակի համար, 200՝ հաշվետվության։ Ցուցակների մեծ մասում 1-ից մինչև ամենամեծը տիրույթից դուրս չափը մերժվում է։' },
]" />

Փոխանցեք դրանք հարցման տողում։ Սա կարդում է 25 հաճախորդից բաղկացած երկրորդ էջը՝

```
GET /api/clients?Page=2&PageSize=25
```

<Callout type="note">
Հարցման մեջ անուններն են <code>Page</code> և <code>PageSize</code>, բայց պատասխանում էջը կոչվում է <code>pageNumber</code>։ <code>pageNumber</code> պարամետրով հարցումը այդ էջը չի կարդում՝ անունը չի ճանաչվում, ուստի կանչը մշակվում է այնպես, կարծես էջ նշված չէ։
</Callout>

Ցուցակների մեծ մասում տիրույթից դուրս արժեքները մերժվում են, ոչ թե ուղղվում։ `GET /api/clients?Page=0&PageSize=500` հարցումը պատասխանում է `400`՝ `VALIDATION_ERROR`-ով՝

```json
{
  "type": "VALIDATION_ERROR",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "errors": {
    "page": ["Page must be 1 or greater."],
    "pageSize": ["Page size must be between 1 and 100."]
  }
}
```

Մի քանի ցուցակներ, այդ թվում օգտատերերի ցուցակը, արժեքներն ուղղում են՝ ամենամեծից մեծ չափը կտրվում է մինչև նրան, իսկ 1-ից փոքր էջը կարդացվում է որպես 1։ Endpoint էջը չի ասում, թե ցուցակը որ տեսակից է, ուստի ստուգեք պատասխանում ստացված արժեքները։

Վերջինից հետո էջը սխալ չէ՝ այն պատասխանում է `200`՝ դատարկ `items` ցուցակով։

## Պատասխան {#response}

Էջավորված endpoint-ը պատասխանում է `PaginatedList` օբյեկտով՝

<SettingTable :settings="[
  { name: 'items', type: 'array', default: 'Չկա', description: 'Այս էջի տարրերը։' },
  { name: 'pageNumber', type: 'number', default: 'Չկա', description: 'Այս էջի համարը՝ հաշվարկը 1-ից։' },
  { name: 'pageSize', type: 'number', default: 'Չկա', description: 'Կիրառված էջի չափը։' },
  { name: 'totalPages', type: 'number', default: 'Չկա', description: 'Քանի էջ ունի ամբողջ արդյունքը։' },
  { name: 'totalCount', type: 'number', default: 'Չկա', description: 'Քանի տարր ունի ամբողջ արդյունքը։' },
  { name: 'hasPreviousPage', type: 'boolean', default: 'Չկա', description: 'True, երբ pageNumber-ը մեծ է 1-ից։' },
  { name: 'hasNextPage', type: 'boolean', default: 'Չկա', description: 'True, երբ pageNumber-ը փոքր է totalPages-ից։' },
  { name: 'metadata', type: 'object կամ null', default: 'Չկա', description: 'Լրացուցիչ տվյալներ ամբողջ ցուցակի մասին այն endpoint-ների համար, որոնք ունեն այդպիսիք։ Մնացած դեպքերում՝ Null։' },
]" />

```json
{
  "items": [
    { "id": 8, "email": "customer5@example.test" },
    { "id": 7, "email": "customer4@example.test" }
  ],
  "pageNumber": 1,
  "pageSize": 2,
  "totalPages": 10,
  "totalCount": 20,
  "hasPreviousPage": false,
  "hasNextPage": true,
  "metadata": null
}
```

Բոլոր էջերը կարդալու համար հետևեք `hasNextPage`-ին, մինչև այն դառնա false։

Որոշ ցուցակային endpoint-ներ վերադարձնում են պարզ զանգված՝ առանց էջավորման, օրինակ՝ հաճախորդի հաշիվ-ապրանքագրերը։ Նրանց endpoint էջը ցույց է տալիս զանգվածը որպես մարմին։

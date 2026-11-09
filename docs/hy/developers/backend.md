---
title: Բեքենդ
description: Ինչպես է հարցումն անցնում կոնտրոլերից հենդլեր և հետ։
---

# Բեքենդ {#backend}

## Հարցման ընթացքը {#the-request-flow}

```
HTTP request
  └── Controller (extends BaseApiController)
        └── ValidationPolicy  (invalid: 400 VALIDATION_ERROR)
              └── Handler
                    └── HandleResult + StatusMapping
                          ├── success: 200 / 201 / 204
                          └── error:   ErrorResponse body
```

Յուրաքանչյուր HTTP հարցում անցնում է նույն ճանապարհով.

1. `BaseApiController`-ը ժառանգող կոնտրոլերը ստանում է հարցումը և հաղորդագրություն (command կամ query) ուղարկում հաղորդագրությունների բուս։
2. `ValidationPolicy`-ն կանգնեցնում է հաղորդագրությունը, ստուգում է այն և վալիդացիայի սխալի դեպքում վերադարձնում է `400 VALIDATION_ERROR` պատասխան։ Վալիդացիայի սխալի դեպքում հենդլերը չի աշխատում։
3. Հենդլերն աշխատում է և վերադարձնում է `Result`։
4. `BaseApiController`-ի `HandleResult`-ը ստուգում է `Result`-ը, `StatusMapping`-ի միջոցով ընտրում է HTTP կարգավիճակի կոդը և գրում է պատասխանը։ Սխալի դեպքում մարմինը `ErrorResponse` է։

## Command-ներ և query-ներ {#commands-and-queries}

Օգտագործման դեպքերը գտնվում են `Innovayse.Hostpanel.Application/<Feature>/` թղթապանակում։ Յուրաքանչյուր դեպք ունի իր ենթաթղթապանակը.

```
Application/<Feature>/Commands/<UseCase>/
  <UseCase>Command.cs
  <UseCase>Handler.cs
  <UseCase>Validator.cs

Application/<Feature>/Queries/<UseCase>/
  <UseCase>Query.cs
  <UseCase>Handler.cs
  <UseCase>Validator.cs
```

Նույն ֆիչերի երկու օգտագործման դեպքի ընդհանուր DTO-ները դրվում են մեկ անգամ՝ `<Feature>/Common/` թղթապանակում։

Հենդլերները հայտնաբերվում են ավտոմատ։ Երբեք մի գրանցեք կամ կանչեք դրանք ձեռքով։ Հաղորդագրությունները ինքնության տվյալ չեն կրում․ հենդլերները ընթացիկ օգտատիրոջը կարդում են `ICurrentUserService`-ից։

## Տվյալների հասանելիություն {#data-access}

Ռեպոզիտորիաներ չկան։ Հենդլերները անմիջապես վերցնում են `IHostpanelDbContext`-ը՝ որպես աշխատանքի միավոր։

Էության կարգավորումները գտնվում են `Infrastructure/Persistence/Configurations/` թղթապանակում։ Յուրաքանչյուր աղյուսակի, սյունակի, բանալու, ինդեքսի և սահմանափակման անուն պետք է լինի PascalCase։ Եթե որևէ նույնացուցիչ շեղվում է, `DatabaseIdentifierNamingTests`-ը խափանում է բիլդը։

Մոդելի յուրաքանչյուր փոփոխություն պահանջում է միգրացիա։ Եթե մոդելը և վերջին միգրացիան համաձայնեցված չեն, `ModelDiffBaselineTests`-ը խափանվում է։

## Պորտեր և ադապտերներ {#ports-and-adapters}

Պորտը `I<Port>` ինտերֆեյս է։ Բոլոր պորտերը գտնվում են `Application/Common/Interfaces/` թղթապանակում և ոչ մի այլ տեղ։ Դա ապահովում է `PortPlacementTests`-ը։

Ադապտերները իրագործում են պորտերը և գտնվում են `Infrastructure/<Capability>/` թղթապանակում։ Դրանք գրանցվում են երեք `DependencyInjection.cs` ֆայլերում (յուրաքանչյուր շերտի համար մեկը) և API-ի `Extensions/*Registration.cs` ֆայլերում։ Գրանցումը կատարվում է ըստ տիպի, երբեք ըստ անվան։

## Սխալների կոդեր {#error-codes}

Սխալների կոդերը տողային հաստատուններ են, որոնք սահմանված են `HostpanelErrorCodes.cs` (կոնկրետ ֆիչերի համար) և `SharedErrorCodes.cs` (ընդհանուր կիրառման) ֆայլերում։

Յուրաքանչյուր կոդ պահանջում է երեք բան.
1. Անգլերեն նախադասություն `ErrorMessages.resx` ֆայլում (`ru` և `hy` տարբերակները՝ հարևան ֆայլերում)։
2. HTTP կարգավիճակ `StatusMapping.cs` ֆայլում։
3. Ուրիշ ոչինչ։ Եթե կոդն առանց նախադասության է, `ResourceCoverageTests`-ը խափանում է բիլդը։

API-ը սխալները միշտ վերադարձնում է `ErrorResponse` մարմնով։

## Ֆոնային աշխատանքներ {#background-jobs}

Կրկնվող աշխատանքները ինքնուրույն վերաժամանակացվում են․ յուրաքանչյուր գործարկում ավարտվելուց առաջ հերթագրում է հաջորդը։ Բաշխված լիզինգը ապահովում է, որ կլաստերում յուրաքանչյուր աշխատանքի ճիշտ մեկ օրինակ աշխատի։ Աշխատանքների ինտերվալները և ժամանակի սահմանաչափերը կարգավորվում են `ScheduledJobs` օպցիաների բաժնում։

## Կոդի ոճ {#code-style}

`backend/src/`-ի բոլոր public անդամները պահանջում են XML փաստաթղթային մեկնաբանություններ։ Բացակայող փաստաթղթերը բիլդի խափանում են, ոչ թե վերանայման նկատառում։ Տե՛ս `rules/xml-docs.md` և `rules/csharp-style.md`։

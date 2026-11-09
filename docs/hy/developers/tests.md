---
title: Թեստեր և ստուգումներ
description: Բոլոր ստուգումները, որ գործարկում է CI-ը, և ինչպես գործարկել դրանք տեղային։
---

# Թեստեր և ստուգումներ {#tests-and-checks}

## Ինչ է գործարկում CI-ը {#what-ci-runs}

Յուրաքանչյուր pull request պետք է անցնի.

1. Բեքենդի թեստեր՝ դոմեյնի, ինտեգրացիոն, ճարտարապետական և պլագինների
2. Ադմինի վահանակ՝ lint, typecheck, unit թեստեր, end-to-end թեստեր
3. Հաճախորդի պորտալ՝ lint, typecheck, unit թեստեր

## Բեքենդի թեստեր {#backend-tests}

Բոլոր բեքենդի թեստերը գործարկեք API կոնտեյներում (`docker/` թղթապանակից).

```bash
./scripts/manage.sh test
```

Հինգ թեստային նախագիծ.

**`Innovayse.Hostpanel.Domain.Tests`**․ unit թեստեր դոմեյնի տրամաբանության համար։

**`Innovayse.Hostpanel.Integration.Tests`**․ յուրաքանչյուր օգտագործման դեպքի համար մեկ ֆայլ՝ `Integration.Tests/<Feature>/<UseCase>Tests.cs` ուղով։ Եթե օգտագործման դեպքի հենդլերը համապատասխան թեստային ֆայլ չունի, `UseCaseShapeTests`-ը խափանվում է։ Այս փաթեթը գործարկում է տվյալների բազայի իր սեփական կոնտեյները, ուստի արտաքին տվյալների բազա պետք չէ։ CI-ը այն բաժանում է զուգահեռ runner-ների միջև․ եթե ավելացնում եք նոր վերին մակարդակի ֆիչերի թղթապանակ, ավելացրեք այն `scripts/check-integration-shards.sh` ֆայլի որևէ մասում։

**`Innovayse.Hostpanel.Architecture.Tests`**․ ստուգում է կառուցվածքային կանոնները.

| Թեստի դաս | Ինչ է ստուգում |
|------------|---------------|
| `PortPlacementTests` | Բոլոր `I<Port>` ինտերֆեյսները գտնվում են `Application/Common/Interfaces/` թղթապանակում |
| `DatabaseIdentifierNamingTests` | Աղյուսակների, սյունակների և բանալիների բոլոր անունները PascalCase են |
| `UseCaseShapeTests` | Յուրաքանչյուր հենդլեր ունի համապատասխան ինտեգրացիոն թեստային ֆայլ |
| `ResourceCoverageTests` | Յուրաքանչյուր սխալի կոդ ունի նախադասություն `ErrorMessages.resx` ֆայլում |

`ModelDiffBaselineTests`-ը գտնվում է `Integration.Tests/Persistence` թղթապանակում և ստուգում է, որ EF մոդելը համապատասխանում է վերջին միգրացիային։

**`Innovayse.CWP.Tests`**, **`Innovayse.Inecobank.Tests`**․ թեստեր ներկառուցված պլագինների համար։

## Ադմինի վահանակ {#admin-panel}

Գործարկեք `admin/` թղթապանակից.

::: code-group

```bash [lint]
npm run lint
```

```bash [lint:i18n]
npm run lint:i18n
```

```bash [typecheck]
npm run typecheck
```

```bash [test]
npm test -- --maxWorkers=2
```

```bash [e2e]
npm run e2e
```

:::

## Հաճախորդի պորտալ {#client-portal}

Գործարկեք `client/` թղթապանակից։ Նոութբուքում թեստային worker-ները սահմանափակեք երկուսով, որպեսզի հիշողությունը չծանրաբեռնվի։

::: code-group

```bash [lint]
yarn lint
```

```bash [lint:i18n]
yarn lint:i18n
```

```bash [typecheck]
yarn typecheck
```

```bash [test]
yarn test --maxWorkers=2
```

```bash [e2e]
yarn e2e
```

:::

`lint` սկրիպտը ներառում է շերտերի ստուգումը (`client/scripts/check-layers.mjs`)։

## Մինչև pull request բացելը {#before-opening-a-pull-request}

- Ճյուղավորեք `develop`-ից, ոչ թե մեկ այլ ֆիչերի ճյուղից։
- Գործարկեք բեքենդի ֆորմատավորիչը։ Ճշգրիտ հրամանը տե՛ս `rules/dotnet-format.md` ֆայլում։
- Յուրաքանչյուր փոփոխության թեստերը ներառեք նույն pull request-ում։
- Հետևեք commit հաղորդագրության ոճին, որը նկարագրված է `rules/project.md` ֆայլում։
- Pull request-ի ամբողջական ստուգացանկի համար տե՛ս [Մասնակցություն նախագծին](./contributing)։

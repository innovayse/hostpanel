---
title: Որտեղ է դրվում նոր ֆիչերը
description: Փոփոխության յուրաքանչյուր տեսակի ֆայլը և աշխատանքային օրինակ։
---

# Որտեղ է դրվում նոր ֆիչերը {#where-a-new-feature-goes}

## Աղյուսակը {#the-table}

| Ինչ | Որտեղ |
|------|-------|
| Կոնտրոլերի action | `Innovayse.Hostpanel.API/Controllers/<Feature>Controller.cs` |
| Command | `Innovayse.Hostpanel.Application/<Feature>/Commands/<UseCase>/<UseCase>Command.cs` |
| Query | `Innovayse.Hostpanel.Application/<Feature>/Queries/<UseCase>/<UseCase>Query.cs` |
| Հենդլեր | `Innovayse.Hostpanel.Application/<Feature>/{Commands,Queries}/<UseCase>/<UseCase>Handler.cs` |
| Վալիդատոր | `Innovayse.Hostpanel.Application/<Feature>/{Commands,Queries}/<UseCase>/<UseCase>Validator.cs` |
| Ընդհանուր DTO | `Innovayse.Hostpanel.Application/<Feature>/Common/<Name>Dto.cs` |
| Պորտ | `Innovayse.Hostpanel.Application/Common/Interfaces/I<Port>.cs` |
| Սխալի կոդ | `Innovayse.Hostpanel.Application/Common/Errors/HostpanelErrorCodes.cs` |
| Կարգավիճակի համապատասխանեցում | `Shared/Controllers/StatusMapping.cs` |
| Սխալի նախադասություն (en) | `Innovayse.Hostpanel.Application/Resources/ErrorMessages.resx` |
| Սխալի նախադասություն (ru) | `Innovayse.Hostpanel.Application/Resources/ErrorMessages.ru.resx` |
| Սխալի նախադասություն (hy) | `Innovayse.Hostpanel.Application/Resources/ErrorMessages.hy.resx` |
| Էություն | `Innovayse.Hostpanel.Domain/<Feature>/<Entity>.cs` |
| Էության կարգավորում | `Innovayse.Hostpanel.Infrastructure/Persistence/Configurations/<Entity>Configuration.cs` |
| Միգրացիա | `Innovayse.Hostpanel.Infrastructure/Persistence/Migrations/` |
| Ադապտեր | `Innovayse.Hostpanel.Infrastructure/<Capability>/<Adapter>.cs` |
| Ինտեգրացիոն թեստ | `Innovayse.Hostpanel.Integration.Tests/<Feature>/<UseCase>Tests.cs` |
| Ադմինի էկրան | `admin/src/views/<area>/<ScreenName>View.vue` |
| Ադմինի store | `admin/src/stores/<area>.ts` |
| Ադմինի API կոմպոզաբլ | `admin/src/composables/apis/use<Area>Api.ts` |
| Ադմինի end-to-end թեստ | `admin/e2e/<area>.spec.ts` |
| Հաճախորդի էջ | `client/pages/<area>/<page>.vue` |
| Հաճախորդի store | `client/stores/<area>.ts` |
| Հաճախորդի API կոմպոզաբլ | `client/composables/use<Area>Api.ts` |
| Պորտալի սերվերային երթուղի | `client/server/api/portal/<area>/` |

## Աշխատանքային օրինակ {#a-worked-example}

Այս օրինակում ավելացվում է միայն կարդալու ադմինի ցանկի էկրան նոր «Labels» ֆիչերի համար։

**1. Query, հենդլեր և վալիդատոր**

Ստեղծեք `Innovayse.Hostpanel.Application/Labels/Queries/GetLabels/GetLabelsQuery.cs`, `GetLabelsHandler.cs` և `GetLabelsValidator.cs` ֆայլերը։ Հենդլերը վերցնում է `IHostpanelDbContext`-ը, կարդում է տողերը և վերադարձնում `Result<IReadOnlyList<LabelDto>>`։ `LabelDto` DTO-ն գտնվում է `Labels/Common/LabelDto.cs` ֆայլում․ `*Response` փաթաթող տիպեր չկան։

**2. Կոնտրոլերի action**

Ավելացրեք `GET /api/admin/labels` action `Innovayse.Hostpanel.API/Controllers/LabelsController.cs` ֆայլում (ստեղծեք այն, եթե չկա)։ Action-ը ստեղծում է `GetLabelsQuery`, կանչում է `await _bus.InvokeAsync<Result<IReadOnlyList<LabelDto>>>(query, ct)` և արդյունքը փոխանցում է `HandleResult`-ին։

**3. Ինտեգրացիոն թեստ**

Ստեղծեք `Innovayse.Hostpanel.Integration.Tests/Labels/GetLabelsTests.cs` ֆայլը։ Թեստը լցնում է տվյալների բազան, կանչում է endpoint-ը և ստուգում պատասխանի ձևը։ Մինչև այս ֆայլի առաջանալը `UseCaseShapeTests`-ը կխափանվի։

**4. Ադմինի store և API կոմպոզաբլ**

Ավելացրեք `admin/src/stores/labels.ts` ֆայլը `fetchLabels` action-ով, որը կանչում է `runAction`։ Ավելացրեք `admin/src/composables/apis/useLabelsApi.ts` ֆայլը `getLabels` ֆունկցիայով, որը կանչում է `useApi`։

**5. Ադմինի էկրան**

Ստեղծեք `admin/src/views/labels/LabelsView.vue` ֆայլը։ View-ն store-ի action-ը կանչում է մաունթի ժամանակ և արտացոլում ցանկը։ View-ում `useApi`-ի ուղիղ կանչեր չկան։

**6. Գործարկեք ստուգումները**

```bash
# backend (from docker/)
./scripts/manage.sh test

# admin
cd admin && npm run lint && npm run typecheck && npm test -- --maxWorkers=2
```

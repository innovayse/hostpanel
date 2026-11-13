---
title: Պլագինի ստեղծում
description: "Ստեղծեք Hostpanel պլագին որպես .NET դասերի գրադարան SDK-ի վրա՝ վճարային և պրովիժնինգի պլագիններ, plugin.json մանիֆեստ, փաթեթավորում և թեստեր։"
keywords: "write plugin, hostpanel sdk, payment plugin, provisioning plugin, plugin manifest"
---

# Պլագինի ստեղծում {#write-a-plugin}

Պլագինը .NET դասերի գրադարան է, որը կապվում է Hostpanel SDK-ին և ավելացնում է վճարային համակարգ, պրովիժնինգի մոդուլ, դոմենների գրանցող կամ այլ հնարավորություն։ Պլագինը երբեք չի կապվում անմիջապես Hostpanel-ի հիմնական հավելվածին, այլ միայն SDK-ին։

## Սկսելուց առաջ {#before-you-start}

SDK նախագիծը գտնվում է պահոցի `backend/src/Hostpanel.Sdk` ուղում։ Կապեք այն ձեր դասերի գրադարանին կամ անմիջապես կապեք պատրաստի `Hostpanel.Sdk.dll`-ը։ Այս ուղեցույցը գրված է Hostpanel.Sdk 3.1.0-ի համար, ուստի կառուցեք պլագինը այդ կամ ավելի նոր 3.x տարբերակի դեմ. ավելի հին SDK մաժոր տարբերակով կառուցված փաթեթը մերժվում է `PLUGIN_SDK_ASSEMBLY_OUTDATED` սխալով։ SDK-ի պահոցը հրապարակում է նաև երկրորդ փաթեթ՝ `Hostpanel.Sdk.Packaging`, որի տարբերակը համընկնում է SDK-ի տարբերակին (3.1.0)։ Այն պարունակում է այն ստուգումները, որոնք սերվերը կատարում է փաթեթի վրա, ուստի դրանք կարող եք գործարկել սեփական կառուցման վրա՝ մինչև վերբեռնելը։ Այդ ստուգումներն են՝ ստորագրության ձևաչափն ու ստուգումը, պլագինի նույնականացուցիչի և տարբերակի կանոնները, վերապահված assembly անունները, արխիվի սահմանաչափերն ու ուղիների ստուգումը, պատկերակի ստուգումը և մանիֆեստի կառուցվածքային կանոնները։ Կապեք այն միայն ձեր կառուցման կամ թողարկման գործիքներից, երբեք հենց պլագինի assembly-ից։ `backend/src/Innovayse.Providers.*` ներկառուցված պլագինները հղումային իրականացումներ են. կարդացեք դրանց կոդը՝ նախքան սեփականը գրելը։

### Անվանեք նախագիծը {#name-the-project}

Պլագինի նախագիծն անվանեք `<Company>.Hostpanel.Plugins.<Name>`՝ ըստ .NET պլագինների `<Company>.<Product>.<Type>.<Name>` պայմանավորվածության (ինչպես `Serilog.Sinks.Console`-ում)։ Նախագիծը, նրա թղթապանակը, assembly-ն և արմատային namespace-ը կրում են այդ անունը, իսկ թեստերի նախագիծը ավելացնում է `.Tests`։ Innovayse-ի սեփական պլագինները կոչվում են `Innovayse.Hostpanel.Plugins.<Name>`, թեստերը՝ `Innovayse.Hostpanel.Plugins.<Name>.Tests`, և յուրաքանչյուրը գտնվում է `hostpanel-<name>` անունով առանձին պահոցում։ Ներկառուցված `Innovayse.Providers.*` նախագծերը ստեղծվել են այս պայմանավորվածությունից առաջ և վերանվանվում են՝ իրենց առանձին պահոցներ տեղափոխվելիս։

`plugin.json`-ի պլագինի `id`-ն անվան մաս չէ և երբեք չի փոխվում նախագծի վերանվանման ժամանակ. սերվերը պլագինի պահված կարգավորումներն ու տվյալները կապում է հենց այս `id`-ի հետ։

## Պլագինների տեսակները {#plugin-types}

`PluginType` թվարկումը սահմանում է, թե ինչ կարող է լինել պլագինը։

| Տեսակ | Արժեք | Քննարկվում է այս ուղեցույցում |
|------|-------|-----------------------|
| `Provisioning` | 0 | Այո, տես ստորև |
| `Payment` | 1 | Այո, տես ստորև |
| `Registrar` | 2 | Միայն թվարկված է |
| `Identity` | 3 | Միայն թվարկված է |
| `Fulfilment` | 4 | Միայն թվարկված է |
| `Hosted` | 5 | Միայն թվարկված է |
| `App` | 6 | Միայն թվարկված է |

## Վճարային պլագին {#a-payment-plugin}

Ժառանգեք `PaymentGatewayBase`-ը և իրականացրեք `IPaymentPlugin`-ը `Hostpanel.Sdk.Plugins.Payment`-ից։

### Պարտադիր անդամներ {#required-members}

| Անդամ | Նկարագրություն |
|--------|-------------|
| `string CurrencyCode` | ISO 4217 թվային արժույթի կոդը, որով այս վճարային համակարգը հաշվարկներ է կատարում (օրինակ՝ `"840"` USD-ի համար, `"051"` AMD-ի համար) |
| `Task<PaymentSession> CreatePaymentAsync(PaymentInstruction instruction, CancellationToken ct)` | Սկսել վճարման սեսիա և վերադարձնել վերահասցեավորման URL |
| `Task<GatewayPaymentStatus> GetStatusAsync(string gatewayOrderId, CancellationToken ct)` | Հարցնել վճարային համակարգին ընթացիկ վիճակը |
| `Task<string> RefundAsync(string gatewayOrderId, long amountMinor, CancellationToken ct)` | Կատարել վերադարձ և վերադարձնել վճարային համակարգի վերադարձի հղումը |

### Տեսակներ {#types}

**`PaymentInstruction`** հատկություններ՝ `OrderNumber`, `AmountMinor`, `ReturnUrl`, `Description`, `Language`, `SavePaymentMethod` (SDK 3.5.0 կամ ավելի նոր, լռելյայն `false`)։

**`PaymentSession`** հատկություններ՝ `GatewayOrderId`, `RedirectUrl`, `FormPost` և `Kind` (`PaymentSessionKind`․ SDK 3.7.0 կամ ավելի նոր, լռելյայն՝ `Redirect`)։ Տե՛ս [Սեսիայի տեսակի ընտրություն](#session-kinds)։

**`GatewayPaymentStatus`** հատկություններ՝ `State` (`GatewayPaymentState`), `TransactionId`, `RawStatus`, `AmountMinor`, `Currency` (ISO 4217 թվային), `SavedMethod` (SDK 3.5.0 կամ ավելի նոր, լռելյայն `null`)։

`AcceptsCurrency(string currencyNumeric)`-ն ունի լռելյայն իրականացում, որը համեմատում է արժեքը `CurrencyCode`-ի հետ. վերասահմանեք այն միայն այն դեպքում, երբ վճարային համակարգը ընդունում է իր հաշվարկային արժույթից տարբեր արժույթներ։

**`GatewayPaymentState`** թվարկում՝ `Pending` (0), `Paid` (1), `Declined` (2)։

### Սեսիայի տեսակի ընտրություն {#session-kinds}

`CreatePaymentAsync`-ը վերադարձնում է `PaymentSession`, և սեսիան պատկանում է հենց մեկ `PaymentSessionKind`-ի (ձևի ուղարկման և բրաուզերում քայլի համար պետք է SDK 3.7.0 կամ ավելի նոր).

| Տեսակ | Երբ | Ինչ է լրացնում պլագինը | Ինչ է անում հոսթը |
|-------|-----|------------------------|-------------------|
| `Redirect` | վճարային համակարգն ունի տեղադրված էջ, որը բացվում է սովորական GET-ով (լռելյայն) | `RedirectUrl` | վճարողին ուղարկում է հասցեով, կարգավիճակը կարդում է, երբ վճարողը վերադառնում է |
| `FormPost` | տեղադրված էջը բացվում է POST-ով, որը կրում է թոքեն կամ այլ դաշտեր | `FormPost` (հասցե և դաշտեր) | կառուցում է ձևը և ուղարկում է վճարողի փոխարեն |
| `BrowserStep` | վճարողը բրաուզերում անցնում է քայլ (քարտի դաշտ, որը տալիս է nonce), նախքան վճարումը կարող է գանձվել | `RedirectUrl` (քայլի էջը) և `Kind` | վճարողին ուղարկում է էջ, սպասում է վերադարձի հարցմանը, նրա պարամետրերը փոխանցում է պլագինին և միայն դրանից հետո կարդում է կարգավիճակը |

Ընտրեք վերահասցեավորումը, երբ վճարային համակարգն այն առաջարկում է։ **Ձևի ուղարկման** դեպքում պլագինը միայն նկարագրում է ձևը․ հոսթը կիրառում է `PaymentFormPost`-ի և `PaymentFormPostLimits`-ի կանոնները (բացարձակ `https` հասցե՝ առանց օգտատիրոջ տվյալների և տողադարձերի, առավելագույնը 64 դաշտ, անունները՝ մինչև 128, արժեքները՝ մինչև 8192 նիշ, հասցեն՝ մինչև 2048), կառուցում է ձևը՝ անուններն ու արժեքները դնելով որպես հատկություններ, ոչ թե մարկափ, և ուղարկում է այն։ Կանոնը խախտող ձևը համարվում է ձախողված վճարում․ հաշվի վրա ոչինչ չի պահվում, և եթե պլագինը իրականացնում է `IPaymentCancelPlugin`, նոր գրանցված պատվերը չեղարկվում է։

### Վճարման ավարտը վերադարձի հարցումից {#payment-return}

Պլագինը, որը պատասխանում է `BrowserStep`-ով կամ որին պետք է արժեք, որը վերադարձի պահին կա միայն վճարողի բրաուզերի մոտ (վճարողի նույնացուցիչ վերադարձի հասցեում), իրականացնում է `IPaymentReturnPlugin`-ը `IPaymentPlugin`-ի կողքին և `capabilities`-ում նշում է `paymentReturn`։ `BrowserStep` սեսիան առանց այդ ինտերֆեյսի պլագինից համարվում է ձախողված վճարում։

Վճարողը վերադառնում է վահանակի արդյունքի էջ, որը հոսթին է ուղարկում վերադարձի հարցման մնացած query պարամետրերը։ Հետո հոսթը կանչում է `CompleteAsync(gatewayOrderId, returnParameters, ct)`, իսկ դրանից հետո՝ `GetStatusAsync`․ վճարողին ինչ ասելը որոշում է կարգավիճակը, ոչ թե `CompleteAsync`-ի վերադարձրած արժեքը։ Հոսթն ընտրում է, թե որ անունները կհասնեն պլագինին, և պահպանում է `PaymentReturnLimits`-ը․ անունները համեմատվում են ordinal կերպով, կրկնվող անվան դեպքում հաղթում է առաջին արժեքը, իսկ 32-ից ավելի պարամետրով, 64 նիշից երկար անունով կամ 2048 նիշից երկար արժեքով հավաքածուն մասամբ չի փոխանցվում, պլագինը ոչինչ չի ստանում։ Մուտքի տվյալները, cookie-ները և վերնագրերը երբեք չեն փոխանցվում։ Արժեքները գալիս են վճարողի բրաուզերից, ուստի ստուգեք դրանք վճարային համակարգի մոտ և երբեք մի վստահեք դրանց գումարի կամ պատվերի հարցում։ `CompleteAsync`-ը կրկին կանչվում է, երբ վճարողը վերադառնում է երկու անգամ, ուստի այն պետք է լինի իդեմպոտենտ։ Արդեն վճարված հաշվի համար այն չի կանչվում, ինչպես և համադրման առաջադրանքը, որը միայն կարդում է կարգավիճակը։ Եթե պլագինը բացառություն է նետում, կարգավիճակը չի կարդացվում, և վճարողին ասվում է, որ վճարային համակարգը անհասանելի է․ համադրման առաջադրանքը հետո կփակի վճարումը, եթե համակարգը այն ընդունել էր։

### Վճարողի եղանակի պահպանումը վճարման հետ {#save-payment-method}

Վճարային պլագինը, որն իրականացնում է `IStoredCredentialPlugin`-ը և հայտարարում է `storedCredentials` հնարավորությունը,
կարող է պահպանել այն եղանակը, որով հաճախորդը վճարում է, որպեսզի հոսթը հետագայում կարողանա գանձել դրանից։ Պահանջվում է
SDK 3.5.0 կամ ավելի նոր։

Պատվերի ձևակերպումը ցույց է տալիս այդպիսի պլագինը՝ `supportsStoredCredentials`-ը `true`, և մուտք գործած հաճախորդին
առաջարկում է քարտը պահպանելու նշիչ՝ համաձայնության տեքստով։ Երբ հաճախորդը նշում է այն, հոսթը նախ համոզվում է, որ
վճարային համակարգի հաճախորդը գոյություն ունի (`EnsureCustomerAsync`), հետո գրանցում է վճարումը
`PaymentInstruction.SavePaymentMethod`-ը `true` արժեքով և գրանցում է հաճախորդի համաձայնությունն այդ վճարման համար՝
տեքստի տարբերակի, հարցման հասցեի և բրաուզերի հետ միասին։ Պլագինը վճարման հետ ուղարկում է վճարային համակարգի սեփական
«պահպանել այս եղանակը» հարցումը։

Երբ `GetStatusAsync`-ը վճարումը կարդում է որպես `Paid`, այն վերադարձնում է պահպանված եղանակը
`GatewayPaymentStatus.SavedMethod`-ում։ Սա նույն `SavedMethod`-ն է, որը վերադարձնում է `ListMethodsAsync`-ը, ուստի եղանակը
հետագայում գանձվում է նույն կերպ։ Հոսթը կապում է այն այդ վճարման համաձայնությանը հաշիվը փակող նույն պահպանման մեջ, և
եղանակը հայտնվում է հաճախորդի պահպանված եղանակների մեջ։

- Վերադարձրեք `SavedMethod` միայն `Paid` վիճակում և միևնույն եղանակը նույն վճարման ամեն ընթերցման ժամանակ։
- Դրոշը չաջակցող պլագինը անտեսում է այն և եղանակ չի վերադարձնում։ Հոսթն այդ դեպքը համարում է «չպահպանված», ոչ թե վճարման սխալ։
- Եղանակը պահպանելու խնդրանք կարող է անել հաշվի մուտք գործած հաճախորդը, իսկ հաշվին կապված օգտվողը՝ միայն հաշիվները տեսնելու և վճարելու իրավունքով։ Հյուրի պատվերը վճարվում է առաջվա պես, առանց այդ հնարավորության։
- Պահպանված եղանակը վերադարձրեք նաև այդ հաճախորդի համար `ListMethodsAsync`-ից. հոսթը ապրանքանիշը, վերջին չորս թվանշանները և ժամկետը վերցնում է այդ ցանկից, իսկ կապված եղանակը, որը ցանկում չկա, ցույց է տալիս որպես անհասանելի։

### Կարգավորումների ընթերցում {#reading-settings}

Կարգավորումը կարդալու համար կանչեք `GetConfig(key)` (`PaymentGatewayBase`-ից)։ Կարգավորումները հավելվածի կոնֆիգուրացիայում պահվում են որպես `integration:{pluginId}:{key}` և գրվում են ադմինի վահանակի Ինտեգրացիաներ էջից։

```csharp
public string? GetBaseUrl() => GetConfig("gateway_url");
```

### Վերահասցեավորումները պլագինի HTTP կլիենտում {#http-redirects}

Հոսթը չի հետևում վերահասցեավորումներին այն ոչ մի HTTP կլիենտում, որ տալիս է պլագինին՝ ո՛չ պլագինի նույնացուցչով անվանված կլիենտում, ո՛չ ֆաբրիկայի անանուն կլիենտում։ 3xx պատասխանը պլագինին հասնում է այնպես, ինչպես կա, իր `Location` վերնագրով, որովհետև դրան հետևող կլիենտը պլագինի authorization վերնագիրը, իսկ որոշ մշակիչների դեպքում նաև հարցման մարմինը, կուղարկեր այն հասցեով, որ նշված է պատասխանում։ Որոշեք ինքներդ․ բանալին կամ թոքենը ուղարկեք միայն այն հոսթին, որի համար կառուցել եք հասցեն, իսկ երբ պատասխանը նոր հասցե է նշում, ստուգեք նրա սխեման, հոսթը և պորտը (և որ այն `https` է), նախքան նրան հարցում ուղարկելը, և մուտքի տվյալներով՝ կրկին միայն եթե դրանք նույնն են։ Սա SDK 3.7.0-ի կանոնն է․ `PluginHttpContract.AssertRedirectIsNotFollowedAsync`-ը ստուգում է այն պլագինի սեփական թեստերում։

### Տվյալների պահպանում կանչերի միջև {#keeping-data-between-calls}

`CreatePaymentAsync`, `GetStatusAsync` և `RefundAsync` առանձին կանչեր են, և վճարային համակարգին կարող է մի կանչից մյուսին ինչ-որ բան պետք գալ (պատվերի նույնականացուցիչ, կուրսոր)։ Պլագինի կոնստրուկտորում `IConfiguration`-ի կողքին վերցրեք `IPluginStore`՝

```csharp
public MyGateway(IConfiguration configuration, IPluginStore store) : base(configuration)
{
    _store = store;
}
```

Սերվերը տրամադրում է այն պլագինը ակտիվացնելիս։ Այն ընդհանուր է ամբողջ պլագինի համար. կապված է ձեր պլագինի `id`-ին և ոչ մի հաճախորդի, ունի նույն բանալու կանոններն ու չափի սահմանաչափերը, ինչ `call.Store`-ը, և հեռացվում է պլագինի տվյալների հետ, երբ պլագինը հեռացվում է և նրա տվյալները մաքրվում են։ Այն երբեք չի հասնում մեկ այլ պլագինի արժեքներին կամ որևէ հաճախորդի արժեքներին. մեկ հաճախորդի մասին տվյալները պատկանում են `call.Store`-ին, որը կապված է կանչի հաճախորդին։ Խանութը նույն տվյալներն է, ինչ հաճախորդից դուրս կատարվող ադմինի կանչի պահոցը։

### Օրինակ {#example}

::: code-group

```csharp [MyGateway.cs]
using Hostpanel.Sdk.Base;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Payment;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MyCompany.Hostpanel.Plugins.MyGateway;

public sealed class MyGateway : PaymentGatewayBase, IPaymentPlugin
{
    public MyGateway(string pluginId, IConfiguration configuration, ILogger<MyGateway> logger)
        : base(pluginId, configuration, logger) { }

    public string CurrencyCode => "840"; // USD (ISO 4217 numeric)

    public Task<PaymentSession> CreatePaymentAsync(
        PaymentInstruction instruction, CancellationToken ct)
    {
        var url = GetConfig("gateway_url")
            ?? throw new InvalidOperationException("gateway_url is required");
        // ... call the gateway API and return a redirect URL
        return Task.FromResult(new PaymentSession("order-123", url + "/pay?order=123"));
    }

    public Task<GatewayPaymentStatus> GetStatusAsync(
        string gatewayOrderId, CancellationToken ct)
    {
        // ... query the gateway
        return Task.FromResult(
            new GatewayPaymentStatus(GatewayPaymentState.Paid, gatewayOrderId, "ok", null, null));
    }

    public Task<string> RefundAsync(string gatewayOrderId, long amountMinor, CancellationToken ct)
    {
        // ... call the gateway refund endpoint
        return Task.FromResult("refund-ref-456");
    }
}
```

```json [plugin.json]
{
  "id": "my-gateway",
  "name": "My Gateway",
  "version": "1.0.0",
  "author": "Your Company",
  "description": "Accept payments through My Gateway.",
  "type": "Payment",
  "category": "Payment Gateways",
  "color": "#0369a1",
  "entryPoint": "MyCompany.Hostpanel.Plugins.MyGateway.MyGateway",
  "sdkVersion": "1.0",
  "showInIntegrations": true,
  "integrationsCategory": "Payment Gateways",
  "fields": [
    { "key": "gateway_url", "label": "Gateway URL", "type": "text",   "required": true  },
    { "key": "api_key",     "label": "API Key",     "type": "secret", "required": true  },
    { "key": "mode",        "label": "Mode",        "type": "select", "required": false }
  ]
}
```

```csharp [MyGatewayTests.cs]
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Hostpanel.Sdk.Plugins.Payment;
using Xunit;

public class MyGatewayTests
{
    [Fact]
    public async Task CreatePaymentAsync_ReturnsRedirectUrl()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["integration:my-gateway:gateway_url"] = "https://test.example.com",
                ["integration:my-gateway:api_key"]     = "test-key",
            })
            .Build();

        var gateway = new MyGateway("my-gateway", config, NullLogger<MyGateway>.Instance);
        var instruction = new PaymentInstruction(
            OrderNumber: "1001",
            AmountMinor: 5000,
            ReturnUrl:   "https://panel.example.com/payment/result",
            Description: "Invoice #1001",
            Language:    "en");

        var session = await gateway.CreatePaymentAsync(instruction, CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(session.RedirectUrl));
        Assert.False(string.IsNullOrEmpty(session.GatewayOrderId));
    }
}
```

:::

## Պրովիժնինգի պլագին {#a-provisioning-plugin}

Ժառանգեք `ProvisioningProviderBase`-ը և իրականացրեք `IProvisioningPlugin`-ը `Hostpanel.Sdk.Plugins.Provisioning`-ից։ Որպես հղումային իրականացում կարդացեք `backend/src/Innovayse.Providers.CWP7`-ը. այն ցույց է տալիս հոսթինգի վահանակի հաճախորդի ամբողջական պայմանագիրը և ինչպես մշակել պրովիժնինգի յուրաքանչյուր հրաման (ստեղծում, կասեցում, կասեցման վերացում, դադարեցում, փաթեթի փոփոխություն, գաղտնաբառի փոփոխություն, մուտք)։

## Մանիֆեստ (`plugin.json`) {#the-manifest-plugin-json}

Պլագինի յուրաքանչյուր ZIP-ի արմատում պետք է լինի `plugin.json`։

<SettingTable :settings="[
  { name: 'id', type: 'string', default: '', description: 'Եզակի նույնականացուցիչ՝ փոքրատառ լատինական տառեր, թվեր և գծիկներ, առավելագույնը 64 նիշ։ Պետք է անփոփոխ մնա տարբերակների միջև։' },
  { name: 'name', type: 'string', default: '', description: 'Անվանումը, որը ցուցադրվում է Պլագինների կառավարիչում և Ինտեգրացիաներում։' },
  { name: 'version', type: 'string', default: '', description: 'Իմաստային տարբերակի տող, օրինակ՝ 1.0.0։' },
  { name: 'author', type: 'string', default: '', description: 'Հեղինակի անունը, որը ցուցադրվում է պլագինի քարտի վրա։' },
  { name: 'description', type: 'string', default: '', description: 'Մեկ տողանոց նկարագրություն, որը ցուցադրվում է պլագինի քարտի վրա։' },
  { name: 'type', type: 'string', default: '', description: 'Պլագինի տեսակը՝ Payment, Provisioning, Registrar, Identity, Fulfilment, Hosted կամ App։' },
  { name: 'category', type: 'string', default: '', description: 'Կատեգորիա, որով պլագինը խմբավորվում է Պլագինների կառավարիչում։' },
  { name: 'color', type: 'string', default: '', description: 'Պլագինի պատկերակի ֆոնի hex գույնը, օրինակ՝ #0369a1։' },
  { name: 'icon', type: 'string', default: '', description: 'Պլագինի ոչ պարտադիր պատկերը՝ փաթեթի ներսի ուղի դեպի SVG կամ PNG ֆայլ, օրինակ՝ icon.svg կամ assets/icon.png։ Փոքրատառ լատինական տառեր, թվեր, կետ, գծիկ և ընդգծման նշան, առաջ թեքվող շեղ գծեր, առանց սկզբի շեղ գծի, առանց .. հատվածի, ընդլայնումը .svg կամ .png, առավելագույնը 256 ԿիԲ։ Բաց թողեք, որ ցուցադրվի գունավոր բլոկը պլագինի սկզբնատառով։' },
  { name: 'entryPoint', type: 'string', default: '', description: 'Պլագինի ինտերֆեյսն իրականացնող դասի լրիվ անունը։' },
  { name: 'sdkVersion', type: 'string', default: '', description: 'Մանիֆեստի SDK տարբերակը։ Ընդունվող արժեքը 1.0 է։ Assembly-ի SDK մաժորը ստուգվում է առանձին, և ավելի հին մաժորները մերժվում են։' },
  { name: 'showInIntegrations', type: 'boolean', default: 'false', description: 'Երբ true է, պլագինը երևում է որպես քարտ Ինտեգրացիաներ էկրանին։' },
  { name: 'integrationsCategory', type: 'string', default: '', description: 'Կատեգորիայի վերնագիրը, որի տակ քարտը երևում է Ինտեգրացիաներ էկրանին։' },
  { name: 'fields', type: 'array', default: '[]', description: 'Ինտեգրացիայի էջում ցուցադրվող կարգավորումները՝ ըստ հերթականության։' }
]" />

`icon` ֆայլը ստուգվում է պլագինը տեղադրելիս և կրկին՝ բեռնելիս։ `PLUGIN_ICON_INVALID` սխալով մերժվում են՝ փաթեթից դուրս գնացող ուղին, չափի սահմանաչափը գերազանցող ֆայլը, PNG չհանդիսացող `.png`-ը և այն SVG-ն, որը լավ կազմված XML չէ կամ պարունակում է սկրիպտ, `foreignObject`, իրադարձության մշակիչի ատրիբուտ (`onload`, `onclick` և այլն), DOCTYPE կամ պատկերից դուրս գնացող հղում (`#fragment` չհանդիսացող `href`, `#fragment` չհանդիսացող `url(...)`, `@import`)։ Բացակայող ֆայլի դեպքում սխալը `PLUGIN_ICON_MISSING` է։ Ադմինի վահանակը պատկերը ցուցադրում է այնտեղ, որտեղ ցուցադրվում է գունավոր բլոկը. սերվերը մատուցում է այն `GET /api/admin/plugins/{id}/icon` հասցեից՝ `X-Content-Type-Options: nosniff` վերնագրով և քաղաքականությամբ, որը արգելափակում է սկրիպտը և ցանկացած բեռնում։

Վերևի դաշտերը վերաբերում են մանիֆեստի 1-ին տարբերակին։ `manifestVersion` չունեցող մանիֆեստը 1-ին տարբերակն է. սերվերն ընդունում է `1` և `2` և մերժում է ցանկացած այլ արժեք (`PLUGIN_MANIFEST_VERSION_UNKNOWN`)։ `version`-ը պետք է լինի խիստ `major.minor.patch` տարբերակ։ Մանիֆեստի 2-րդ տարբերակը (`"manifestVersion": 2`) ավելացնում է ստորև նշված անդամները։

### Մանիֆեստի v2 անդամներ {#manifest-v2-members}

<SettingTable :settings="[
  { name: 'manifestVersion', type: 'number', default: '1', description: 'Սահմանեք 2՝ ստորև նշված անդամներն օգտագործելու համար։' },
  { name: 'settings', type: 'array', default: '[]', description: 'Տիպավորված կարգավորումներ, որոնք լրացնում է ադմինիստրատորը։ Փոխարինում է fields-ին. երբ settings-ը դատարկ է, սերվերը fields-ը վերածում է settings-ի։' },
  { name: 'capabilities', type: 'string[]', default: '[]', description: 'Ինչ կարող է անել պլագինը, ցուցադրվում է առանց ակտիվացնելու։ Հայտնի անուններ՝ storedCredentials (վճարային պլագին, որը նաև իրականացնում է IStoredCredentialPlugin), paymentReturn (վճարային պլագին, որը նաև իրականացնում է IPaymentReturnPlugin, SDK 3.7.0 կամ ավելի նոր) և endpoints (պլագինի դասն իրականացնում է IPluginEndpoints. App չհանդիսացող պլագինի համար սերվերը մատուցում է միայն նրա հանրային հետկանչերը, տես Արտաքին հետկանչեր. App պլագինը մատուցվում է առանց այն հայտարարելու)։ Պետք է համընկնի այն ինտերֆեյսների հետ, որոնք դասը իրոք իրականացնում է։' },
  { name: 'hostAccess', type: 'string[]', default: '[]', description: 'Հաճախորդի հոսթինգին հասանելիության տեսակներ, որոնք պլագինը խնդրում է սերվերից։ Այսօր միակ տեսակը ssh է։ Ադմինի վահանակի Պլագիններ էջը ցույց է տալիս պլագինի հայտարարած յուրաքանչյուրը։' },
  { name: 'locales', type: 'object', default: '{}', description: 'Տեքստեր՝ ըստ լեզվի կոդի, ապա ըստ բանալու, օրինակ՝ en լեզուն menu.chat բանալիով և Chat տեքստով։ Տեքստի բանալին ունի առնվազն երկու կետով բաժանված մաս, օրինակ՝ menu.chat։ en տեքստերը պետք է սահմանեն մանիֆեստում օգտագործվող բոլոր բանալիները։' },
  { name: 'api', type: 'object', default: '{}', description: 'Երթուղիներ, որոնք սերվերը փոխանցում է պլագինին՝ api.client և api.admin, յուրաքանչյուրը երթուղիների ցանկ։' },
  { name: 'ui', type: 'object', default: '{}', description: 'Ինչ է պլագինը ավելացնում վահանակներին՝ ui.client և ui.admin (մենյուներ և էջեր) և ui.slots (վիջեթներ սերվերի սլոտներում)։' },
  { name: 'tags', type: 'string[]', default: '[]', description: 'Ոչ պարտադիր հիմնաբառեր, որոնցով պլագինների ռեեստրը օգնում է մարդկանց գտնել պլագինը։ Պահանջում է SDK 3.3.0 կամ ավելի նոր։ Առավելագույնը 10 պիտակ, յուրաքանչյուրը 2-ից 30 փոքրատառ լատինական տառ, թիվ և գծիկ, առանց սկզբի #, և ոչ մի պիտակ երկու անգամ։ Սխալ ցանկը մերժվում է PLUGIN_MANIFEST_TAGS_INVALID սխալով։' },
  { name: 'externalOrigins', type: 'string[]', default: '[]', description: 'Ոչ պարտադիր։ Վեբ origin-ներ (սխեմա, հոսթ և պորտ, օրինակ՝ https://example.com կամ https://*.example.com), որոնց կարող է ուղղել պլագինի տեսքի link բլոկը բացարձակ https հասցեով և որոնք կարող է շրջանակել preview բլոկը (միայն զուտ https origin)։ Բաց թողեք, երբ պլագինը հղվում է միայն տեղադրման վրա տեղակայված կայքերին. տես ստորև՝ ընթերցողի սեփական կայքերի հղումների բաժինը։' }
]" />

Պիտակները պարզ հիմնաբառեր են և մանիֆեստում գրվում են այսպես՝

```json
"tags": ["billing", "card-payments", "invoices"]
```

Սերվերը ստուգում է ցանկը պլագինը տեղադրելիս և բեռնելիս՝ նույն կանոնով, որը `Hostpanel.Sdk.Packaging` փաթեթը կիրառում է ձեր կառուցման վրա։ `PLUGIN_MANIFEST_TAGS_INVALID` սխալով մերժվում են՝ 2 նիշից կարճ կամ 30 նիշից երկար պիտակը, մեծատառ, բացատ, սկզբի `#` կամ տառերից, թվերից ու գծիկներից դուրս ցանկացած նիշ պարունակող պիտակը, երկու անգամ նշված պիտակը, ինչպես նաև 10-ից ավելի պիտակները։ `tags` չունեցող մանիֆեստը ընդունելի է։

### Աջակցվող հոսթի տարբերակներ և փոփոխությունների ցանկ {#supported-host-versions-and-changelog}

Այս ոչ պարտադիր դաշտերը թույլատրված են մանիֆեստի ցանկացած տարբերակում։ Պահանջում է SDK 3.4.0 կամ ավելի նոր։

<SettingTable :settings="[
  { name: 'minHostVersion', type: 'string', default: '', description: 'Hostpanel-ի ամենացածր տարբերակը, որի հետ պլագինն աշխատում է, խիստ major.minor.patch ձևով, օրինակ՝ 2.1.0։ Բաց թողեք, եթե ներքևի սահման չկա։' },
  { name: 'maxHostVersion', type: 'string', default: '', description: 'Hostpanel-ի ամենաբարձր տարբերակը, որի հետ պլագինը հաստատ աշխատում է, նույն ձևով և ոչ ցածր minHostVersion-ից։ Բաց թողեք, եթե վերևի սահման չկա։' },
  { name: 'changelog', type: 'string', default: '', description: 'Այս տարբերակի փոփոխությունների կարճ ցանկ՝ Markdown-ով, առավելագույնը 4000 նիշ։ Տողադարձերը պահպանվում են։' }
]" />

```json
"minHostVersion": "2.1.0",
"maxHostVersion": "2.9.0",
"changelog": "Adds refunds.
Fixes the renewal date."
```

Երկու սահմաններն էլ ներառական են և համեմատվում են իմաստային տարբերակների կարգով, ուստի նախնական տարբերակը, օրինակ՝ `2.1.0-rc.1`, ցածր է `2.1.0`-ից։ Վավեր տարբերակ չհանդիսացող սահմանը մերժվում է `PLUGIN_MANIFEST_HOST_VERSION_INVALID` սխալով, `maxHostVersion`-ից բարձր `minHostVersion`-ը՝ `PLUGIN_MANIFEST_HOST_VERSION_RANGE_INVERTED`-ով, իսկ ավելի երկար ցանկը՝ `PLUGIN_MANIFEST_CHANGELOG_TOO_LONG`-ով։

Երբ ադմինիստրատորը փաթեթ է վերբեռնում, ադմինի վահանակը նախ ցույց է տալիս, թե ինչ է այն պահանջում (յուրաքանչյուր `hostAccess` տեսակ և յուրաքանչյուր հնարավորություն՝ բառերով), փոփոխությունների ցանկը և աջակցվող տարբերակները, և տեղադրում է միայն հաստատումից հետո։ Հոսթը միջակայքը համեմատում է իր տարբերակի հետ տեղադրման ցանկացած ճանապարհին՝ վերբեռնում, ռեեստրից տեղադրում և տեղադրված պլագինի թարմացում։ Միջակայքից դուրս տեղադրումը մերժվում է `PLUGIN_HOST_VERSION_OUT_OF_RANGE` (409) սխալով, և ոչինչ չի բացվում կամ հերթագրվում։

Այնտեղ, որտեղ պիտակը, վերնագիրը կամ նկարագրությունը կարող է տեքստ լինել, գրեք կամ `locales`-ի բանալի, կամ պարզ տեքստ։ Բանալու տեսք ունեցող արժեքը (տառեր, թվեր, գծիկներ և ընդգծման նշաններ՝ կետերով բաժանված երկու կամ ավելի մասերով) փնտրվում է `en` տեքստերում և մերժվում է, եթե սահմանված չէ (`PLUGIN_LABEL_KEY_MISSING`)։

`settings`-ի յուրաքանչյուր գրառում՝

<SettingTable :settings="[
  { name: 'key', type: 'string', default: '', description: 'Կարգավորման բանալին, որը կարդացվում է GetConfig(key)-ով։' },
  { name: 'label', type: 'string', default: '', description: 'Կարգավորման համար ցուցադրվող տեքստը։' },
  { name: 'type', type: 'string', default: '', description: 'Մուտքագրման տեսակը՝ Text, Url, Select, Secret, Number, Boolean կամ Textarea։ Secret-ը պահվում է գաղտնագրված և ընթերցման ժամանակ երբեք չի վերադարձվում։' },
  { name: 'required', type: 'boolean', default: 'false', description: 'Երբ true է, կարգավորումը պետք է լրացվի, որպեսզի պլագինը հնարավոր լինի միացնել։' },
  { name: 'description', type: 'string', default: '', description: 'Օգնության տեքստը, որը ցուցադրվում է կարգավորման տակ։' },
  { name: 'default', type: 'string', default: '', description: 'Արժեք, որն օգտագործվում է, քանի դեռ ադմինիստրատորը չի սահմանել իրենը։ Միշտ գրվում է որպես տեքստ, օրինակ՝ 30 կամ true։' },
  { name: 'placeholder', type: 'string', default: '', description: 'Դատարկ դաշտում ցուցադրվող հուշումը։' },
  { name: 'min', type: 'number', default: '', description: 'Number կարգավորման ամենափոքր արժեքը։' },
  { name: 'max', type: 'number', default: '', description: 'Number կարգավորման ամենամեծ արժեքը։' },
  { name: 'options', type: 'array', default: '[]', description: 'Select կարգավորման ընտրանքները, յուրաքանչյուրը օբյեկտ՝ value (պահվող) և label (ցուցադրվող) դաշտերով։' }
]" />

`api.client`-ի և `api.admin`-ի յուրաքանչյուր գրառում երթուղի է, որը սերվերը մատուցում է `/api/client/plugins/{id}/...` և `/api/admin/plugins/{id}/ext/...` հասցեների տակ։ Սերվերը ստուգում է մուտքը, կանչողի իրավասությունը, CSRF-ը և հարցումների սահմանափակումը՝ նախքան պլագինը կաշխատի։

<SettingTable :settings="[
  { name: 'method', type: 'string', default: '', description: 'Get, Post, Put, Patch կամ Delete։' },
  { name: 'path', type: 'string', default: '', description: 'Ուղին պլագինի նախածանցից ներքև՝ սկզբում շեղ գիծ, ապա փոքրատառ լատինական տառեր, թվեր, գծիկներ և ընդգծման նշաններ կամ {parameter} հատվածներ, օրինակ՝ /invoices/{id}։ Առանց դատարկ հատվածի և առանց .. հատվածի։ Նույն մեթոդն ու ուղին կարելի է հայտարարել մեկ անգամ յուրաքանչյուր տարածքի համար։' },
  { name: 'permission', type: 'string', default: '', description: 'Միայն հաճախորդի երթուղիների համար՝ հաճախորդի իրավասության անունը, կամ Any՝ ցանկացած մուտք գործած կանչողի համար։ Ադմինի երթուղին իրավասություն չի հայտարարում, քանի որ ադմինի տարածքը պաշտպանված է միայն ադմինի քաղաքականությամբ (PLUGIN_ADMIN_PERMISSION_DECLARED)։' },
  { name: 'rateLimit', type: 'string', default: 'Default', description: 'Հարցումների սահմանափակման քաղաքականությունը՝ Default, Strict (թանկ կամ զգայուն երթուղիների համար, օրինակ՝ հաղորդագրություններ ուղարկելը) կամ Heavy (հազվադեպ, ծանր աշխատանքի համար, օրինակ՝ արտահանումը)։' },
  { name: 'streaming', type: 'boolean', default: 'false', description: 'Երբ true է, երթուղին պատասխանում է server-sent events հոսքով՝ մեկ պատասխանի փոխարեն։ Հոսքային երթուղին նաև ենթարկվում է միաժամանակյա կապերի սահմանաչափի։' }
]" />

`ui.client`-ը և `ui.admin`-ը յուրաքանչյուրը պարունակում են `menus` և `pages`։ Յուրաքանչյուր էջ՝

<SettingTable :settings="[
  { name: 'id', type: 'string', default: '', description: 'Էջի նույնականացուցիչը, եզակի յուրաքանչյուր տարածքում՝ 1-ից 64 փոքրատառ լատինական տառ, թիվ, գծիկ և ընդգծման նշան, սկսվում է տառով կամ թվով։' },
  { name: 'path', type: 'string', default: '', description: 'Էջի ուղին վահանակում՝ պլագինի նախածանցից ներքև, նույն կանոններով, ինչ api ուղին։' },
  { name: 'title', type: 'string', default: '', description: 'Էջի վերնագիրը։' },
  { name: 'view', type: 'string', default: '', description: 'Փաթեթի ներսի ուղի դեպի տեսքի փաստաթուղթը, որը նկարագրում է էջը։ Սերվերը ցուցադրում է այն իր սեփական բաղադրիչներով, ուստի բրաուզերում պլագինի ոչ մի կոդ չի աշխատում։' },
  { name: 'permission', type: 'string', default: '', description: 'Միայն հաճախորդի էջերի համար՝ հաճախորդի իրավասության անունը կամ Any։ Ադմինի էջի վրա չի թույլատրվում։' }
]" />

Մենյուի յուրաքանչյուր գրառում՝

<SettingTable :settings="[
  { name: 'id', type: 'string', default: '', description: 'Գրառման նույնականացուցիչը, եզակի պլագինի ներսում։' },
  { name: 'label', type: 'string', default: '', description: 'Մենյուում ցուցադրվող տեքստը։' },
  { name: 'page', type: 'string', default: '', description: 'Նույն տարածքի այն էջի id-ն, որը բացում է գրառումը։' },
  { name: 'icon', type: 'string', default: '', description: 'Պատկերակի անունը՝ 1-ից 64 փոքրատառ լատինական տառ, թիվ և գծիկ։ Բաց թողեք վահանակի լռելյայն պատկերակի համար։' },
  { name: 'order', type: 'number', default: '0', description: 'Դիրքը մենյուում, ամենափոքրը՝ առաջինը։' }
]" />

`ui.slots`-ի յուրաքանչյուր գրառում վիջեթ է դնում սերվերի առաջարկած սլոտում։ Սլոտների ցանկը տարբերակավորված է. սերվերն առաջարկում է `client.dashboard.cards` (առանց կոնտեքստի), `client.service.tabs` (կոնտեքստի բանալի՝ `serviceId`), `admin.client.panels` (`clientId`) և `admin.service.panels` (`serviceId` և `clientId`)։ Ցանկում չեղած սլոտը կամ սլոտի չառաջարկած կոնտեքստի բանալին մերժվում է։

<SettingTable :settings="[
  { name: 'id', type: 'string', default: '', description: 'Վիջեթի նույնականացուցիչը, եզակի սլոտի ներսում՝ փոքրատառ լատինական տառեր, թվեր, գծիկներ և ընդգծման նշաններ։' },
  { name: 'slot', type: 'string', default: '', description: 'Սլոտի id-ն, վերևի չորսից մեկը։' },
  { name: 'view', type: 'string', default: '', description: 'Փաթեթի ներսի ուղի դեպի տեսքի փաստաթուղթը, որը նկարագրում է վիջեթը։' },
  { name: 'title', type: 'string', default: '', description: 'Վիջեթի հետ ցուցադրվող վերնագիրը։' },
  { name: 'order', type: 'number', default: '0', description: 'Դիրքը սլոտի վիջեթների մեջ, ամենափոքրը՝ առաջինը։' },
  { name: 'permission', type: 'string', default: '', description: 'Միայն հաճախորդի սլոտների համար՝ հաճախորդի իրավասության անունը կամ Any։' },
  { name: 'productIds', type: 'number[]', default: '[]', description: 'Ապրանքներ, որոնց կապված է վիջեթը. այն երևում է միայն այնտեղ, որտեղ սլոտի ծառայությունը պատկանում է դրանցից մեկին։ Դատարկ արժեքի դեպքում վիջեթը երևում է բոլոր ապրանքների համար։ Ադմինիստրատորը կարող է փոխել կապը Պլագիններ էջում։' },
  { name: 'contextKeys', type: 'string[]', default: '[]', description: 'Կոնտեքստ, որն անհրաժեշտ է վիջեթին սլոտից։ Սերվերը ստուգում է բանալիները կանչողի համար և պլագինին փոխանցում է միայն դրանք։' }
]" />

### Արտաքին հետկանչեր {#callbacks-from-outside}

Վճարային համակարգը դիմում է վահանակի սերվերին սերվերից սերվեր, առանց մուտք գործած օգտատիրոջ՝ քարտի webhook, նախնական ստուգում և հաստատում, բանկի արդյունքի URL։ `IPluginEndpoints`-ն իրականացնող պլագինը կարող է ստանալ դրանք հանրային դռնից՝ առանց վճարային համակարգի կոդը սերվերում պահելու։ Սա բաց է ցանկացած տեսակի պլագինի համար. վճարային պլագինը իր մանիֆեստում նշում է `"capabilities": ["endpoints"]`, և նրա դասը `IPaymentPlugin`-ի կողքին իրականացնում է `IPluginEndpoints`. App պլագինին capability պետք չէ։ Capability-ն բացում է միայն այս դուռը։ App չհանդիսացող պլագինի `api.client` և `api.admin` երթուղիները, էջերը, մենյուներն ու սլոտները չեն մատուցվում՝ անկախ այն բանից, թե ինչ է նա հայտարարել։ Մանիֆեստը, որը նշում է `endpoints`, բայց նրա դասը չի իրականացնում `IPluginEndpoints`, բեռնելիս մերժվում է `PLUGIN_ENDPOINTS_NOT_IMPLEMENTED` սխալով, իսկ այն պլագինը, որը դա չի նշել, իր հետկանչերին պատասխանում է նույն `404 PLUGIN_ROUTE_NOT_FOUND`-ով, ինչ անհայտ պլագինը։

Յուրաքանչյուր հետկանչ հայտարարեք `api.callbacks`-ում՝ երթուղիների ցանկում, որտեղ `method`-ի և `path`-ի կանոնները նույնն են, ինչ `api.client`-ում (`permission` թույլատրված չէ, `streaming`-ը նույնպես. `rateLimit`-ը անտեսվում է, քանի որ դուռն ունի իր սեփական սահմանաչափը)՝

```json
{
  "api": {
    "callbacks": [
      { "method": "Post", "path": "/webhook" },
      { "method": "Post", "path": "/confirm/{step}" }
    ]
  }
}
```

Սերվերը մատուցում է դրանք `/api/plugins/{id}/callback/...` հասցեի տակ, ուստի վերևի երկրորդ երթուղին `POST /api/plugins/{id}/callback/confirm/result` է։ Այդ հասցեն տվեք վճարային համակարգին։

Ինչ է անում սերվերը՝

- Պատասխանում է միայն տեղադրված և միացված պլագինի հայտարարված հետկանչին։ Անհայտ պլագինը, անջատվածը կամ չկարգավորվածը, չհայտարարված ուղին և սխալ մեթոդը բոլորն ստանում են նույն `404 PLUGIN_ROUTE_NOT_FOUND` պատասխանը, ուստի դուռը երբեք չի ցույց տալիս, թե ինչ է տեղադրված։ `api.client` և `api.admin` երթուղիները դրա միջով հասանելի չեն։
- Մուտք չկա և CSRF վերնագիր չկա։ Պլագինը ստանում է `call.Area == PluginArea.Callback`, առանց հաճախորդի հղման, առանց իրավասությունների, կանչողի ֆիքսված հղում `callback` և պլագինի ընդհանուր խանութը։
- Մարմինը փոխանցվում է բայթ առ բայթ, երբեք չի վերլուծվում և նորից չի սերիալիզացվում, ուստի դրա վրայի ստորագրությունը շարունակում է ստուգվել։ `call.Headers`-ը պարունակում է հարցման վերնագրերի թույլատրված ցանկը ճիշտ այնպես, ինչպես ստացվել են (տես ստորև), իսկ `call.Query`-ն՝ հարցման տողը։
- Հետկանչը կարող է պատասխանել `PluginReply.Raw(status, body, contentType)`-ով՝ այն վճարային համակարգերի համար, որոնք սպասում են պարզ պատասխանի, օրինակ՝ `OK` որպես `text/plain`. բայթերը, կարգավիճակը և media type-ը գրվում են այնպես, ինչպես տրվել են, `X-Content-Type-Options: nosniff` վերնագրով։ Raw պատասխանն ընդունվում է միայն հետկանչի վրա (հաճախորդի կամ ադմինի երթուղու վրա դա պլագինի սխալ է՝ `PLUGIN_FAILED`), նրա media type-ը պետք է լինի `text/plain`, `application/json`, `application/xml`, `application/x-www-form-urlencoded` կամ `text/html` (մնացած ամեն ինչ, ներառյալ սկրիպտը, `PLUGIN_FAILED` է), և նրա մարմինը սահմանափակվում է `PluginApi__MaxBodyBytes`-ով։ Յուրաքանչյուր raw պատասխան, որը HTML չէ, նաև կրում է `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` վերնագիրը։
- Հետկանչը կարող է պատասխանել HTML էջով՝ `PluginReply.Raw(200, bytes, "text/html; charset=utf-8")`-ով, որպեսզի վճարային համակարգը կարողանա ցույց տալ իր վճարման էջը (օրինակ՝ քայլ բրաուզերում)։ Էջը տրվում է խիստ քաղաքականությամբ, որը պլագինը չի կարող փոխել․ ոչինչ չի բեռնվում, չի միանում, ձև չի ուղարկում և շրջանակ չի բացում, բացի իր մանիֆեստի `externalOrigins`-ում նշված ճշգրիտ `https` աղբյուրներից (wildcard-ը և `http` աղբյուրը ոչինչ չեն նշում), էջը չի կարելի ներդնել շրջանակում, այն չունի բազային հասցե և աշխատում է sandbox-ում (սկրիպտներն ու ձևերը թույլատրված են, բայց առանց վահանակի origin-ի, ուստի այն չի հասնում վահանակի սեսիային ու պահոցին)։ Այն երբեք չի քեշավորվում և referrer չի ուղարկում, որովհետև նրա հասցեն կարող է կրել վճարման թոքեն։ Ներդրված սկրիպտն ու ոճը կատարվում են․ բեռնվող սկրիպտը պետք է գա թվարկված աղբյուրից։
- `call.Headers`-ին հասնում են հարցման միայն այս վերնագրերը՝ `Content-Type`, `Content-Length`, `User-Agent`, `Signature`, `Digest`, `Content-Digest`, `Date`, `Idempotency-Key`, `Webhook-Id`, `Webhook-Timestamp`, `Webhook-Signature`, և ցանկացած `X-*` վերնագիր, բացի `X-Forwarded-*`-ից, `X-Real-IP`-ից և սերվերի սեփականներից (`X-Language`, `X-Requested-With`, `X-Logout-Key`, `X-Service-Key`, `X-Renewed-Access-Token`)։ `Cookie`, `Authorization`, `Proxy-Authorization` և `Host` երբեք չեն փոխանցվում։ Եթե ձեր վճարային համակարգը ստորագրում է այս ցանկում չեղած վերնագիր, ուղարկեք այն որպես `X-*` վերնագիր կամ դրեք մարմնի մեջ։
- Մարմինը սահմանափակվում է `PluginApi__MaxBodyBytes`-ով (դրանից ավելին՝ 413 `PLUGIN_REQUEST_TOO_LARGE`), և դռան ամեն հարցում հաշվվում է ըստ կանչողի հասցեի `RateLimit__PluginCallback`-ի դեմ (դրանից ավելին՝ 429)։
- Սխալը գրանցվում է միայն պլագինի id-ով, երբեք վերնագրով, հարցման տողով կամ մարմնով։

Ինչ պետք է անի պլագինը՝ ինքը ստուգի կանչը (վճարային համակարգի ստորագրությունը, վերահսկիչ գումարը կամ ընդհանուր գաղտնիքը), նախքան դրա մեջ որևէ բանի վստահելը, օգտագործելով սեփական կարգավորումների գաղտնիքը։ Չստուգվող կանչին պատասխանեք `PluginReply.Problem(400, ...)`-ով կամ `403`-ով, իսկ ստուգվողին՝ հաջողության պատասխանով. գործը պահեք իդեմպոտենտ, քանի որ վճարային համակարգերը նույն իրադարձությունը առաքում են մեկից ավելի անգամ։ Սերվերում ոչինչ չի ասում, որ կանչը իսկական է։

Այս կանոններից որևէ մեկը խախտող մանիֆեստը մերժվում է տեղադրման և գործարկման ժամանակ `PLUGIN_*` մանիֆեստի սխալի կոդով, որը նշում է խախտող անդամը (`PLUGIN_CALLBACK_INVALID`՝ իրավասություն հայտարարող կամ հոսքային հետկանչի համար, `PLUGIN_ROUTE_PATH_INVALID` և `PLUGIN_ROUTE_DUPLICATE`՝ ուղու և կրկնությունների համար)։ Ճշգրիտ տեսակների համար կարդացեք `backend/src/Hostpanel.Sdk/Plugins/Manifest`-ը։

### Capabilities {#capabilities}

2-րդ տարբերակի մանիֆեստը կարող է թվարկել `capabilities`, յուրաքանչյուրը SDK-ի `PluginCapabilities`-ից մի անուն՝

| Capability | Թույլատրվում է | Պլագինի դասը նաև պետք է իրականացնի |
|------------|------------|--------------------------------------|
| `storedCredentials` | միայն `Payment` | `IStoredCredentialPlugin` |
| `endpoints` | ցանկացած տեսակ | `IPluginEndpoints` |

`App` պլագինը պատասխանում է երթուղիներին և ցուցադրում է էջեր, մենյուներ ու սլոտներ՝ առանց ինչ-որ բան հայտարարելու։ Ցանկացած այլ տեսակ, որը հայտարարում է
`api` երթուղիներ կամ `ui` մենյուներ, էջեր կամ սլոտներ, պետք է հայտարարի `endpoints`, այլապես փաթեթը մերժվում է տեղադրման ժամանակ և բաց է թողնվում
գործարկման ժամանակ (`PLUGIN_ENDPOINTS_NOT_DECLARED`)։ Այսպես է մեկ պլագինը, օրինակ՝ կրեդիտներ վաճառող `Fulfilment` պլագինը,
նաև հաճախորդին տալիս էկրան։ Անհայտ անունը, երկու անգամ հայտարարված անունը կամ `Payment`-ից տարբեր տեսակի վրա `storedCredentials`-ը
մերժվում է նույն կերպ (`PLUGIN_CAPABILITY_NOT_ALLOWED`)։

Հայտարարված capability-ն ստուգվում է դասի դեմ պլագինը ակտիվացնելիս։ Եթե դասը չի իրականացնում
capability-ի խոստացած ինտերֆեյսը, պլագինը ոչ մի բանի համար չի ակտիվանում, ներառյալ իր սեփական տեսակի կանչերը. վճարային
պլագինը, որը հայտարարում է `storedCredentials`՝ առանց `IStoredCredentialPlugin`-ը իրականացնելու, վճարումներ չի ընդունում, իսկ
fulfilment պլագինը, որը հայտարարում է `endpoints`՝ առանց `IPluginEndpoints`-ը իրականացնելու, fulfilment կանչեր չի ստանում։
Ադմինի մատյանը նշում է պլագինը և capability-ն։

`fields`-ի յուրաքանչյուր գրառում՝

<SettingTable :settings="[
  { name: 'key', type: 'string', default: '', description: 'Կարգավորման բանալին, որը կարդացվում է GetConfig(key)-ով։' },
  { name: 'label', type: 'string', default: '', description: 'Ինտեգրացիայի էջում դաշտի կողքին ցուցադրվող պիտակը։' },
  { name: 'type', type: 'string', default: 'text', description: 'Դաշտի տեսակը՝ text, secret, select կամ textarea։' },
  { name: 'required', type: 'boolean', default: 'false', description: 'Երբ true է, ինտեգրացիայի էջը ցույց է տալիս Կարգավորված չէ, քանի դեռ այս դաշտը չի լրացվել։' }
]" />

## Բլոկների դասավորությունը {#layout-of-blocks}

Պանելը բլոկները դասավորում է ըստ տեսակի, view-ում ոչինչ սահմանել պետք չէ։ Իրար հաջորդող `stat` բլոկները կազմում են սալիկների մեկ ցանց, իսկ `format: "badge"` ունեցող `stat`-ը արժեքը ցույց է տալիս որպես բեյջ։ Իրար հաջորդող `link` բլոկները կազմում են եզրագծով կոճակների մեկ փաթաթվող շարք, իսկ պանելից դուրս տանող հղումը նշվում է արտաքին հղման պատկերակով։ `header` բլոկը պլագինի էջում էջի վերնագիրն է, իսկ հոսթ էջի վիջեթի ներսում դառնում է բաժնի կոմպակտ վերնագիր։ `tabs` բլոկի ներդիրների շարքը ոլորվում է կողքերով, եթե ներդիրները չեն տեղավորվում։

## Տեսքի վիճակ {#view-state}

Էջի կամ սլոտի տեսքը (`{ "schema": 1, "blocks": [...] }`) կարող է արձագանքել իր տվյալներին՝ առանց էջը նորից բեռնելու։
Դա անում են երեք ոչ պարտադիր անդամ. բոլորը պահանջում են SDK 2.2.0 կամ ավելի նոր, և տեսքի սխեման մնում է 1։

**Պայմանը** մեկ ձև է, օգտագործվում է `visibleWhen`-ում և `followUp.until`-ում, ճիշտ այս երեք անդամներով՝

```json
{ "route": { "method": "get", "path": "/services/{serviceId}/site" }, "field": "site.status", "equals": ["ready", "live"] }
```

- `route`-ը `GET` երթուղի է, որը ձեր մանիֆեստը հայտարարում է էջի կամ սլոտի նույն տարածքում։
- `field`-ը կետերով ուղի է JSON պատասխանի ներսում (յուրաքանչյուր հատվածում տառեր, թվեր, `_` և `-`, առավելագույնը 200 նիշ)։
- `equals`-ը մեկ JSON արժեք է (տող, թիվ, `true`, `false`, `null`) կամ դրանցից 1-ից 20 արժեքի ցանկ։ Պայմանը կատարվում է,
  երբ դաշտը խիստ հավասար է դրանցից մեկին՝ `"1"`-ը չի համընկնում `1`-ի հետ։

**`visibleWhen`**-ը դրվում է ցանկացած բլոկի վրա, ներառյալ վերնագրի գործողությունները և ներդիրների ներսի բլոկները։ Բլոկը ցուցադրվում է միայն այն դեպքում,
երբ պայմանը կատարվում է. քանի դեռ այն կարդացվում է, երբ ընթերցումը ձախողվում է կամ երբ դաշտը բացակայում է, բլոկը մնում է թաքնված։ Այն նորից կարդացվում է,
երբ թարմացվում է բլոկի սեփական `id`-ն։ Թաքցրեք ստեղծման ձևը, երբ կայքն արդեն կա՝

```json
{
  "kind": "form",
  "id": "create-site",
  "visibleWhen": { "route": { "method": "get", "path": "/services/{serviceId}/site" }, "field": "exists", "equals": false },
  "fields": [
    { "key": "name", "label": "site.name", "type": "text", "required": true },
    { "key": "description", "label": "site.description", "type": "textarea" }
  ],
  "submit": {
    "route": { "method": "post", "path": "/services/{serviceId}/site" },
    "label": "site.create",
    "refresh": ["create-site", "site"],
    "resetOnSuccess": true
  }
}
```

**Որքան հաճախ է երթուղին կարդացվում։** Էջը ձեր տվյալների մեկ պատկեր է։ Էջի վրա նույն
`GET` երթուղին նշող յուրաքանչյուր բլոկ, պայման և քարտ սպասարկվում է մեկ հարցումով, և վահանակը նորը ուղարկում է միայն այն դեպքում, երբ էջը թարմացվում է (դա պահանջել է
գործողությունը, ձևը, զրույցի քայլը կամ հետագա ստուգումը) այն բանից հետո, երբ ընթերցում չհանդիսացող ցանկացած կանչ ավարտվել է, կամ երբ
զրույցի քայլը սկսվում կամ ավարտվում է։ Ավելի ուշ հայտնվող բլոկը վերցնում է այն պատասխանը, որը էջն արդեն ունի, ուստի մեկ վիճակի երթուղի տասը տեղում նշող էջը
արժե մեկ հարցում թարմացման համար, ոչ թե տասը։ Վահանակը ընթերցում չի պահում այդ պահից հետո, ոչ տասը վայրկյանից հին և ոչ մեկ այլ էջից։ Մի քանի բլոկի կողմից կարդացվող երթուղին
ամեն թարմացման համար դեռ կարդացվում է մեկ անգամ, ուստի այն պահեք էժան պատասխանելու համար։

**`resetOnSuccess`**-ը `true` է կամ `false`, միայն ձևի `submit`-ի ներսում։ Հաջող ուղարկումից հետո յուրաքանչյուր դաշտ
դատարկվում է։

**`followUp`**-ը դրվում է գործողության բլոկի վրա կամ ձևի `submit`-ի ներսում, ճիշտ `refresh` և `until` անդամներով։ Հաջող կանչից հետո
(և նրա սովորական `refresh`-ից հետո) վահանակը նորից ու նորից կարդում է `until.route`-ը՝ ամեն անգամ թարմացնելով թվարկված բլոկները,
մինչև պայմանը կատարվի (այդ դեպքում ևս մեկ անգամ), փորձերը սպառվեն կամ ընթերցողը լքի էջը։ Ձախողված
ընթերցումը հաշվվում է որպես փորձ։ Դիտարկումը ավելի երկար է ապրում, քան այն սկսած կոճակը, ուստի այն կոճակը, որը թաքցնում է իրեն, երբ իր աշխատանքը
ավարտվել է (նույն տվյալների վրա `visibleWhen`), դիտարկումը չի կանգնեցնում։ Այն նախադասությունը, որով պատասխանել էր գործողությունը, հանվում է, երբ
պայմանը կատարվում է, ուստի «հրապարակվում է» նման տողը չի մնում հրապարակումից հետո։ Ցուցադրեք հրապարակման արդյունքը առանց
էջը նորից բեռնելու՝

```json
{
  "kind": "action",
  "label": "site.publish",
  "route": { "method": "post", "path": "/services/{serviceId}/site/publish" },
  "followUp": {
    "refresh": ["publish-result"],
    "until": {
      "route": { "method": "get", "path": "/services/{serviceId}/site" },
      "field": "publish.state",
      "equals": ["done", "failed"]
    }
  }
}
```

Երկու օրինակներն էլ ենթադրում են, որ տեսքն ունի նաև `site` և `publish-result` id-ներով բլոկներ, և որ մանիֆեստը հայտարարում է
երեք երթուղիները իր `api` բլոկում և պիտակների բանալիները `locales.en`-ում՝

```json
{
  "locales": {
    "en": {
      "site.name": "Site name",
      "site.description": "Description",
      "site.create": "Create site",
      "site.publish": "Publish"
    }
  }
}
```

`refresh`-ը թվարկում է 1-ից 20 բլոկի `id`՝ յուրաքանչյուրը նույն տեսքում առկա։ Յուրաքանչյուր `id` ունեցող բլոկին տվեք իր սեփականը, ցանկացած
կոնտեյներում (ներդիր, վահանակ, վերնագիր)՝ վահանակը մեկ `id` ունեցող բլոկները դիտարկում է որպես մեկ բլոկ, ուստի թարմացումը կհասնի բոլորին,
իսկ դրանցից մեկի նոր follow-up-ը կդադարեցնի մյուսի ընթացիկ follow-up-ը։ Այն տեսքը, որը մեկ `id` տալիս է երկու բլոկի,
մերժվում է տեղադրման ժամանակ (`PLUGIN_VIEW_BLOCK_ID_REPEATED`). այդպիսի տեսքով արդեն տեղադրված պլագինը շարունակում է բեռնվել,
սերվերը գրում է նախազգուշացում իր մատյանում, և վահանակը նկարում է յուրաքանչյուր բլոկ, բայց մեկ `id` ունեցող բլոկների հետ վարվում է որպես մեկի հետ։ Ներդիրի կամ preview աղբյուրի `id`-ն առանձին անուն է և կարող է համընկնել
բլոկի `id`-ի հետ։ Վահանակը որքան հաճախ և որքան երկար է կատարում ստուգումը, սերվերի ընտրությունն է, ոչ ձերը՝ յուրաքանչյուր 3 վայրկյանը մեկ, առավելագույնը 20 անգամ (մոտ մեկ րոպե)։ Երկար աշխատանքը պետք է հաղորդի իր վիճակը
`until` երթուղիով՝ ավելի շատ ժամանակ սպասելու փոխարեն։

**`bodySource`**-ը դրվում է հաճախորդի տարածքի գործողության բլոկի վրա և կոճակին ստիպում է ուղարկել այն, ինչ տեսել է ընթերցողը։ Այն պարունակում է
ճիշտ `GET` `route` և 1-ից 10 `fields`, յուրաքանչյուրը մարմնի անդամ (camelCase անուն, ոչ թե պիտակի անդամ, ինչպես `title`)
և այն կետերով ուղին, որից այն կարդացվում է։ Առանց դրա գործողությունը ուղարկում է `{}`, ինչպես նախկինում։

```json
{
  "kind": "action",
  "id": "publish",
  "label": "site.publish",
  "route": { "method": "post", "path": "/services/{serviceId}/site/publish" },
  "labelSource": { "route": { "method": "get", "path": "/services/{serviceId}/site" }, "field": "publishLabel" },
  "bodySource": {
    "route": { "method": "get", "path": "/services/{serviceId}/site" },
    "fields": { "expectedVersion": "publishVersion" }
  }
}
```

Գործողությունը պահանջում է `id` և `POST`, `PUT` կամ `PATCH` երթուղի, իսկ `labelSource`-ը պետք է կարդա նույն երթուղին. վահանակը
կատարում է մեկ ընթերցում երկուսի համար, ուստի կոճակն ուղարկում է իր պիտակի վրայի արժեքը։ Կոճակն անջատված է, քանի դեռ յուրաքանչյուր անդամ չի կարդացվել
որպես տող, թիվ կամ բուլյան արժեք։ Մարմինը ֆիքսվում է սեղմման պահին։ Մերժված կանչը նորից կարդում է տվյալները և գործողության
`refresh` բլոկները, ուստի կոնֆլիկտին պատասխանեք սեփական նախադասությամբ. արժեքը դեռ ընթացիկ է արդյոք, որոշում է ձեր երթուղին, ոչ թե վահանակը։
Զրույցի իրադարձության արդյունքի քարտի գործողությունը կրում է նույն ձևի բառացի `body`. սխալ ձևավորված `body` ունեցող իրադարձությունը կորցնում է այդ գործողությունը։

Այս կանոնները խախտող տեսքը մերժվում է տեղադրման ժամանակ և բաց է թողնվում գործարկման ժամանակ՝ `PLUGIN_VIEW_ACTION_BODY_INVALID` (վատ
`bodySource` կամ սխալ տեղում դրված), `PLUGIN_VIEW_CONDITION_INVALID` (վատ
պայման, `GET` չհանդիսացող պայմանի երթուղի, բլոկից դուրս `visibleWhen`), `PLUGIN_VIEW_FOLLOW_UP_INVALID` (վատ
`followUp` կամ սխալ տեղում դրված, `refresh` id, որը ոչ մի բլոկ չի կրում, `resetOnSuccess`, որը բուլյան չէ կամ `submit`-ում չէ),
և `PLUGIN_VIEW_ROUTE_UNDECLARED`՝ պայմանի երթուղու համար, որը մանիֆեստը չի հայտարարել։

Երկու ստուգում ավելացվեցին այն բանից հետո, երբ պլագիններն արդեն տեղադրված էին, ուստի դրանք կիրառվում են միայն **տեղադրման** ժամանակ՝ `PLUGIN_VIEW_BLOCK_ID_REPEATED`
(մեկ `id` մեկից ավելի բլոկի վրա) և `PLUGIN_VIEW_INVALID`՝ ցուցադրման սխալ տիպի անդամի համար (վերնագրի `compact`,
`titleSource` կամ `subtitleSource`, գործողության `labelSource`, preview-ի `note`, աղյուսակի `display`)։ Այդպիսի տեսքով արդեն
տեղադրված պլագինը սերվերի թարմացման ժամանակ չի հեռացվում. այն բեռնվում է, և սերվերը յուրաքանչյուր խախտման համար մեկ նախազգուշացում է գրում
իր մատյանում։ Ուղղեք դրանք ձեր հաջորդ տարբերակում, քանի որ այն վերբեռնելը մերժվելու է, քանի դեռ չեք ուղղել։

## Աշխատանքային էկրաններ՝ layout, split և preview {#working-screens-layout-split-and-preview}

Պահանջում է SDK 3.2.0 կամ ավելի նոր. տեսքի սխեման մնում է 1։

Հաճախորդի էջը կարող է `plugin.json`-ում սահմանել `"layout": "workspace"` (`PluginPageLayouts`)՝ ամբողջ լայնությամբ, առանց ծառայության կողային մենյուի,
բարակ հղումով դեպի ծառայությունը (որ ծառայությունն է, ցույց է տալիս նրա `{serviceId}` ուղու պարամետրը)։ Բացակայությունը նշանակում է `standard`։ Ադմինի էջը
layout չի ընդունում։ Վատ արժեքը `PLUGIN_PAGE_LAYOUT_UNKNOWN` է։

`split`-ը դնում է երկու վահանակ կողք կողքի, հեռախոսի վրա՝ իրար վրա, և երբեք չի ներդրվում split-ի մեջ (`PLUGIN_VIEW_SPLIT_INVALID`)՝

```json
{ "kind": "split", "panes": [ { "blocks": [ { "kind": "chat", "send": { "method": "post", "path": "/chat" } } ] },
                              { "blocks": [ { "kind": "preview", "sources": [ ] } ] } ] }
```

`preview`-ն ցուցադրում է մեկ հասցե մեկուսացված շրջանակում, միայն հաճախորդի էջում՝

```json
{
  "kind": "preview",
  "id": "site-preview",
  "size": "desktop",
  "sources": [
    { "id": "preview", "label": "site.preview", "route": { "method": "get", "path": "/services/{serviceId}/site" }, "field": "previewUrl" },
    { "id": "live", "label": "site.live", "route": { "method": "get", "path": "/services/{serviceId}/site" }, "field": "liveUrl" }
  ]
}
```

Մինչև 5 `sources`, յուրաքանչյուրը ճիշտ `id`, `label`, `GET` `route` և կետերով `field`. `size`-ը `desktop` է կամ `phone`, և
ընթերցողը կարող է փոխել երկուսն էլ։ Վատ ձևը, կամ preview սլոտում կամ ադմինի էջում, `PLUGIN_VIEW_PREVIEW_INVALID` է։

**Ընթերցողին տարրեր ընտրելու հնարավորություն տալը։** Preview-ն կարող է հայտարարել `selection`, իսկ նույն փաստաթղթի զրույցը՝ `"selection": true`. բացակայության դեպքում երկուսն էլ անջատված են՝

```json
"selection": {
  "sources": ["preview"],
  "route": { "method": "post", "path": "/services/{serviceId}/site/selection" },
  "release": { "route": { "method": "get", "path": "/services/{serviceId}/site/state" }, "field": "previewRelease" }
}
```

`sources`-ը այն աղբյուրների id-ներն են, որոնք կարող են ընտրել. կենդանի կայքը թողեք դուրս։ Շրջանակավորված էջը աշխատեցնում է ձեր սեփական սկրիպտը և վահանակի հետ խոսում է `postMessage`-ով, `{ "type": "hostpanel.preview.selection", "version": 1, "release": "...", "event": "ready" | "toggle" | "exit", "id": "..." }`, և վահանակից ստանում է `{ type, version, "event": "state", "active": bool, "selected": [ids] }`։ Ընտրելու կոճակը հայտնվում է միայն `ready`-ից հետո։ `id`-ն տառերից և թվերից կազմված կետերով ուղի է, առավելագույնը 128 նիշ և 20 մեկ ընտրության համար։ **Հաղորդագրությունը պնդում է, երբեք վստահելի չէ.** վահանակն այն ընդունում է միայն իր շրջանակի պատուհանից, ճիշտ այս ձևով, ձեր երթուղու հաղորդած release-ը նշելով, թվարկված աղբյուրի վրա, իսկ `toggle`-ը՝ միայն ռեժիմի միացված ժամանակ. շրջանակի ցանկացած սկրիպտ դեռ կարող է ուղարկել այն։ Ուստի ձեր `route`-ը ստանում է `{ "releaseId", "ids" }` և պատասխանում է `{ "items": [{ "id", "label" }] }`՝ այն id-ներով, որոնք իրոք գիտի, և վահանակը նկարում է միայն այդ պիտակները։ Հաջորդ զրույցի հաղորդագրությունը կրում է `"selection": { "releaseId", "ids" }`, որը ձեր սերվերը պետք է կրկին ստուգի. շրջանակից ոչ մի տեքստ մի վերցրեք։ `ids`-ն ու `releaseId`-ն երկու երթուղիներում էլ համարեք անվստահելի մուտք. դրանք գալիս են էջի սկրիպտից, ինչ էլ առաջինը ստուգած լինի վահանակը, ուստի ստուգեք դրանք ձեր սեփական տվյալների դեմ և առանց այդ ստուգման երբեք մի օգտագործեք որպես ուղի, հարցման հատված կամ մոդելի համար տեքստ։ Ընտրությունը դատարկվում է, երբ release-ը փոխվում է։ Այդ ժամանակ վահանակը անտեսում է շրջանակը, մինչև նա նորից ասի `ready`, ուստի **շրջանակավորված էջը release-ի փոփոխությունից հետո պետք է նորից ուղարկի `ready`** (նոր փաստաթուղթը դա անում է բեռնվելով. էջը, որը թարմացվում է տեղում, պետք է դա ուղարկի ինքը), այլապես ընտրելու կոճակը մնում է թաքնված։ «Կրկին փորձել» (`resend`) գործողությունը ուղարկում է նույն ընտրությունը, քանի դեռ նրա release-ը preview-ում է. երբ release-ը փոխվել է, վահանակը ոչինչ չի ուղարկում, ընթերցողին ասում է, որ էջը փոխվել է և պետք է նորից ընտրել, և տեքստը թողնում է հաղորդագրության դաշտում, քանի որ ընտրությունից զուրկ հաղորդագրությունը ավելի հեռու կհասներ, քան ընթերցողի ընտրածը։ Վահանակը release-ը կարդում է միայն բլոկը կարդալիս, ուստի **ձեր զրույցի երթուղին պետք է մերժի `releaseId`, որն այլևս preview-ում չէ, խնդրի պատասխանով և նախքան որևէ բան փոխելը**. վահանակի ստուգումը քաղաքավարություն է, իսկ ձերն է, որ պահում է։ Մեկ փաստաթղթում միայն մեկ preview կարող է հայտարարել `selection`, քանի որ պնդումը չի ասում, թե որ preview-ն է այն արել։ Վատ ձևը, երկրորդ ընտրող preview-ն կամ preview ընտրություն չունեցող զրույցի ընտրությունը `PLUGIN_VIEW_SELECTION_INVALID` է։

**Հղումներ ընթերցողի սեփական կայքերին։** Պլագինը հրապարակվում է մեկ անգամ և աշխատում է յուրաքանչյուր տեղադրման վրա, ուստի չի կարող թվարկել
յուրաքանչյուրի դոմենները։ Link բլոկը (և զրույցի քարտի հղումը) կարող է նաև ուղղված լինել ընթերցողի սեփական ակտիվ հոսթինգ ծառայությունների դոմենի `https` origin-ին
ստորև նշված preview-ի նույն կանոնով՝ միայն այն ապրանքի վրա, որը օպերատորը կապել է ձեր պլագինի սլոտի վիջեթներին,
ինչպես դոմենը մուտքագրվել է պատվերի ժամանակ (իր `www` անունով), միայն ծառայությունները տեսնելու իրավունք ունեցող ընթերցողի համար, երբեք
վահանակի սեփական origin-ի։ Նույն ցանկը հասնում է սլոտի վիջեթի տեսքին։ Ուստի «բացել կենդանի կայքը» կամ «բացել փորձնական նախադիտումը» հղումը
`externalOrigins` գրառում չի պահանջում տեղադրման վրա տեղակայված կայքերի համար. origin թվարկեք միայն դրանից դուրս հասցեի համար։ Ադմինի
վահանակը ընթերցողի հաշիվ չունի, ուստի այնտեղ գործում են միայն մանիֆեստի origin-ները։ Ձեր պլագինը չի կարող այս ամենից ոչինչ փոխել։

**Ինչ կարող է շրջանակավորվել։** Երթուղու վերադարձրած հասցեն ցուցադրվում է միայն այն դեպքում, երբ նրա origin-ը ձեր մանիֆեստի
`externalOrigins`-ից մեկն է (զուտ `https` origin) կամ ընթերցողի սեփական ակտիվ հոսթինգ ծառայության դոմենն է այն ապրանքի վրա, որը
օպերատորը կապել է ձեր պլագինի սլոտի վիջեթներին, ինչպես ծառայության դոմենը մուտքագրվել է պատվերի ժամանակ (իր `www` անունով, և միայն
ծառայությունները տեսնելու իրավունք ունեցող ընթերցողի համար. ձեր մանիֆեստի `productIds`-ը չի հաշվվում, ոչինչ կապված չլինելը նշանակում է դոմեններ չկան, իսկ վահանակի
սեփական origin-ը երբեք ցանկում չէ)։ Մնացած ամեն ինչ ոչինչ չի ցուցադրում, առանց հաղորդագրության, և ձեր պլագինը չի կարող
դա փոխել. սերվերը ցանկը գրում է յուրաքանչյուր ընթերցողի համար, և նույն ցանկը նեղացնում է էջի `frame-src`-ը, ուստի բրաուզերը մերժում է
ցանկացած այլ շրջանակ նույնպես։

**Շրջանակը `sandbox="allow-scripts allow-forms"` է։** Սկրիպտները աշխատում են, բայց շրջանակն ունի անթափանց origin. կայքի սեփական cookie-ներ կամ
պահոց չկա, թռուցիկ պատուհաններ չկան, վերին նավիգացիա չկա, վահանակին հասանելիություն չկա։ Սեփական սեսիա պահանջող կայքը
այնտեղ կերևա որպես դուրս եկած, իսկ շրջանակավորումը մերժող կայքը (`X-Frame-Options`, `frame-ancestors`) մնում է դատարկ, ուստի ուղարկեք
ձեր preview-ն առանց այդ վերնագրերի։

Մշակողի համակարգչում `docker/.env`-ում `PLUGIN_PREVIEW_DEV_HTTP_ORIGIN=http://localhost:8088` (dev compose ֆայլը այն կապում է
`PluginPreview__DevelopmentHttpOrigins__0`-ին, իսկ `PLUGIN_PREVIEW_DEV_HTTP_ORIGIN_2`-ը՝ `__1`-ին, երկրորդ հասցեի համար.
դատարկը նշանակում է ոչինչ) թույլ է տալիս ճիշտ `http` origin loopback հոսթի վրա։ Development միջավայրից դուրս սերվերը հրաժարվում է գործարկվել, եթե սա սահմանված է։

## Զրույցի իրադարձություններ {#chat-events}

Պահանջում է SDK 3.2.0 կամ ավելի նոր։ `chat` բլոկի հոսքային երթուղին կարող է ուղարկել այս իրադարձությունները (`PluginChatEvents`) պատասխանի
տեքստի կողքին, որպեսզի ընթերցողը տեսնի, թե ինչ է պատահել, առանց այն պարբերության մեջ կարդալու։ Տեքստը պարզ է և արդեն ընթերցողի
լեզվով։ `error`-ը և `done`-ը մնում են սերվերին։ Հենց պատասխանի տեքստը (`delta`, `message`, `history`-ի օգնականի տող) կարող է
օգտագործել Markdown-ի փոքր ենթաբազմություն՝ պարբերություններ, `#` վերնագրեր, `-` և `1.` ցանկեր, պարսպապատ կոդ, `**թավ**`, `*շեղ*` և
`` `code` ``։ HTML-ը և Markdown հղումները դրանում մնում են պարզ տեքստ. հղումն ուղարկեք `link` իրադարձությամբ։

Բլոկն ինքը ընդունում է `send` (հոսքային `post` երթուղի) և, ըստ ցանկության, `history` (`get` երթուղի, որը պատասխանում է `{ "role": "user" | "assistant", "text", "links"? }` տողերով), `placeholder`, `maxLength` (ամենաերկար հաղորդագրությունը), `assistant` (`{ "label" }`, անունը, որով ստորագրվում է յուրաքանչյուր պատասխան), `intro` (`{ "title", "text" }`) և `suggestions` (`[{ "label", "text"? }]`). դատարկ զրույցը ցույց է տալիս ներածությունն ու առաջարկները, իսկ առաջարկը սեղմելիս ուղարկվում է նրա `text`-ը, իսկ առանց դրա՝ `label`-ը։ `refresh`-ը թվարկում է այն բլոկների id-ները, որոնք վահանակը նորից կարդում է քայլի ավարտից հետո՝ հաջող թե ոչ։ Ստորև բերված իրադարձություններից բացի՝ `status`-ը (`{ "text" }`) ասում է, թե հիմա ինչ եք անում, և երևում է պատասխանի ներսում, քանի դեռ այն գրվում է, իսկ `link`-ը (`{ "text", "url" }`) պատասխանին կցում է հղում՝ հղման բլոկի կանոնով. `url`-ը կա՛մ հոսթի նկատմամբ հարաբերական ուղի է (մեկ առաջատար `/`, երբեք `//`, երբեք `/api` կամ դրա տակ որևէ բան, առանց հակադարձ գծի և կառավարող նիշերի), կա՛մ բացարձակ `https` հասցե՝ առանց օգտատիրոջ տվյալների, էջի թույլատրելի ծագումներից մեկի վրա (`externalOrigins`, իսկ հաճախորդի վահանակում՝ նաև ընթերցողի սեփական կապված ծառայությունների `https` ծագումները)։ Ցանկացած այլ հասցե (`http:`, `javascript:`, արձանագրության նկատմամբ հարաբերական կամ այլ ծագում) ստուգվում է իրադարձության ստացման պահին, և հղումը բաց է թողնվում, իսկ պատասխանի տեքստը մնում է։ Վահանակը պահում է ընթերցողի մուտքագրածը ներդիրում, մինչև պատասխան գա, և տեքստը վերադարձնում է դաշտ, եթե ուղարկումը պատասխան չի ստացել։

```json
{ "event": "step", "data": { "id": "build", "label": "Building the site", "state": "running" } }
{ "event": "step", "data": { "id": "build", "label": "Building the site", "state": "done" } }
{ "event": "result", "data": { "tone": "success", "title": "Published", "text": "Your site is live.", "actions": [
  { "label": "Open site", "href": "https://example.com/" },
  { "label": "Roll back", "route": { "method": "post", "path": "/services/{serviceId}/site/rollback" }, "confirm": "Roll back?" } ] } }
{ "event": "issue", "data": { "text": "The build failed: the theme is missing." } }
```

- `step`-ը պատասխանի ցանկի մեկ տողն է. նրա `id`-ն (կամ `label`-ը՝ առանց `id`-ի) նորից ուղարկելը թարմացնում է տողը։
  `state`-ը `running`, `done` կամ `failed` է։
- `result`-ը քարտ է։ `tone`-ը `info` է (լռելյայն), `success`, `warning` կամ `danger`. պետք է լինի `title`-ից և `text`-ից առնվազն մեկը։
  Առավելագույնը 5 `actions`, յուրաքանչյուրը `label`-ով և ճիշտ մեկով՝ `route` (ձեր սեփականներից մեկը, կանչվում է սերվերի միջոցով, ուստի
  չհայտարարված երթուղին կամ բացակայող իրավասությունը սովորականի պես մերժվում է) կամ `href` (link բլոկի կանոնով)։
  Քարտը մնում է զրույցում այն բանից հետո էլ, երբ այն, ինչ առաջարկում է, փոխվել է, ուստի գործողությունը կարող է կրել `visibleWhen`՝ նույն
  պայմանը, ինչ ունի տեսքի բլոկը, միայն թե իրադարձությունը արժեքների ցանկը գրում է `equalsAny` (C# record-ը չի կարող ունենալ
  `equals` անունով անդամ)՝ `{ "route": { "method": "get", "path": "/services/{serviceId}/site" }, "field": "publishVersion",
  "equalsAny": [5] }`։ Երթուղին ձեր սեփական `GET` երթուղին է, որը էջը կարող է լրացնել, `field`-ը՝ կետերով ուղի, իսկ
  `equalsAny`-ը՝ մեկից 20 տող, թիվ, բուլյան արժեք կամ `null`. գործողությունը ցուցադրվում է միայն այնքան ժամանակ, քանի դեռ դաշտը խիստ հավասար է դրանցից
  մեկին։ Վահանակը կարդում է այն քարտը նկարելիս և կրկին ամեն անգամ, երբ զրույցի `refresh`-ում նշված բլոկը նորից
  կարդացվում է (քայլից հետո և քարտի գործողությունից հետո), և գործողությունը թաքցնում է կարդալու ընթացքում, երբ ընթերցումը ձախողվում է և
  երբ պայմանը չի կատարվում։ Այն կիրառվում է երթուղու, `href`-ի և `resend` գործողության վրա հավասարապես։ Առանց դրա գործողությունը
  միշտ ցուցադրվում է։ Վահանակի չկարդացած պայմանը գործողությունը հանում է, քանի որ թաքնվելու համար նախատեսված կոճակը երբեք չպետք է ցուցադրվի։
  Կանչը ընթացիկ է արդյոք, միևնույն է որոշում է ձեր երթուղին. պայմանը քաղաքավարություն է ընթերցողի նկատմամբ, ոչ թե ստուգում։
  Առանց տեսանելի գործողության մնացած տողը նույնպես թաքցվում է։
- `issue`-ն ձեր սեփական ձախողումն է, վտանգի երանգով։ Հոսքը շարունակվում է. ավարտեք այն, երբ պատրաստ եք։

Վահանակը անտեսում է իրադարձությունը, որը չի կարող կարդալ։ **Stop**-ը չեղարկում է հոսքը. ձեր թվարկիչի թոքենը գործարկվում է (`EnumeratorCancellation`)
և այլևս ոչինչ չի ուղարկվում։ Stop-ը ոչինչ չի հետարկում, ուստի հետևեք թոքենին և ձեր սեփական աշխատանքը թողեք հետևողական վիճակում։

## Փաթեթավորում {#package-it}

Թղթապանակի կառուցվածքը ZIP-ի ներսում՝

```
plugin.json                                ← at the root
MyCompany.Hostpanel.Plugins.MyGateway.dll  ← your assembly
plugin.sig                                 ← signature file (required on production servers)
```

**Assembly-ի անվանում։** `Innovayse.Hostpanel.` նախածանցը պատկանում է սերվերին, ուստի այդ նախածանցով DLL պարունակող փաթեթը մերժվում է։ Միակ բացառությունը
`Innovayse.Hostpanel.Plugins.<Name>` պլագինի namespace-ն է (օրինակ՝ `Innovayse.Hostpanel.Plugins.Stripe.dll`), որով անվանվում են պաշտոնական պլագինները և որը փաթեթում միշտ թույլատրված է։ `Plugins`-ից հետո կետը սահմանն է. `Innovayse.Hostpanel.PluginsX.dll`-ը և `Innovayse.Hostpanel.Plugins.dll`-ը դեռ սերվերի անուններ են և մերժվում են։ Սերվերի ոչ մի assembly երբեք չի անվանվում պլագինի namespace-ի տակ (դա պահպանում է ճարտարապետական թեստը), ուստի պլագինը երբեք չի կարող ստվերել որևէ մեկը։ Երրորդ կողմի պլագինները պահում են իրենց սեփական նախածանցը, օրինակ՝ `MyCompany.MyGateway`։

**Մի ներառեք** SDK assembly-ն (`Hostpanel.Sdk.dll`) կամ ցանկացած assembly, որը սերվերն արդեն տրամադրում է։ Սերվերը մերժում է սերվերի assembly պարունակող փաթեթները (`PLUGIN_HOST_ASSEMBLY_REFUSED`)։ Ձեր assembly-ներին տվեք անուններ, որոնք ոչ մի այլ պլագին չի օգտագործում. փաթեթը նաև մերժվում է (`PLUGIN_ASSEMBLY_CONFLICT`), երբ նրա DLL կամ EXE ֆայլերից որևէ մեկի անունը, տառաչափը անտեսելով, արդեն առաքվում է մեկ այլ տեղադրված կամ պատրաստված պլագինի կողմից, քանի որ բեռնիչը երկու պլագինն էլ կկապեր մեկ պատճենի հետ։ Կանոնը վերաբերում է փաթեթի յուրաքանչյուր DLL-ի, ներառյալ ընդհանուր երրորդ կողմի գրադարանները (JSON կամ HTTP գրադարան, որը երկու պլագին էլ առաքում են, հաշվվում է). եթե մեկ այլ տեղադրված պլագին արդեն առաքում է այդ անունով գրադարան, ընտրեք այլ assembly անուն կամ գրադարանը միավորեք ձեր սեփական assembly-ի մեջ։ Նույն պլագինի `id`-ի թարմացումը երբեք չի հակասում ինքն իր հետ։ Կրկնվող պլագինի `id`-ն գործարկման ժամանակ բաց է թողնվում առանց սխալի. առաջինը բեռնված տարբերակը հաղթում է (ներկառուցված պլագինները բեռնվում են վերբեռնվածներից առաջ)։

### Ստորագրեք փաթեթը {#sign-the-package}

Development և Testing միջավայրերից դուրս բեռնվող փաթեթները պետք է ստորագրված լինեն։ Ստորագրության ստուգումները բաց թողնելու համար ադմինի վահանակում բացեք **Հավելումներ** և միացրեք **Թույլատրել ստորագրություն չունեցող պլագիններ**։ `PluginSigner` գործիքը Hostpanel պահոցի մաս է (`backend/tools/Innovayse.Hostpanel.PluginSigner`), ոչ թե SDK-ի։ Գործարկեք այն **չփաթեթավորված թղթապանակի** վրա՝ ZIP անելուց առաջ։ Գործիքը ստորագրության ձևաչափի համար օգտագործում է `Hostpanel.Sdk.Packaging` (3.1.0), ուստի նրա գրած ստորագրությունը ստուգվում է նույն կոդով, որով տեղադրում է սերվերը, և ձեր pipeline-ը կարող է այդ փաթեթից կանչել `PluginSigner` և `PluginSignatureValidator`՝ թողարկումից առաջ ստորագրված թղթապանակը ստուգելու համար։

Ստեղծեք բանալիների զույգ։ Բանալու ֆայլը base64-ով կոդավորված գաղտնի բանալի է. պահեք այն ցանկացած git աշխատանքային ծառից դուրս (եթե բանալու ֆայլը դրա ներսում է, գործիքը հրաժարվում է ստորագրել)՝

```bash
dotnet run --project backend/tools/Innovayse.Hostpanel.PluginSigner -- keygen my-key.key
```

Ստորագրեք պլագինի թղթապանակը՝

```bash
dotnet run --project backend/tools/Innovayse.Hostpanel.PluginSigner -- sign ./MyCompany.Hostpanel.Plugins.MyGateway my-key.key my-key-id
```

Սա թղթապանակում գրում է `plugin.sig`։ Ստորագրելուց հետո թղթապանակը դարձրեք ZIP։

Օպերատորը պետք է համապատասխան հանրային բանալին ավելացնի իր տեղակայման միջավայրի `PluginSigning:OperatorKeys`-ում, այլապես փաթեթը կմերժվի `PLUGIN_SIGNATURE_KEY_UNKNOWN` սխալով։ Պլագինը թարմացնելիս նոր թղթապանակը նորից ստորագրեք՝ նախքան կրկին ZIP անելը. թարմացումը նորից ստուգում է ստորագրությունը, և սերվերը կմերժի հնացած կամ բացակայող `plugin.sig`-ը։

## Թեստավորում {#test-it}

Գրեք ձեր վճարային համակարգի unit թեստերը՝ կեղծ HTTP հաճախորդի դեմ, որպեսզի ոչ մի թեստ իրական վճարային մատակարար չպահանջի։

Ձեռքով ամբողջական թեստի համար՝

<Steps>
<li>

Գործարկեք Hostpanel-ի լոկալ միջավայրը (տես [Լոկալ մշակում](/hy/developers/local-dev))։

</li>
<li>

Կառուցեք ձեր դասերի գրադարանը Release ռեժիմով, ստորագրեք ելքային թղթապանակը `PluginSigner`-ով, դարձրեք ZIP և վերբեռնեք ZIP-ը **Կարգավորումներ → Հավելումների կառավարիչ → Տեղադրել պլագին** ճանապարհով։ Development և Testing միջավայրերում ստորագրություն չունեցող փաթեթները բեռնվում են առանց որևէ անջատիչի։ Մյուս սերվերների վրա ադմինի վահանակում բացեք **Հավելումներ** և ստորագրության ստուգումը բաց թողնելու համար միացրեք **Թույլատրել ստորագրություն չունեցող պլագիններ**։

</li>
<li>

Վերագործարկեք սերվերը «Անհրաժեշտ է վերագործարկում» վերնագրից։

</li>
<li>

Բացեք **Ինտեգրացիաներ**, գտեք ձեր պլագինի քարտը, սեղմեք **Կարգավորել →**, մուտքագրեք փորձնական տվյալները, պահպանեք և միացրեք։

</li>
<li>

Ստեղծեք փորձնական հաճախորդ, կազմեք հաշիվ-ապրանքագիր և վճարեք այն։ Համոզվեք, որ վճարումը երևում է **Դարպասի գործարքների մատյանում**։ Եթե ստորագրության ստուգումը ձախողվի, վերբեռնումը մերժվում է `PLUGIN_UNSIGNED` սխալով։

</li>
</Steps>

## Կիսվեք պլագինով {#share-it}

Օգտագործեք իմաստային տարբերակավորում (`major.minor.patch`)։ Ավելացրեք փոփոխությունների մատյան, որպեսզի օպերատորները իմանան, թե ինչ է փոխվել։ Օպերատորները տեղադրում են Պլագինների կառավարիչով՝ ZIP-ը վերբեռնելով։

<Callout type="danger">
Երբեք մի տեղադրեք իրական մուտքային տվյալներ (API բանալիներ, գաղտնիքներ) սքրինշոթի, issue-ի կամ pull request-ի մեջ։ Օգտագործեք փորձնական տվյալներ կամ թաքցրեք դրանք։
</Callout>

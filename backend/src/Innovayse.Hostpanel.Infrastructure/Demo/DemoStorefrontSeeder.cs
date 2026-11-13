using Innovayse.Hostpanel.Domain.Faqs;
using Innovayse.Hostpanel.Domain.Faqs.Enums;
using Innovayse.Hostpanel.Domain.Locations;
using Innovayse.Hostpanel.Domain.Partners;
using Innovayse.Hostpanel.Domain.Projects;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Domain.Testimonials;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// Seeds the public storefront's own content for the demo: fictional customer testimonials, partners,
/// showcased projects and frequently asked questions, each in English, Russian and Armenian, plus the hero
/// figures and the contact map. The office address belongs to <see cref="DemoPortalSettingsSeeder"/>, which owns every contact line. Everything is invented: the people, companies and addresses are made up
/// and every link is on a reserved <c>.example</c> domain, except the contact map, which must be a real embeddable page. Each dataset is added only when its table is empty,
/// through the entity's factory and behaviour methods, with fixed counts, so a second run changes nothing.
/// </summary>
internal sealed class DemoStorefrontSeeder
{
    /// <summary>The locale codes every translated row carries, in the order the triples below are written.</summary>
    private static readonly string[] Locales = ["en", "ru", "hy"];

    /// <summary>The embeddable map the contact page frames; a real, frame-friendly map page so the block shows a map and not a broken frame.</summary>
    internal const string MapEmbedUrl = "https://www.openstreetmap.org/export/embed.html?bbox=-0.13%2C51.50%2C-0.10%2C51.52&layer=mapnik";

    /// <summary>The storefront settings the demo sets, by key.</summary>
    private static readonly DemoSettingValue[] Settings =
    [
        new(PortalSettingKeys.HeroBadge, "Hosting, domains and servers in one place"),
        new(PortalSettingKeys.StatsClients, "500+"),
        new(PortalSettingKeys.StatsUptime, "99.9%"),
        new(PortalSettingKeys.StatsRegions, "Europe, North America"),
        new(PortalSettingKeys.ContactMapUrl, MapEmbedUrl),
    ];

    /// <summary>The bundled sample portraits a testimonial may show; the rest show the client's initials.</summary>
    private static readonly string[] Avatars =
    [
        "/demo/avatars/avatar-1.svg", "/demo/avatars/avatar-2.svg", "/demo/avatars/avatar-3.svg", "/demo/avatars/avatar-4.svg",
    ];

    /// <summary>The testimonials: client name, company, rating, then the role and the quote in three languages.</summary>
    private static readonly DemoTestimonial[] Testimonials =
    [
        new("Alex Morgan", "Northwind Outfitters", 5,
            ["CEO", "Генеральный директор", "Գործադիր տնօրեն"],
            ["Moving our sites here took one evening, and nothing has gone down since.",
             "Перенос наших сайтов занял один вечер, и с тех пор ничего не падало.",
             "Մեր կայքերի տեղափոխումը տևեց մեկ երեկո, և այդ պահից ոչինչ չի խափանվել։"]),
        new("Mira Stone", "Bluebird Studio", 5,
            ["Creative director", "Креативный директор", "Ստեղծագործական տնօրեն"],
            ["Domains, hosting and billing in one account saves us hours every month.",
             "Домены, хостинг и счета в одном аккаунте экономят нам часы каждый месяц.",
             "Դոմեյնները, հոսթինգը և հաշիվները մեկ հաշվում ամեն ամիս խնայում են մեզ ժամեր։"]),
        new("Jon Park", "Harborline Logistics", 4,
            ["Operations manager", "Операционный менеджер", "Օպերացիոն մենեջեր"],
            ["Support answers fast and actually fixes the problem the first time.",
             "Поддержка отвечает быстро и решает проблему с первого раза.",
             "Աջակցությունը արագ է պատասխանում և խնդիրը լուծում է առաջին անգամից։"]),
        new("Sara Lind", "Fjordly Foods", 5,
            ["Founder", "Основатель", "Հիմնադիր"],
            ["Traffic tripled in peak season and the server held without a single interruption.",
             "Трафик утроился в пиковый сезон, а сервер выдержал без единого сбоя.",
             "Տրաֆիկը եռապատկվեց սեզոնի գագաթնակետին, իսկ սերվերը դիմացավ առանց մեկ խափանման։"]),
        new("Omar Reyes", "Granite Advisory", 5,
            ["Managing partner", "Управляющий партнёр", "Գլխավոր գործընկեր"],
            ["Invoices and renewals are clear, so nothing expires unnoticed.",
             "Счета и продления понятны, поэтому ничего не истекает незамеченным.",
             "Հաշիվները և երկարացումները հստակ են, ուստի ոչինչ աննկատ չի լրանում։"]),
        new("Lena Hart", "Emberwood Press", 4,
            ["Head of web", "Руководитель веб-направления", "Վեբ ուղղության ղեկավար"],
            ["A clean control panel and honest prices. Exactly what we wanted.",
             "Понятная панель управления и честные цены. Именно то, что мы искали.",
             "Մաքուր կառավարման վահանակ և ազնիվ գներ։ Հենց այն, ինչ փնտրում էինք։"]),
        new("Ivo Brandt", "Copperfield Clinic", 5,
            ["IT lead", "Руководитель ИТ", "ՏՏ ղեկավար"],
            ["Our booking system has run here for two years without a single lost appointment.",
             "Наша система записи работает здесь два года без единой потерянной записи.",
             "Մեր ամրագրման համակարգը այստեղ աշխատում է երկու տարի՝ առանց մեկ կորած գրանցման։"]),
        new("Nadia Klein", "Silverpine Retail", 5,
            ["E-commerce manager", "Менеджер интернет-магазина", "Առցանց վաճառքի մենեջեր"],
            ["Black Friday traffic was ten times normal and checkout never slowed down.",
             "В Чёрную пятницу трафик вырос в десять раз, а оформление заказа не замедлилось.",
             "Սև ուրբաթին տրաֆիկը տասնապատկվեց, իսկ պատվերի ձևակերպումը չդանդաղեց։"]),
        new("Tomasz Wolny", "Riverbend Software", 4,
            ["CTO", "Технический директор", "Տեխնիկական տնօրեն"],
            ["Root access, snapshots and a clear API. Our developers felt at home on day one.",
             "Root-доступ, снимки и понятный API. Разработчики освоились с первого дня.",
             "Root հասանելիություն, պատկերներ և հստակ API։ Մշակողները առաջին օրվանից իրենց տանը զգացին։"]),
        new("Grace Owens", "Meadowlark Charity", 5,
            ["Volunteer coordinator", "Координатор волонтёров", "Կամավորների համակարգող"],
            ["Friendly support that explains things without jargon. That matters to a small team.",
             "Дружелюбная поддержка, которая объясняет без жаргона. Для маленькой команды это важно.",
             "Բարյացակամ աջակցություն, որը բացատրում է առանց ժարգոնի։ Փոքր թիմի համար դա կարևոր է։"]),
        new("Ruben Castillo", "Sunpeak Travel", 4,
            ["Marketing lead", "Руководитель маркетинга", "Մարքեթինգի ղեկավար"],
            ["We launched sites in three languages and every one of them loads fast worldwide.",
             "Мы запустили сайты на трёх языках, и каждый быстро открывается по всему миру.",
             "Մենք գործարկեցինք կայքեր երեք լեզվով, և յուրաքանչյուրն արագ է բացվում ամբողջ աշխարհում։"]),
        new("Elif Arslan", "Bosphorus Design", 5,
            ["Studio owner", "Владелец студии", "Ստուդիայի սեփականատեր"],
            ["Managing all our clients' domains and hosting from one place changed how we work.",
             "Управление доменами и хостингом всех клиентов из одного места изменило нашу работу.",
             "Մեր բոլոր հաճախորդների դոմեյնների և հոսթինգի կառավարումը մեկ տեղից փոխեց մեր աշխատանքը։"]),
    ];

    /// <summary>The partners: the name shown, in three languages.</summary>
    private static readonly string[][] Partners =
    [
        ["Alderstone Group", "Олдерстоун Груп", "Օլդերսթոուն Գրուպ"],
        ["Beacon Labs", "Бикон Лабс", "Բիկոն Լաբս"],
        ["Cobalt Works", "Кобальт Воркс", "Կոբալտ Ուորքս"],
        ["Delta Studio", "Дельта Студия", "Դելտա Ստուդիա"],
        ["Ember Foods", "Эмбер Фудс", "Էմբեր Ֆուդս"],
        ["Fjord Logistics", "Фьорд Логистика", "Ֆյորդ Լոգիստիկա"],
        ["Granite Bank", "Гранит Банк", "Գրանիտ Բանկ"],
        ["Harbor Media", "Харбор Медиа", "Հարբոր Մեդիա"],
        ["Irislane Analytics", "Айрислейн Аналитикс", "Այրիսլեյն Անալիտիքս"],
        ["Jadeline Cloud", "Джейдлайн Клауд", "Ջեյդլայն Քլաուդ"],
        ["Vantor Security", "Вантор Секьюрити", "Վանտոր Սեքյուրիթի"],
        ["Lumora Networks", "Люмора Нетворкс", "Լյումորա Նեթվորքս"],
        ["Marlowe Payments", "Марлоу Пейментс", "Մարլոու Փեյմենթս"],
        ["Nimbusline Data", "Нимбуслайн Дата", "Նիմբուսլայն Դատա"],
        ["Orchid Commerce", "Орхид Коммерс", "Օրքիդ Քոմերս"],
        ["Pinnacle Labs", "Пиннакл Лабс", "Փինաքլ Լաբս"],
    ];

    /// <summary>
    /// The server locations: ISO country code, then the data-centre name, the city and the latency note in three
    /// languages. The data centres are invented; the countries and cities are real places.
    /// </summary>
    private static readonly DemoServerLocation[] ServerLocations =
    [
        new("US", ["Atlantic Gate DC1", "Атлантик Гейт DC1", "Ատլանտիկ Գեյթ DC1"],
            ["Ashburn", "Ашберн", "Աշբերն"],
            ["About 8 ms from the US east coast", "Около 8 мс до восточного побережья США", "Մոտ 8 մվ ԱՄՆ արևելյան ափից"]),
        new("DE", ["Rhine Core DC2", "Райн Кор DC2", "Ռայն Կոր DC2"],
            ["Frankfurt", "Франкфурт", "Ֆրանկֆուրտ"],
            ["About 12 ms from central Europe", "Около 12 мс до центральной Европы", "Մոտ 12 մվ Կենտրոնական Եվրոպայից"]),
        new("NL", ["Canal Point DC3", "Канал Поинт DC3", "Քենալ Փոյնթ DC3"],
            ["Amsterdam", "Амстердам", "Ամստերդամ"],
            ["About 10 ms from western Europe", "Около 10 мс до западной Европы", "Մոտ 10 մվ Արևմտյան Եվրոպայից"]),
        new("PL", ["Vistula Gate DC4", "Висла Гейт DC4", "Վիստուլա Գեյթ DC4"],
            ["Warsaw", "Варшава", "Վարշավա"],
            ["About 8 ms from eastern Europe", "Около 8 мс до Восточной Европы", "Մոտ 8 մվ Արևելյան Եվրոպայից"]),
        new("SG", ["Merlion Bay DC5", "Мерлион Бэй DC5", "Մերլիոն Բեյ DC5"],
            ["Singapore", "Сингапур", "Սինգապուր"],
            ["About 15 ms from south-east Asia", "Около 15 мс до Юго-Восточной Азии", "Մոտ 15 մվ Հարավարևելյան Ասիայից"]),
        new("FI", ["Aurora Fjord DC6", "Аврора Фьорд DC6", "Աուրորա Ֆյորդ DC6"],
            ["Helsinki", "Хельсинки", "Հելսինկի"],
            ["About 14 ms from northern Europe", "Около 14 мс до Северной Европы", "Մոտ 14 մվ Հյուսիսային Եվրոպայից"]),
    ];

    /// <summary>Folder, under the client's public assets, that holds the partner logo files named after the partner slug.</summary>
    private const string PartnerLogoFolder = "/demo/partners/";

    /// <summary>Folder, under the client's public assets, that holds the project preview files named after the project slug.</summary>
    private const string ProjectImageFolder = "/demo/projects/";

    /// <summary>The projects: slug, then the title and the one-line summary in three languages.</summary>
    private static readonly DemoProject[] Projects =
    [
        new("northwind-portal", ["Northwind Portal", "Портал Northwind", "Northwind պորտալ"],
            ["A customer portal with online payments and self-service requests.",
             "Клиентский портал с онлайн-оплатой и самообслуживанием.",
             "Հաճախորդների պորտալ առցանց վճարումներով և ինքնասպասարկմամբ։"]),
        new("bluebird-shop", ["Bluebird Shop", "Магазин Bluebird", "Bluebird խանութ"],
            ["An online store with inventory sync and a fast checkout.",
             "Интернет-магазин с синхронизацией склада и быстрым оформлением заказа.",
             "Առցանց խանութ պահեստի համաժամեցմամբ և արագ վճարմամբ։"]),
        new("harborline-tracking", ["Harborline Tracking", "Трекинг Harborline", "Harborline հետևում"],
            ["A shipment tracking dashboard for dispatchers and clients.",
             "Панель отслеживания грузов для диспетчеров и клиентов.",
             "Բեռների հետևման վահանակ դիսպետչերների և հաճախորդների համար։"]),
        new("fjordly-menu", ["Fjordly Menu", "Меню Fjordly", "Fjordly մենյու"],
            ["A restaurant site with a live menu and table booking.",
             "Сайт ресторана с живым меню и бронированием столиков.",
             "Ռեստորանի կայք կենդանի մենյուով և սեղանների ամրագրմամբ։"]),
        new("granite-library", ["Granite Library", "Библиотека Granite", "Granite գրադարան"],
            ["A searchable document library with access control.",
             "Библиотека документов с поиском и разграничением доступа.",
             "Փաստաթղթերի գրադարան որոնմամբ և հասանելիության վերահսկմամբ։"]),
        new("emberwood-magazine", ["Emberwood Magazine", "Журнал Emberwood", "Emberwood ամսագիր"],
            ["A publishing site with subscriptions and a reader archive.",
             "Издательский сайт с подписками и архивом для читателей.",
             "Հրատարակչական կայք բաժանորդագրություններով և ընթերցողների արխիվով։"]),
        new("copperfield-booking", ["Copperfield Booking", "Запись Copperfield", "Copperfield ամրագրում"],
            ["An appointment booking system with reminders by mail and text.",
             "Система записи на приём с напоминаниями по почте и SMS.",
             "Ընդունելության ամրագրման համակարգ՝ հիշեցումներով էլ. փոստով և SMS-ով։"]),
        new("silverpine-store", ["Silverpine Store", "Магазин Silverpine", "Silverpine խանութ"],
            ["A high-traffic fashion store with product filters and gift cards.",
             "Нагруженный магазин одежды с фильтрами товаров и подарочными картами.",
             "Բարձր այցելությամբ հագուստի խանութ՝ ապրանքների զտիչներով և նվեր քարտերով։"]),
        new("riverbend-docs", ["Riverbend Docs", "Документация Riverbend", "Riverbend փաստաթղթեր"],
            ["A developer documentation site with versioned guides and search.",
             "Сайт документации для разработчиков с версиями руководств и поиском.",
             "Մշակողների փաստաթղթերի կայք՝ տարբերակված ուղեցույցներով և որոնմամբ։"]),
        new("meadowlark-donations", ["Meadowlark Donations", "Пожертвования Meadowlark", "Meadowlark նվիրատվություններ"],
            ["A charity site with recurring donations and a volunteer sign-up form.",
             "Благотворительный сайт с регулярными пожертвованиями и анкетой волонтёра.",
             "Բարեգործական կայք՝ պարբերական նվիրատվություններով և կամավորի գրանցման ձևով։"]),
        new("sunpeak-tours", ["Sunpeak Tours", "Туры Sunpeak", "Sunpeak տուրեր"],
            ["A multilingual travel site with tour search and online deposits.",
             "Многоязычный туристический сайт с поиском туров и онлайн-предоплатой.",
             "Բազմալեզու զբոսաշրջային կայք՝ տուրերի որոնմամբ և առցանց կանխավճարով։"]),
        new("bosphorus-portfolio", ["Bosphorus Portfolio", "Портфолио Bosphorus", "Bosphorus պորտֆոլիո"],
            ["A design studio portfolio with case studies and a client area.",
             "Портфолио дизайн-студии с кейсами и кабинетом клиента.",
             "Դիզայն ստուդիայի պորտֆոլիո՝ դեպքերի ուսումնասիրություններով և հաճախորդի բաժնով։"]),
    ];

    /// <summary>The questions: category, then the question and the answer in three languages.</summary>
    private static readonly DemoFaq[] Faqs =
    [
        new(FaqCategory.General, ["What does the platform offer?", "Что предлагает платформа?", "Ի՞նչ է առաջարկում հարթակը"],
            ["Hosting, domains, servers and a client portal for ordering, billing and support.",
             "Хостинг, домены, серверы и клиентский портал для заказов, оплаты и поддержки.",
             "Հոսթինգ, դոմեյններ, սերվերներ և հաճախորդի պորտալ պատվերների, վճարումների և աջակցության համար։"]),
        new(FaqCategory.General, ["How do I get started?", "Как начать?", "Ինչպե՞ս սկսել"],
            ["Choose a plan, create an account and pay the first invoice. Most services activate automatically.",
             "Выберите тариф, создайте аккаунт и оплатите первый счёт. Большинство услуг включается автоматически.",
             "Ընտրեք փաթեթ, ստեղծեք հաշիվ և վճարեք առաջին հաշիվը։ Ծառայությունների մեծ մասն ակտիվանում է ինքնաբերաբար։"]),
        new(FaqCategory.General, ["Which payment methods do you accept?", "Какие способы оплаты вы принимаете?", "Ի՞նչ վճարման եղանակներ եք ընդունում"],
            ["Cards and the other methods enabled at checkout.",
             "Карты и другие способы, доступные при оформлении заказа.",
             "Քարտեր և վճարման այլ եղանակներ, որոնք հասանելի են պատվերի ժամանակ։"]),
        new(FaqCategory.General, ["How does billing work?", "Как устроено выставление счетов?", "Ինչպե՞ս են ձևավորվում հաշիվները"],
            ["Services are billed per cycle. Invoices are issued ahead of the due date and reminders are emailed.",
             "Услуги оплачиваются по циклам. Счета выставляются заранее, напоминания приходят по почте.",
             "Ծառայությունները վճարվում են ցիկլերով։ Հաշիվները ձևավորվում են նախօրոք, հիշեցումները՝ էլ. փոստով։"]),
        new(FaqCategory.General, ["How do I contact support?", "Как связаться с поддержкой?", "Ինչպե՞ս կապվել աջակցության հետ"],
            ["Open a ticket from the client portal or use the contact page.",
             "Откройте тикет в клиентском портале или воспользуйтесь страницей контактов.",
             "Բացեք հարցում հաճախորդի պորտալից կամ օգտվեք կապի էջից։"]),
        new(FaqCategory.General, ["Can I cancel a service?", "Можно ли отменить услугу?", "Կարո՞ղ եմ չեղարկել ծառայությունը"],
            ["Yes. Request cancellation from the service page and it ends at the close of the paid period.",
             "Да. Запросите отмену на странице услуги, она завершится в конце оплаченного периода.",
             "Այո։ Չեղարկումը պահանջեք ծառայության էջից, այն կավարտվի վճարված ժամանակահատվածի վերջում։"]),
        new(FaqCategory.Products, ["Can I move my existing site?", "Можно ли перенести существующий сайт?", "Կարո՞ղ եմ տեղափոխել առկա կայքը"],
            ["Yes. Migration assistance is available for sites, databases and mail; open a ticket to plan it.",
             "Да. Доступна помощь с переносом сайтов, баз данных и почты; откройте тикет, чтобы спланировать его.",
             "Այո։ Հասանելի է կայքերի, տվյալների բազաների և փոստի տեղափոխման աջակցություն. բացեք հարցում՝ այն պլանավորելու համար։"]),
        new(FaqCategory.Products, ["How do I register or transfer a domain?", "Как зарегистрировать или перенести домен?", "Ինչպե՞ս գրանցել կամ տեղափոխել դոմեյն"],
            ["Search for the name on the domains page and add it to your cart. To transfer, provide the authorization code.",
             "Найдите имя на странице доменов и добавьте в корзину. Для переноса укажите код авторизации.",
             "Փնտրեք անունը դոմեյնների էջում և ավելացրեք զամբյուղ։ Տեղափոխման համար նշեք թույլտվության կոդը։"]),
        new(FaqCategory.Products, ["Is an SSL certificate included?", "Входит ли SSL-сертификат?", "Ներառվա՞ծ է SSL վկայագիրը"],
            ["Yes, a free SSL certificate is included in every hosting plan.",
             "Да, бесплатный SSL-сертификат входит в каждый тариф хостинга.",
             "Այո, անվճար SSL վկայագիրը ներառված է հոսթինգի յուրաքանչյուր փաթեթում։"]),
        new(FaqCategory.Products, ["Can I upgrade my plan later?", "Можно ли сменить тариф позже?", "Կարո՞ղ եմ հետո փոխել փաթեթը"],
            ["Yes. Upgrade a service from the client portal at any time.",
             "Да. Повысить тариф можно в клиентском портале в любое время.",
             "Այո։ Ծառայությունը կարելի է թարմացնել հաճախորդի պորտալից ցանկացած պահի։"]),
        new(FaqCategory.Products, ["Are backups included?", "Входят ли резервные копии?", "Ներառվա՞ծ են պահուստային պատճենները"],
            ["Daily backups are included in every hosting plan.",
             "Ежедневные резервные копии входят в каждый тариф хостинга.",
             "Օրական պահուստային պատճենները ներառված են հոսթինգի յուրաքանչյուր փաթեթում։"]),
        new(FaqCategory.Products, ["Do you offer dedicated servers?", "Есть ли выделенные серверы?", "Առաջարկո՞ւմ եք առանձնացված սերվերներ"],
            ["Yes, virtual and dedicated servers with full root access and monitoring.",
             "Да, виртуальные и выделенные серверы с полным root-доступом и мониторингом.",
             "Այո, վիրտուալ և առանձնացված սերվերներ՝ լիարժեք root հասանելիությամբ և մոնիթորինգով։"]),
        new(FaqCategory.General, ["Is there a money-back guarantee?", "Есть ли гарантия возврата денег?", "Կա՞ գումարի վերադարձի երաշխիք"],
            ["Yes. Hosting plans can be cancelled within thirty days for a full refund.",
             "Да. Тарифы хостинга можно отменить в течение тридцати дней с полным возвратом.",
             "Այո։ Հոսթինգի փաթեթները կարելի է չեղարկել երեսուն օրվա ընթացքում՝ ամբողջական վերադարձով։"]),
        new(FaqCategory.General, ["Can I pay annually?", "Можно ли платить за год?", "Կարո՞ղ եմ վճարել տարեկան"],
            ["Yes. Every plan has an annual price that saves about two months compared with monthly billing.",
             "Да. У каждого тарифа есть годовая цена, которая экономит около двух месяцев.",
             "Այո։ Յուրաքանչյուր փաթեթ ունի տարեկան գին, որը խնայում է մոտ երկու ամիս։"]),
        new(FaqCategory.General, ["Which currencies can I pay in?", "В какой валюте можно платить?", "Ի՞նչ արժույթով կարող եմ վճարել"],
            ["Dollars and euros. Choose the currency when you create the account.",
             "В долларах и евро. Валюту выбирают при создании аккаунта.",
             "Դոլարով և եվրոյով։ Արժույթն ընտրվում է հաշիվ ստեղծելիս։"]),
        new(FaqCategory.General, ["Do you send invoices by email?", "Отправляете ли вы счета по почте?", "Հաշիվներն ուղարկո՞ւմ եք էլ. փոստով"],
            ["Yes. Every invoice is emailed and is also available to download in the client portal.",
             "Да. Каждый счёт приходит по почте и доступен для скачивания в клиентском портале.",
             "Այո։ Յուրաքանչյուր հաշիվ ուղարկվում է էլ. փոստով և հասանելի է ներբեռնման համար հաճախորդի պորտալում։"]),
        new(FaqCategory.General, ["Can several people manage one account?", "Могут ли несколько человек управлять одним аккаунтом?", "Կարո՞ղ են մի քանի հոգի կառավարել մեկ հաշիվ"],
            ["Yes. Invite team members and choose what each of them may see and do.",
             "Да. Пригласите участников команды и выберите, что каждый может видеть и делать.",
             "Այո։ Հրավիրեք թիմի անդամներին և ընտրեք, թե յուրաքանչյուրն ինչ կարող է տեսնել և անել։"]),
        new(FaqCategory.General, ["Where are your data centres?", "Где находятся ваши дата-центры?", "Որտե՞ղ են գտնվում ձեր տվյալների կենտրոնները"],
            ["In Europe and North America. Choose the location when you order a server.",
             "В Европе и Северной Америке. Расположение выбирается при заказе сервера.",
             "Եվրոպայում և Հյուսիսային Ամերիկայում։ Վայրն ընտրվում է սերվեր պատվիրելիս։"]),
        new(FaqCategory.Products, ["Can I host several sites on one plan?", "Можно ли разместить несколько сайтов на одном тарифе?", "Կարո՞ղ եմ մեկ փաթեթում տեղադրել մի քանի կայք"],
            ["Yes. Business Hosting allows ten sites and Pro Hosting allows unlimited sites.",
             "Да. Business Hosting вмещает десять сайтов, а Pro Hosting без ограничений.",
             "Այո։ Business Hosting-ը թույլ է տալիս տասը կայք, իսկ Pro Hosting-ը՝ անսահմանափակ։"]),
        new(FaqCategory.Products, ["Do you offer managed site hosting?", "Есть ли управляемый хостинг сайтов?", "Առաջարկո՞ւմ եք կառավարվող կայքերի հոսթինգ"],
            ["Yes. Managed Site Hosting keeps the site engine and its plugins up to date for you.",
             "Да. Тариф Managed Site Hosting сам обновляет движок сайта и плагины.",
             "Այո։ Managed Site Hosting-ը ինքն է թարմացնում կայքի շարժիչը և հավելումները։"]),
        new(FaqCategory.Products, ["Can I take snapshots of my VPS?", "Можно ли делать снимки VPS?", "Կարո՞ղ եմ VPS-ի պատկերներ ստեղծել"],
            ["Yes. Every VPS can keep up to three snapshots at no extra cost.",
             "Да. Каждый VPS может хранить до трёх снимков без доплаты.",
             "Այո։ Յուրաքանչյուր VPS կարող է պահել մինչև երեք պատկեր՝ առանց հավելավճարի։"]),
        new(FaqCategory.Products, ["What is WHOIS privacy?", "Что такое скрытие WHOIS?", "Ի՞նչ է WHOIS գաղտնիությունը"],
            ["It keeps your contact details out of the public domain record and forwards mail to you.",
             "Оно скрывает ваши контакты в публичной записи домена и пересылает вам почту.",
             "Այն թաքցնում է ձեր կոնտակտային տվյալները դոմեյնի հրապարակային գրառումից և փոստը վերահասցեավորում ձեզ։"]),
        new(FaqCategory.Products, ["Which certificate should I choose?", "Какой сертификат выбрать?", "Ո՞ր վկայագիրն ընտրեմ"],
            ["Basic suits one site, Wildcard covers every subdomain and Extended shows your verified organisation.",
             "Basic подходит для одного сайта, Wildcard покрывает все поддомены, а Extended показывает проверенную организацию.",
             "Basic-ը հարմար է մեկ կայքի համար, Wildcard-ը ծածկում է բոլոր ենթադոմեյնները, իսկ Extended-ը ցույց է տալիս ստուգված կազմակերպությունը։"]),
        new(FaqCategory.Products, ["Can I add more mailboxes later?", "Можно ли добавить почтовые ящики позже?", "Կարո՞ղ եմ հետո ավելացնել փոստարկղեր"],
            ["Yes. Upgrade the mail plan at any time and the new mailboxes are ready at once.",
             "Да. Повысьте почтовый тариф в любое время, и новые ящики сразу готовы.",
             "Այո։ Փոստի փաթեթը կարելի է թարմացնել ցանկացած պահի, և նոր փոստարկղերը անմիջապես պատրաստ են։"]),
    ];

    /// <summary>The data context the storefront rows will be added to and saved through.</summary>
    private readonly AppDbContext _db;

    /// <summary>The anchor the testimonial publish dates are counted back from.</summary>
    private readonly DateTimeOffset _today;

    /// <summary>Creates the seeder.</summary>
    /// <param name="db">The data context the storefront rows will be added to and saved through.</param>
    /// <param name="today">The anchor the testimonial publish dates are counted back from.</param>
    public DemoStorefrontSeeder(AppDbContext db, DateTimeOffset today)
    {
        _db = db;
        _today = today;
    }

    /// <summary>Seeds the storefront content that is not there yet and sets the hero and office settings.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    public async Task SeedAsync(CancellationToken ct)
    {
        await SeedContentAsync(ct);
        await SeedSettingsAsync(ct);
    }

    /// <summary>
    /// Seeds only the storefront content tables (testimonials, partners, server locations, projects, FAQs) that are still empty,
    /// leaving the settings alone. The development seeder calls this so a local storefront is fully populated
    /// without its locally edited settings being overwritten on every start.
    /// </summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    public async Task SeedContentAsync(CancellationToken ct)
    {
        await SeedTestimonialsAsync(ct);
        await SeedPartnersAsync(ct);
        await SeedLocationsAsync(ct);
        await SeedProjectsAsync(ct);
        await SeedFaqsAsync(ct);
    }

    /// <summary>Adds the testimonials when there are none.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedTestimonialsAsync(CancellationToken ct)
    {
        if (await _db.Testimonials.AnyAsync(ct))
        {
            return;
        }

        for (var i = 0; i < Testimonials.Length; i++)
        {
            var (name, company, rating, role, quote) = Testimonials[i];
            // Every third testimonial shows a bundled portrait, the others fall back to initials; one a month back over the last year.
            var avatar = i % 3 == 0 ? Avatars[(i / 3) % Avatars.Length] : null;
            var row = Testimonial.Create(name, company, avatar, rating, i, true, _today.AddMonths(-((i % 12) + 1)));
            for (var l = 0; l < Locales.Length; l++)
            {
                row.SetTranslation(Locales[l], role[l], quote[l]);
            }

            _db.Testimonials.Add(row);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Adds the partners when there are none; each carries a bundled logo file, a name and a link.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedPartnersAsync(CancellationToken ct)
    {
        if (await _db.Partners.AnyAsync(ct))
        {
            return;
        }

        for (var i = 0; i < Partners.Length; i++)
        {
            var row = Partner.Create(PartnerLogoUrl(Partners[i][0]), $"https://partner-{i + 1}.example", i, true);
            for (var l = 0; l < Locales.Length; l++)
            {
                row.SetTranslation(Locales[l], Partners[i][l]);
            }

            _db.Partners.Add(row);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Adds the server locations when there are none; every one is active and shown in the order written above.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedLocationsAsync(CancellationToken ct)
    {
        if (await _db.ServerLocations.AnyAsync(ct))
        {
            return;
        }

        for (var i = 0; i < ServerLocations.Length; i++)
        {
            var (country, name, city, note) = ServerLocations[i];
            var row = ServerLocation.Create(country, i, true);
            for (var l = 0; l < Locales.Length; l++)
            {
                row.SetTranslation(Locales[l], name[l], city[l], note[l]);
            }

            _db.ServerLocations.Add(row);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The bundled logo address of a partner: its English name in lower case with the spaces turned into hyphens,
    /// under <see cref="PartnerLogoFolder"/>, as an SVG file.
    /// </summary>
    /// <param name="englishName">The partner's name in English.</param>
    /// <returns>The site-relative address of the partner's logo.</returns>
    internal static string PartnerLogoUrl(string englishName)
    {
        return $"{PartnerLogoFolder}{englishName.ToLowerInvariant().Replace(' ', '-')}.svg";
    }

    /// <summary>The bundled preview address of a project: its slug under <see cref="ProjectImageFolder"/>, as an SVG file.</summary>
    /// <param name="slug">The project's slug.</param>
    /// <returns>The site-relative address of the project's preview picture.</returns>
    internal static string ProjectImageUrl(string slug)
    {
        return $"{ProjectImageFolder}{slug}.svg";
    }

    /// <summary>Adds the projects when there are none; each carries a bundled preview picture, text and a link.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedProjectsAsync(CancellationToken ct)
    {
        if (await _db.Projects.AnyAsync(ct))
        {
            return;
        }

        for (var i = 0; i < Projects.Length; i++)
        {
            var (slug, title, summary) = Projects[i];
            var row = Project.Create(slug, $"https://{slug}.example", [ProjectImageUrl(slug)], i, true);
            for (var l = 0; l < Locales.Length; l++)
            {
                row.SetTranslation(Locales[l], title[l], summary[l], null);
            }

            _db.Projects.Add(row);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Adds the frequently asked questions when there are none.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedFaqsAsync(CancellationToken ct)
    {
        if (await _db.Faqs.AnyAsync(ct))
        {
            return;
        }

        for (var i = 0; i < Faqs.Length; i++)
        {
            var (category, question, answer) = Faqs[i];
            var row = Faq.Create(category, i, true);
            for (var l = 0; l < Locales.Length; l++)
            {
                row.SetTranslation(Locales[l], question[l], answer[l]);
            }

            _db.Faqs.Add(row);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Sets the hero figures and the contact map on the rows the shared settings seeder created.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the settings are saved.</returns>
    private async Task SeedSettingsAsync(CancellationToken ct)
    {
        var keys = Settings.Select(s => s.Key).ToList();
        var rows = await _db.Settings.Where(s => keys.Contains(s.Key)).ToDictionaryAsync(s => s.Key, ct);

        foreach (var (key, value) in Settings)
        {
            if (rows.TryGetValue(key, out var row) && row.Value != value)
            {
                row.UpdateValue(value);
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}

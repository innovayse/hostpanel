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
/// showcased projects and frequently asked questions, each in English, Russian, Armenian, German and Spanish, plus the hero
/// figures and the contact map. The office address belongs to <see cref="DemoPortalSettingsSeeder"/>, which owns every contact line. Everything is invented: the people, companies and addresses are made up
/// and every link is on a reserved <c>.example</c> domain, except the contact map, which must be a real embeddable page. Each dataset is added only when its table is empty,
/// through the entity's factory and behaviour methods, with fixed counts, so a second run changes nothing.
/// </summary>
internal sealed class DemoStorefrontSeeder
{
    /// <summary>The locale codes every translated row carries, in the order the triples below are written.</summary>
    private static readonly string[] Locales = ["en", "ru", "hy", "de", "es"];

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

    /// <summary>The testimonials: client name, company, rating, then the role and the quote in five languages.</summary>
    private static readonly DemoTestimonial[] Testimonials =
    [
        new("Alex Morgan", "Northwind Outfitters", 5,
            ["CEO", "Генеральный директор", "Գործադիր տնօրեն", "Geschäftsführer", "Director ejecutivo"],
            ["Moving our sites here took one evening, and nothing has gone down since.",
             "Перенос наших сайтов занял один вечер, и с тех пор ничего не падало.",
             "Մեր կայքերի տեղափոխումը տևեց մեկ երեկո, և այդ պահից ոչինչ չի խափանվել։",
             "Der Umzug unserer Websites dauerte einen Abend, und seitdem gab es keinen einzigen Ausfall.",
             "Trasladar nuestros sitios nos llevó una tarde y desde entonces no ha habido ni una sola caída."]),
        new("Mira Stone", "Bluebird Studio", 5,
            ["Creative director", "Креативный директор", "Ստեղծագործական տնօրեն", "Kreativdirektorin", "Directora creativa"],
            ["Domains, hosting and billing in one account saves us hours every month.",
             "Домены, хостинг и счета в одном аккаунте экономят нам часы каждый месяц.",
             "Դոմեյնները, հոսթինգը և հաշիվները մեկ հաշվում ամեն ամիս խնայում են մեզ ժամեր։",
             "Domains, Hosting und Abrechnung in einem Konto sparen uns jeden Monat Stunden.",
             "Tener dominios, alojamiento y facturación en una sola cuenta nos ahorra horas cada mes."]),
        new("Jon Park", "Harborline Logistics", 4,
            ["Operations manager", "Операционный менеджер", "Օպերացիոն մենեջեր", "Betriebsleiter", "Responsable de operaciones"],
            ["Support answers fast and actually fixes the problem the first time.",
             "Поддержка отвечает быстро и решает проблему с первого раза.",
             "Աջակցությունը արագ է պատասխանում և խնդիրը լուծում է առաջին անգամից։",
             "Der Support antwortet schnell und löst das Problem tatsächlich gleich beim ersten Mal.",
             "El soporte responde con rapidez y resuelve el problema a la primera."]),
        new("Sara Lind", "Fjordly Foods", 5,
            ["Founder", "Основатель", "Հիմնադիր", "Gründerin", "Fundadora"],
            ["Traffic tripled in peak season and the server held without a single interruption.",
             "Трафик утроился в пиковый сезон, а сервер выдержал без единого сбоя.",
             "Տրաֆիկը եռապատկվեց սեզոնի գագաթնակետին, իսկ սերվերը դիմացավ առանց մեկ խափանման։",
             "In der Hochsaison hat sich der Traffic verdreifacht, und der Server lief ohne eine einzige Unterbrechung.",
             "El tráfico se triplicó en plena temporada y el servidor aguantó sin una sola interrupción."]),
        new("Omar Reyes", "Granite Advisory", 5,
            ["Managing partner", "Управляющий партнёр", "Գլխավոր գործընկեր", "Geschäftsführender Partner", "Socio director"],
            ["Invoices and renewals are clear, so nothing expires unnoticed.",
             "Счета и продления понятны, поэтому ничего не истекает незамеченным.",
             "Հաշիվները և երկարացումները հստակ են, ուստի ոչինչ աննկատ չի լրանում։",
             "Rechnungen und Verlängerungen sind übersichtlich, sodass nichts unbemerkt abläuft.",
             "Las facturas y las renovaciones son claras, así que nada caduca sin que nos demos cuenta."]),
        new("Lena Hart", "Emberwood Press", 4,
            ["Head of web", "Руководитель веб-направления", "Վեբ ուղղության ղեկավար", "Leiterin Web", "Responsable de web"],
            ["A clean control panel and honest prices. Exactly what we wanted.",
             "Понятная панель управления и честные цены. Именно то, что мы искали.",
             "Մաքուր կառավարման վահանակ և ազնիվ գներ։ Հենց այն, ինչ փնտրում էինք։",
             "Ein aufgeräumtes Kontrollpanel und faire Preise. Genau das, was wir wollten.",
             "Un panel de control claro y precios honestos. Justo lo que buscábamos."]),
        new("Ivo Brandt", "Copperfield Clinic", 5,
            ["IT lead", "Руководитель ИТ", "ՏՏ ղեկավար", "IT-Leiter", "Responsable de TI"],
            ["Our booking system has run here for two years without a single lost appointment.",
             "Наша система записи работает здесь два года без единой потерянной записи.",
             "Մեր ամրագրման համակարգը այստեղ աշխատում է երկու տարի՝ առանց մեկ կորած գրանցման։",
             "Unser Buchungssystem läuft hier seit zwei Jahren, ohne dass je ein Termin verloren ging.",
             "Nuestro sistema de reservas funciona aquí desde hace dos años sin perder una sola cita."]),
        new("Nadia Klein", "Silverpine Retail", 5,
            ["E-commerce manager", "Менеджер интернет-магазина", "Առցանց վաճառքի մենեջեր", "E-Commerce-Managerin", "Responsable de comercio electrónico"],
            ["Black Friday traffic was ten times normal and checkout never slowed down.",
             "В Чёрную пятницу трафик вырос в десять раз, а оформление заказа не замедлилось.",
             "Սև ուրբաթին տրաֆիկը տասնապատկվեց, իսկ պատվերի ձևակերպումը չդանդաղեց։",
             "Am Black Friday war der Traffic zehnmal so hoch wie sonst, und der Checkout wurde nie langsamer.",
             "El Black Friday el tráfico fue diez veces superior al habitual y el pago nunca se ralentizó."]),
        new("Tomasz Wolny", "Riverbend Software", 4,
            ["CTO", "Технический директор", "Տեխնիկական տնօրեն", "Technischer Leiter (CTO)", "Director de tecnología (CTO)"],
            ["Root access, snapshots and a clear API. Our developers felt at home on day one.",
             "Root-доступ, снимки и понятный API. Разработчики освоились с первого дня.",
             "Root հասանելիություն, պատկերներ և հստակ API։ Մշակողները առաջին օրվանից իրենց տանը զգացին։",
             "Root-Zugriff, Snapshots und eine klare API. Unsere Entwickler fühlten sich vom ersten Tag an zu Hause.",
             "Acceso root, instantáneas y una API clara. Nuestros desarrolladores se sintieron como en casa desde el primer día."]),
        new("Grace Owens", "Meadowlark Charity", 5,
            ["Volunteer coordinator", "Координатор волонтёров", "Կամավորների համակարգող", "Koordinatorin der Freiwilligen", "Coordinadora de voluntarios"],
            ["Friendly support that explains things without jargon. That matters to a small team.",
             "Дружелюбная поддержка, которая объясняет без жаргона. Для маленькой команды это важно.",
             "Բարյացակամ աջակցություն, որը բացատրում է առանց ժարգոնի։ Փոքր թիմի համար դա կարևոր է։",
             "Freundlicher Support, der ohne Fachjargon erklärt. Für ein kleines Team ist das wichtig.",
             "Un soporte amable que explica las cosas sin jerga. Para un equipo pequeño eso es fundamental."]),
        new("Ruben Castillo", "Sunpeak Travel", 4,
            ["Marketing lead", "Руководитель маркетинга", "Մարքեթինգի ղեկավար", "Marketingleiter", "Responsable de marketing"],
            ["We launched sites in five languages and every one of them loads fast worldwide.",
             "Мы запустили сайты на трёх языках, и каждый быстро открывается по всему миру.",
             "Մենք գործարկեցինք կայքեր երեք լեզվով, և յուրաքանչյուրն արագ է բացվում ամբողջ աշխարհում։",
             "Wir haben Websites in drei Sprachen gestartet, und jede lädt weltweit schnell.",
             "Lanzamos sitios en tres idiomas y todos cargan rápido en cualquier parte del mundo."]),
        new("Elif Arslan", "Bosphorus Design", 5,
            ["Studio owner", "Владелец студии", "Ստուդիայի սեփականատեր", "Studioinhaberin", "Propietaria del estudio"],
            ["Managing all our clients' domains and hosting from one place changed how we work.",
             "Управление доменами и хостингом всех клиентов из одного места изменило нашу работу.",
             "Մեր բոլոր հաճախորդների դոմեյնների և հոսթինգի կառավարումը մեկ տեղից փոխեց մեր աշխատանքը։",
             "Alle Domains und Hosting-Pakete unserer Kunden an einem Ort zu verwalten, hat unsere Arbeitsweise verändert.",
             "Gestionar los dominios y el alojamiento de todos nuestros clientes desde un solo lugar ha cambiado nuestra forma de trabajar."]),
    ];

    /// <summary>The partners: the name shown, in five languages.</summary>
    private static readonly string[][] Partners =
    [
        ["Alderstone Group", "Олдерстоун Груп", "Օլդերսթոուն Գրուպ", "Alderstone Group", "Alderstone Group"],
        ["Beacon Labs", "Бикон Лабс", "Բիկոն Լաբս", "Beacon Labs", "Beacon Labs"],
        ["Cobalt Works", "Кобальт Воркс", "Կոբալտ Ուորքս", "Cobalt Works", "Cobalt Works"],
        ["Delta Studio", "Дельта Студия", "Դելտա Ստուդիա", "Delta Studio", "Delta Studio"],
        ["Ember Foods", "Эмбер Фудс", "Էմբեր Ֆուդս", "Ember Foods", "Ember Foods"],
        ["Fjord Logistics", "Фьорд Логистика", "Ֆյորդ Լոգիստիկա", "Fjord Logistics", "Fjord Logistics"],
        ["Granite Bank", "Гранит Банк", "Գրանիտ Բանկ", "Granite Bank", "Granite Bank"],
        ["Harbor Media", "Харбор Медиа", "Հարբոր Մեդիա", "Harbor Media", "Harbor Media"],
        ["Irislane Analytics", "Айрислейн Аналитикс", "Այրիսլեյն Անալիտիքս", "Irislane Analytics", "Irislane Analytics"],
        ["Jadeline Cloud", "Джейдлайн Клауд", "Ջեյդլայն Քլաուդ", "Jadeline Cloud", "Jadeline Cloud"],
        ["Vantor Security", "Вантор Секьюрити", "Վանտոր Սեքյուրիթի", "Vantor Security", "Vantor Security"],
        ["Lumora Networks", "Люмора Нетворкс", "Լյումորա Նեթվորքս", "Lumora Networks", "Lumora Networks"],
        ["Marlowe Payments", "Марлоу Пейментс", "Մարլոու Փեյմենթս", "Marlowe Payments", "Marlowe Payments"],
        ["Nimbusline Data", "Нимбуслайн Дата", "Նիմբուսլայն Դատա", "Nimbusline Data", "Nimbusline Data"],
        ["Orchid Commerce", "Орхид Коммерс", "Օրքիդ Քոմերս", "Orchid Commerce", "Orchid Commerce"],
        ["Pinnacle Labs", "Пиннакл Лабс", "Փինաքլ Լաբս", "Pinnacle Labs", "Pinnacle Labs"],
    ];

    /// <summary>
    /// The server locations: ISO country code, then the data-centre name, the city and the latency note in five
    /// languages. The data centres are invented; the countries and cities are real places.
    /// </summary>
    private static readonly DemoServerLocation[] ServerLocations =
    [
        new("US", ["Atlantic Gate DC1", "Атлантик Гейт DC1", "Ատլանտիկ Գեյթ DC1", "Atlantic Gate DC1", "Atlantic Gate DC1"],
            ["Ashburn", "Ашберн", "Աշբերն", "Ashburn", "Ashburn"],
            ["About 8 ms from the US east coast", "Около 8 мс до восточного побережья США", "Մոտ 8 մվ ԱՄՆ արևելյան ափից", "Etwa 8 ms von der Ostküste der USA", "Unos 8 ms de la costa este de EE. UU."]),
        new("DE", ["Rhine Core DC2", "Райн Кор DC2", "Ռայն Կոր DC2", "Rhine Core DC2", "Rhine Core DC2"],
            ["Frankfurt", "Франкфурт", "Ֆրանկֆուրտ", "Frankfurt", "Fráncfort"],
            ["About 12 ms from central Europe", "Около 12 мс до центральной Европы", "Մոտ 12 մվ Կենտրոնական Եվրոպայից", "Etwa 12 ms von Mitteleuropa", "Unos 12 ms de Europa central"]),
        new("NL", ["Canal Point DC3", "Канал Поинт DC3", "Քենալ Փոյնթ DC3", "Canal Point DC3", "Canal Point DC3"],
            ["Amsterdam", "Амстердам", "Ամստերդամ", "Amsterdam", "Ámsterdam"],
            ["About 10 ms from western Europe", "Около 10 мс до западной Европы", "Մոտ 10 մվ Արևմտյան Եվրոպայից", "Etwa 10 ms von Westeuropa", "Unos 10 ms de Europa occidental"]),
        new("PL", ["Vistula Gate DC4", "Висла Гейт DC4", "Վիստուլա Գեյթ DC4", "Vistula Gate DC4", "Vistula Gate DC4"],
            ["Warsaw", "Варшава", "Վարշավա", "Warschau", "Varsovia"],
            ["About 8 ms from eastern Europe", "Около 8 мс до Восточной Европы", "Մոտ 8 մվ Արևելյան Եվրոպայից", "Etwa 8 ms von Osteuropa", "Unos 8 ms de Europa oriental"]),
        new("SG", ["Merlion Bay DC5", "Мерлион Бэй DC5", "Մերլիոն Բեյ DC5", "Merlion Bay DC5", "Merlion Bay DC5"],
            ["Singapore", "Сингапур", "Սինգապուր", "Singapur", "Singapur"],
            ["About 15 ms from south-east Asia", "Около 15 мс до Юго-Восточной Азии", "Մոտ 15 մվ Հարավարևելյան Ասիայից", "Etwa 15 ms von Südostasien", "Unos 15 ms del sudeste asiático"]),
        new("FI", ["Aurora Fjord DC6", "Аврора Фьорд DC6", "Աուրորա Ֆյորդ DC6", "Aurora Fjord DC6", "Aurora Fjord DC6"],
            ["Helsinki", "Хельсинки", "Հելսինկի", "Helsinki", "Helsinki"],
            ["About 14 ms from northern Europe", "Около 14 мс до Северной Европы", "Մոտ 14 մվ Հյուսիսային Եվրոպայից", "Etwa 14 ms von Nordeuropa", "Unos 14 ms del norte de Europa"]),
    ];

    /// <summary>Folder, under the client's public assets, that holds the partner logo files named after the partner slug.</summary>
    private const string PartnerLogoFolder = "/demo/partners/";

    /// <summary>Folder, under the client's public assets, that holds the project preview files named after the project slug.</summary>
    private const string ProjectImageFolder = "/demo/projects/";

    /// <summary>The projects: slug, then the title and the one-line summary in five languages.</summary>
    private static readonly DemoProject[] Projects =
    [
        new("northwind-portal", ["Northwind Portal", "Портал Northwind", "Northwind պորտալ", "Northwind Portal", "Northwind Portal"],
            ["A customer portal with online payments and self-service requests.",
             "Клиентский портал с онлайн-оплатой и самообслуживанием.",
             "Հաճախորդների պորտալ առցանց վճարումներով և ինքնասպասարկմամբ։",
             "Ein Kundenportal mit Online-Zahlungen und Self-Service-Anfragen.",
             "Un portal de clientes con pagos en línea y gestiones de autoservicio."]),
        new("bluebird-shop", ["Bluebird Shop", "Магазин Bluebird", "Bluebird խանութ", "Bluebird Shop", "Bluebird Shop"],
            ["An online store with inventory sync and a fast checkout.",
             "Интернет-магазин с синхронизацией склада и быстрым оформлением заказа.",
             "Առցանց խանութ պահեստի համաժամեցմամբ և արագ վճարմամբ։",
             "Ein Onlineshop mit Bestandsabgleich und schnellem Checkout.",
             "Una tienda en línea con sincronización de inventario y un proceso de pago rápido."]),
        new("harborline-tracking", ["Harborline Tracking", "Трекинг Harborline", "Harborline հետևում", "Harborline Tracking", "Harborline Tracking"],
            ["A shipment tracking dashboard for dispatchers and clients.",
             "Панель отслеживания грузов для диспетчеров и клиентов.",
             "Բեռների հետևման վահանակ դիսպետչերների և հաճախորդների համար։",
             "Ein Dashboard zur Sendungsverfolgung für Disponenten und Kunden.",
             "Un panel de seguimiento de envíos para despachadores y clientes."]),
        new("fjordly-menu", ["Fjordly Menu", "Меню Fjordly", "Fjordly մենյու", "Fjordly Menu", "Fjordly Menu"],
            ["A restaurant site with a live menu and table booking.",
             "Сайт ресторана с живым меню и бронированием столиков.",
             "Ռեստորանի կայք կենդանի մենյուով և սեղանների ամրագրմամբ։",
             "Eine Restaurant-Website mit aktueller Speisekarte und Tischreservierung.",
             "Un sitio de restaurante con menú actualizado y reserva de mesas."]),
        new("granite-library", ["Granite Library", "Библиотека Granite", "Granite գրադարան", "Granite Library", "Granite Library"],
            ["A searchable document library with access control.",
             "Библиотека документов с поиском и разграничением доступа.",
             "Փաստաթղթերի գրադարան որոնմամբ և հասանելիության վերահսկմամբ։",
             "Eine durchsuchbare Dokumentenbibliothek mit Zugriffskontrolle.",
             "Una biblioteca de documentos con búsqueda y control de acceso."]),
        new("emberwood-magazine", ["Emberwood Magazine", "Журнал Emberwood", "Emberwood ամսագիր", "Emberwood Magazine", "Emberwood Magazine"],
            ["A publishing site with subscriptions and a reader archive.",
             "Издательский сайт с подписками и архивом для читателей.",
             "Հրատարակչական կայք բաժանորդագրություններով և ընթերցողների արխիվով։",
             "Eine Verlagswebsite mit Abonnements und Leserarchiv.",
             "Un sitio editorial con suscripciones y archivo para lectores."]),
        new("copperfield-booking", ["Copperfield Booking", "Запись Copperfield", "Copperfield ամրագրում", "Copperfield Booking", "Copperfield Booking"],
            ["An appointment booking system with reminders by mail and text.",
             "Система записи на приём с напоминаниями по почте и SMS.",
             "Ընդունելության ամրագրման համակարգ՝ հիշեցումներով էլ. փոստով և SMS-ով։",
             "Ein Terminbuchungssystem mit Erinnerungen per E-Mail und SMS.",
             "Un sistema de reserva de citas con recordatorios por correo y SMS."]),
        new("silverpine-store", ["Silverpine Store", "Магазин Silverpine", "Silverpine խանութ", "Silverpine Store", "Silverpine Store"],
            ["A high-traffic fashion store with product filters and gift cards.",
             "Нагруженный магазин одежды с фильтрами товаров и подарочными картами.",
             "Բարձր այցելությամբ հագուստի խանութ՝ ապրանքների զտիչներով և նվեր քարտերով։",
             "Ein stark frequentierter Modeshop mit Produktfiltern und Geschenkkarten.",
             "Una tienda de moda de gran afluencia con filtros de productos y tarjetas regalo."]),
        new("riverbend-docs", ["Riverbend Docs", "Документация Riverbend", "Riverbend փաստաթղթեր", "Riverbend Docs", "Riverbend Docs"],
            ["A developer documentation site with versioned guides and search.",
             "Сайт документации для разработчиков с версиями руководств и поиском.",
             "Մշակողների փաստաթղթերի կայք՝ տարբերակված ուղեցույցներով և որոնմամբ։",
             "Eine Entwicklerdokumentation mit versionierten Anleitungen und Suche.",
             "Un sitio de documentación para desarrolladores con guías versionadas y búsqueda."]),
        new("meadowlark-donations", ["Meadowlark Donations", "Пожертвования Meadowlark", "Meadowlark նվիրատվություններ", "Meadowlark Donations", "Meadowlark Donations"],
            ["A charity site with recurring donations and a volunteer sign-up form.",
             "Благотворительный сайт с регулярными пожертвованиями и анкетой волонтёра.",
             "Բարեգործական կայք՝ պարբերական նվիրատվություններով և կամավորի գրանցման ձևով։",
             "Eine Spendenwebsite mit regelmäßigen Spenden und Anmeldeformular für Freiwillige.",
             "Un sitio benéfico con donaciones periódicas y un formulario de inscripción para voluntarios."]),
        new("sunpeak-tours", ["Sunpeak Tours", "Туры Sunpeak", "Sunpeak տուրեր", "Sunpeak Tours", "Sunpeak Tours"],
            ["A multilingual travel site with tour search and online deposits.",
             "Многоязычный туристический сайт с поиском туров и онлайн-предоплатой.",
             "Բազմալեզու զբոսաշրջային կայք՝ տուրերի որոնմամբ և առցանց կանխավճարով։",
             "Eine mehrsprachige Reisewebsite mit Tour-Suche und Online-Anzahlung.",
             "Un sitio de viajes multilingüe con búsqueda de tours y depósitos en línea."]),
        new("bosphorus-portfolio", ["Bosphorus Portfolio", "Портфолио Bosphorus", "Bosphorus պորտֆոլիո", "Bosphorus Portfolio", "Bosphorus Portfolio"],
            ["A design studio portfolio with case studies and a client area.",
             "Портфолио дизайн-студии с кейсами и кабинетом клиента.",
             "Դիզայն ստուդիայի պորտֆոլիո՝ դեպքերի ուսումնասիրություններով և հաճախորդի բաժնով։",
             "Ein Portfolio eines Designstudios mit Fallstudien und Kundenbereich.",
             "Un portfolio de estudio de diseño con casos de estudio y área de clientes."]),
    ];

    /// <summary>The questions: category, then the question and the answer in five languages.</summary>
    private static readonly DemoFaq[] Faqs =
    [
        new(FaqCategory.General, ["What does the platform offer?", "Что предлагает платформа?", "Ի՞նչ է առաջարկում հարթակը", "Was bietet die Plattform?", "¿Qué ofrece la plataforma?"],
            ["Hosting, domains, servers and a client portal for ordering, billing and support.",
             "Хостинг, домены, серверы и клиентский портал для заказов, оплаты и поддержки.",
             "Հոսթինգ, դոմեյններ, սերվերներ և հաճախորդի պորտալ պատվերների, վճարումների և աջակցության համար։",
             "Hosting, Domains, Server und ein Kundenportal für Bestellung, Abrechnung und Support.",
             "Alojamiento, dominios, servidores y un portal de clientes para pedidos, facturación y soporte."]),
        new(FaqCategory.General, ["How do I get started?", "Как начать?", "Ինչպե՞ս սկսել", "Wie fange ich an?", "¿Cómo empiezo?"],
            ["Choose a plan, create an account and pay the first invoice. Most services activate automatically.",
             "Выберите тариф, создайте аккаунт и оплатите первый счёт. Большинство услуг включается автоматически.",
             "Ընտրեք փաթեթ, ստեղծեք հաշիվ և վճարեք առաջին հաշիվը։ Ծառայությունների մեծ մասն ակտիվանում է ինքնաբերաբար։",
             "Wählen Sie einen Tarif, erstellen Sie ein Konto und bezahlen Sie die erste Rechnung. Die meisten Dienste werden automatisch aktiviert.",
             "Elija un plan, cree una cuenta y pague la primera factura. La mayoría de los servicios se activan automáticamente."]),
        new(FaqCategory.General, ["Which payment methods do you accept?", "Какие способы оплаты вы принимаете?", "Ի՞նչ վճարման եղանակներ եք ընդունում", "Welche Zahlungsmethoden akzeptieren Sie?", "¿Qué métodos de pago aceptan?"],
            ["Cards and the other methods enabled at checkout.",
             "Карты и другие способы, доступные при оформлении заказа.",
             "Քարտեր և վճարման այլ եղանակներ, որոնք հասանելի են պատվերի ժամանակ։",
             "Karten und die weiteren Methoden, die an der Kasse verfügbar sind.",
             "Tarjetas y los demás métodos habilitados al finalizar la compra."]),
        new(FaqCategory.General, ["How does billing work?", "Как устроено выставление счетов?", "Ինչպե՞ս են ձևավորվում հաշիվները", "Wie funktioniert die Abrechnung?", "¿Cómo funciona la facturación?"],
            ["Services are billed per cycle. Invoices are issued ahead of the due date and reminders are emailed.",
             "Услуги оплачиваются по циклам. Счета выставляются заранее, напоминания приходят по почте.",
             "Ծառայությունները վճարվում են ցիկլերով։ Հաշիվները ձևավորվում են նախօրոք, հիշեցումները՝ էլ. փոստով։",
             "Dienste werden pro Abrechnungszeitraum berechnet. Rechnungen werden vor dem Fälligkeitsdatum gestellt, Erinnerungen kommen per E-Mail.",
             "Los servicios se facturan por ciclo. Las facturas se emiten antes del vencimiento y los recordatorios se envían por correo electrónico."]),
        new(FaqCategory.General, ["How do I contact support?", "Как связаться с поддержкой?", "Ինչպե՞ս կապվել աջակցության հետ", "Wie erreiche ich den Support?", "¿Cómo me pongo en contacto con el soporte?"],
            ["Open a ticket from the client portal or use the contact page.",
             "Откройте тикет в клиентском портале или воспользуйтесь страницей контактов.",
             "Բացեք հարցում հաճախորդի պորտալից կամ օգտվեք կապի էջից։",
             "Eröffnen Sie ein Ticket im Kundenportal oder nutzen Sie die Kontaktseite.",
             "Abra un ticket desde el portal de clientes o utilice la página de contacto."]),
        new(FaqCategory.General, ["Can I cancel a service?", "Можно ли отменить услугу?", "Կարո՞ղ եմ չեղարկել ծառայությունը", "Kann ich einen Dienst kündigen?", "¿Puedo cancelar un servicio?"],
            ["Yes. Request cancellation from the service page and it ends at the close of the paid period.",
             "Да. Запросите отмену на странице услуги, она завершится в конце оплаченного периода.",
             "Այո։ Չեղարկումը պահանջեք ծառայության էջից, այն կավարտվի վճարված ժամանակահատվածի վերջում։",
             "Ja. Beantragen Sie die Kündigung auf der Dienstseite; sie endet mit Ablauf des bezahlten Zeitraums.",
             "Sí. Solicite la cancelación desde la página del servicio y finalizará al término del periodo pagado."]),
        new(FaqCategory.Products, ["Can I move my existing site?", "Можно ли перенести существующий сайт?", "Կարո՞ղ եմ տեղափոխել առկա կայքը", "Kann ich meine bestehende Website umziehen?", "¿Puedo trasladar mi sitio actual?"],
            ["Yes. Migration assistance is available for sites, databases and mail; open a ticket to plan it.",
             "Да. Доступна помощь с переносом сайтов, баз данных и почты; откройте тикет, чтобы спланировать его.",
             "Այո։ Հասանելի է կայքերի, տվյալների բազաների և փոստի տեղափոխման աջակցություն. բացեք հարցում՝ այն պլանավորելու համար։",
             "Ja. Für Websites, Datenbanken und E-Mails steht Umzugshilfe bereit; eröffnen Sie ein Ticket, um ihn zu planen.",
             "Sí. Ofrecemos ayuda para migrar sitios, bases de datos y correo; abra un ticket para planificarla."]),
        new(FaqCategory.Products, ["How do I register or transfer a domain?", "Как зарегистрировать или перенести домен?", "Ինչպե՞ս գրանցել կամ տեղափոխել դոմեյն", "Wie registriere oder transferiere ich eine Domain?", "¿Cómo registro o transfiero un dominio?"],
            ["Search for the name on the domains page and add it to your cart. To transfer, provide the authorization code.",
             "Найдите имя на странице доменов и добавьте в корзину. Для переноса укажите код авторизации.",
             "Փնտրեք անունը դոմեյնների էջում և ավելացրեք զամբյուղ։ Տեղափոխման համար նշեք թույլտվության կոդը։",
             "Suchen Sie den Namen auf der Domain-Seite und legen Sie ihn in den Warenkorb. Für einen Transfer geben Sie den Autorisierungscode an.",
             "Busque el nombre en la página de dominios y añádalo a su carrito. Para transferirlo, indique el código de autorización."]),
        new(FaqCategory.Products, ["Is an SSL certificate included?", "Входит ли SSL-сертификат?", "Ներառվա՞ծ է SSL վկայագիրը", "Ist ein SSL-Zertifikat enthalten?", "¿Se incluye un certificado SSL?"],
            ["Yes, a free SSL certificate is included in every hosting plan.",
             "Да, бесплатный SSL-сертификат входит в каждый тариф хостинга.",
             "Այո, անվճար SSL վկայագիրը ներառված է հոսթինգի յուրաքանչյուր փաթեթում։",
             "Ja, ein kostenloses SSL-Zertifikat ist in jedem Hosting-Tarif enthalten.",
             "Sí, todos los planes de alojamiento incluyen un certificado SSL gratuito."]),
        new(FaqCategory.Products, ["Can I upgrade my plan later?", "Можно ли сменить тариф позже?", "Կարո՞ղ եմ հետո փոխել փաթեթը", "Kann ich meinen Tarif später upgraden?", "¿Puedo mejorar mi plan más adelante?"],
            ["Yes. Upgrade a service from the client portal at any time.",
             "Да. Повысить тариф можно в клиентском портале в любое время.",
             "Այո։ Ծառայությունը կարելի է թարմացնել հաճախորդի պորտալից ցանկացած պահի։",
             "Ja. Sie können einen Dienst jederzeit im Kundenportal upgraden.",
             "Sí. Puede mejorar un servicio desde el portal de clientes en cualquier momento."]),
        new(FaqCategory.Products, ["Are backups included?", "Входят ли резервные копии?", "Ներառվա՞ծ են պահուստային պատճենները", "Sind Backups enthalten?", "¿Se incluyen copias de seguridad?"],
            ["Daily backups are included in every hosting plan.",
             "Ежедневные резервные копии входят в каждый тариф хостинга.",
             "Օրական պահուստային պատճենները ներառված են հոսթինգի յուրաքանչյուր փաթեթում։",
             "Tägliche Backups sind in jedem Hosting-Tarif enthalten.",
             "Todos los planes de alojamiento incluyen copias de seguridad diarias."]),
        new(FaqCategory.Products, ["Do you offer dedicated servers?", "Есть ли выделенные серверы?", "Առաջարկո՞ւմ եք առանձնացված սերվերներ", "Bieten Sie dedizierte Server an?", "¿Ofrecen servidores dedicados?"],
            ["Yes, virtual and dedicated servers with full root access and monitoring.",
             "Да, виртуальные и выделенные серверы с полным root-доступом и мониторингом.",
             "Այո, վիրտուալ և առանձնացված սերվերներ՝ լիարժեք root հասանելիությամբ և մոնիթորինգով։",
             "Ja, virtuelle und dedizierte Server mit vollem Root-Zugriff und Monitoring.",
             "Sí, servidores virtuales y dedicados con acceso root completo y monitorización."]),
        new(FaqCategory.General, ["Is there a money-back guarantee?", "Есть ли гарантия возврата денег?", "Կա՞ գումարի վերադարձի երաշխիք", "Gibt es eine Geld-zurück-Garantie?", "¿Hay garantía de devolución del dinero?"],
            ["Yes. Hosting plans can be cancelled within thirty days for a full refund.",
             "Да. Тарифы хостинга можно отменить в течение тридцати дней с полным возвратом.",
             "Այո։ Հոսթինգի փաթեթները կարելի է չեղարկել երեսուն օրվա ընթացքում՝ ամբողջական վերադարձով։",
             "Ja. Hosting-Tarife können innerhalb von dreißig Tagen gegen volle Erstattung gekündigt werden.",
             "Sí. Los planes de alojamiento pueden cancelarse en un plazo de treinta días con reembolso completo."]),
        new(FaqCategory.General, ["Can I pay annually?", "Можно ли платить за год?", "Կարո՞ղ եմ վճարել տարեկան", "Kann ich jährlich zahlen?", "¿Puedo pagar anualmente?"],
            ["Yes. Every plan has an annual price that saves about two months compared with monthly billing.",
             "Да. У каждого тарифа есть годовая цена, которая экономит около двух месяцев.",
             "Այո։ Յուրաքանչյուր փաթեթ ունի տարեկան գին, որը խնայում է մոտ երկու ամիս։",
             "Ja. Jeder Tarif hat einen Jahrespreis, der gegenüber monatlicher Zahlung etwa zwei Monate spart.",
             "Sí. Cada plan tiene un precio anual que ahorra unos dos meses frente a la facturación mensual."]),
        new(FaqCategory.General, ["Which currencies can I pay in?", "В какой валюте можно платить?", "Ի՞նչ արժույթով կարող եմ վճարել", "In welchen Währungen kann ich zahlen?", "¿En qué monedas puedo pagar?"],
            ["Dollars and euros. Choose the currency when you create the account.",
             "В долларах и евро. Валюту выбирают при создании аккаунта.",
             "Դոլարով և եվրոյով։ Արժույթն ընտրվում է հաշիվ ստեղծելիս։",
             "In Dollar und Euro. Die Währung wählen Sie bei der Kontoerstellung.",
             "En dólares y en euros. Elija la moneda al crear la cuenta."]),
        new(FaqCategory.General, ["Do you send invoices by email?", "Отправляете ли вы счета по почте?", "Հաշիվներն ուղարկո՞ւմ եք էլ. փոստով", "Versenden Sie Rechnungen per E-Mail?", "¿Envían las facturas por correo electrónico?"],
            ["Yes. Every invoice is emailed and is also available to download in the client portal.",
             "Да. Каждый счёт приходит по почте и доступен для скачивания в клиентском портале.",
             "Այո։ Յուրաքանչյուր հաշիվ ուղարկվում է էլ. փոստով և հասանելի է ներբեռնման համար հաճախորդի պորտալում։",
             "Ja. Jede Rechnung wird per E-Mail verschickt und steht außerdem im Kundenportal zum Download bereit.",
             "Sí. Cada factura se envía por correo electrónico y también puede descargarse desde el portal de clientes."]),
        new(FaqCategory.General, ["Can several people manage one account?", "Могут ли несколько человек управлять одним аккаунтом?", "Կարո՞ղ են մի քանի հոգի կառավարել մեկ հաշիվ", "Können mehrere Personen ein Konto verwalten?", "¿Pueden varias personas gestionar una misma cuenta?"],
            ["Yes. Invite team members and choose what each of them may see and do.",
             "Да. Пригласите участников команды и выберите, что каждый может видеть и делать.",
             "Այո։ Հրավիրեք թիմի անդամներին և ընտրեք, թե յուրաքանչյուրն ինչ կարող է տեսնել և անել։",
             "Ja. Laden Sie Teammitglieder ein und legen Sie fest, was jeder sehen und tun darf.",
             "Sí. Invite a los miembros de su equipo y elija lo que cada uno puede ver y hacer."]),
        new(FaqCategory.General, ["Where are your data centres?", "Где находятся ваши дата-центры?", "Որտե՞ղ են գտնվում ձեր տվյալների կենտրոնները", "Wo befinden sich Ihre Rechenzentren?", "¿Dónde están sus centros de datos?"],
            ["In Europe and North America. Choose the location when you order a server.",
             "В Европе и Северной Америке. Расположение выбирается при заказе сервера.",
             "Եվրոպայում և Հյուսիսային Ամերիկայում։ Վայրն ընտրվում է սերվեր պատվիրելիս։",
             "In Europa und Nordamerika. Den Standort wählen Sie bei der Serverbestellung.",
             "En Europa y Norteamérica. Elija la ubicación al pedir un servidor."]),
        new(FaqCategory.Products, ["Can I host several sites on one plan?", "Можно ли разместить несколько сайтов на одном тарифе?", "Կարո՞ղ եմ մեկ փաթեթում տեղադրել մի քանի կայք", "Kann ich mehrere Websites in einem Tarif hosten?", "¿Puedo alojar varios sitios en un mismo plan?"],
            ["Yes. Business Hosting allows ten sites and Pro Hosting allows unlimited sites.",
             "Да. Business Hosting вмещает десять сайтов, а Pro Hosting без ограничений.",
             "Այո։ Business Hosting-ը թույլ է տալիս տասը կայք, իսկ Pro Hosting-ը՝ անսահմանափակ։",
             "Ja. Business Hosting erlaubt zehn Websites, Pro Hosting unbegrenzt viele.",
             "Sí. Business Hosting admite diez sitios y Pro Hosting, sitios ilimitados."]),
        new(FaqCategory.Products, ["Do you offer managed site hosting?", "Есть ли управляемый хостинг сайтов?", "Առաջարկո՞ւմ եք կառավարվող կայքերի հոսթինգ", "Bieten Sie Managed Hosting für Websites an?", "¿Ofrecen alojamiento web gestionado?"],
            ["Yes. Managed Site Hosting keeps the site engine and its plugins up to date for you.",
             "Да. Тариф Managed Site Hosting сам обновляет движок сайта и плагины.",
             "Այո։ Managed Site Hosting-ը ինքն է թարմացնում կայքի շարժիչը և հավելումները։",
             "Ja. Managed Site Hosting hält die Website-Software und ihre Plugins für Sie aktuell.",
             "Sí. Managed Site Hosting mantiene actualizados por usted el motor del sitio y sus complementos."]),
        new(FaqCategory.Products, ["Can I take snapshots of my VPS?", "Можно ли делать снимки VPS?", "Կարո՞ղ եմ VPS-ի պատկերներ ստեղծել", "Kann ich Snapshots meines VPS erstellen?", "¿Puedo crear instantáneas de mi VPS?"],
            ["Yes. Every VPS can keep up to three snapshots at no extra cost.",
             "Да. Каждый VPS может хранить до трёх снимков без доплаты.",
             "Այո։ Յուրաքանչյուր VPS կարող է պահել մինչև երեք պատկեր՝ առանց հավելավճարի։",
             "Ja. Jeder VPS kann bis zu drei Snapshots ohne Aufpreis speichern.",
             "Sí. Cada VPS puede conservar hasta tres instantáneas sin coste adicional."]),
        new(FaqCategory.Products, ["What is WHOIS privacy?", "Что такое скрытие WHOIS?", "Ի՞նչ է WHOIS գաղտնիությունը", "Was ist WHOIS-Datenschutz?", "¿Qué es la privacidad WHOIS?"],
            ["It keeps your contact details out of the public domain record and forwards mail to you.",
             "Оно скрывает ваши контакты в публичной записи домена и пересылает вам почту.",
             "Այն թաքցնում է ձեր կոնտակտային տվյալները դոմեյնի հրապարակային գրառումից և փոստը վերահասցեավորում ձեզ։",
             "Er hält Ihre Kontaktdaten aus dem öffentlichen Domain-Eintrag heraus und leitet Post an Sie weiter.",
             "Mantiene sus datos de contacto fuera del registro público del dominio y le reenvía el correo."]),
        new(FaqCategory.Products, ["Which certificate should I choose?", "Какой сертификат выбрать?", "Ո՞ր վկայագիրն ընտրեմ", "Welches Zertifikat sollte ich wählen?", "¿Qué certificado debo elegir?"],
            ["Basic suits one site, Wildcard covers every subdomain and Extended shows your verified organisation.",
             "Basic подходит для одного сайта, Wildcard покрывает все поддомены, а Extended показывает проверенную организацию.",
             "Basic-ը հարմար է մեկ կայքի համար, Wildcard-ը ծածկում է բոլոր ենթադոմեյնները, իսկ Extended-ը ցույց է տալիս ստուգված կազմակերպությունը։",
             "Basic eignet sich für eine Website, Wildcard deckt alle Subdomains ab und Extended zeigt Ihre geprüfte Organisation an.",
             "Basic sirve para un sitio, Wildcard cubre todos los subdominios y Extended muestra su organización verificada."]),
        new(FaqCategory.Products, ["Can I add more mailboxes later?", "Можно ли добавить почтовые ящики позже?", "Կարո՞ղ եմ հետո ավելացնել փոստարկղեր", "Kann ich später weitere Postfächer hinzufügen?", "¿Puedo añadir más buzones más adelante?"],
            ["Yes. Upgrade the mail plan at any time and the new mailboxes are ready at once.",
             "Да. Повысьте почтовый тариф в любое время, и новые ящики сразу готовы.",
             "Այո։ Փոստի փաթեթը կարելի է թարմացնել ցանկացած պահի, և նոր փոստարկղերը անմիջապես պատրաստ են։",
             "Ja. Sie können den Mail-Tarif jederzeit upgraden, und die neuen Postfächer sind sofort bereit.",
             "Sí. Mejore el plan de correo en cualquier momento y los nuevos buzones estarán listos al instante."]),
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

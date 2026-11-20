using Innovayse.Hostpanel.Domain.Slides;
using Innovayse.Hostpanel.Domain.Slides.Enums;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// Seeds the storefront and client-area content: homepage slides with English, Russian, Armenian, German and Spanish text,
/// and the downloads library with its categories. Everything is built through the entities' factories and
/// behaviour methods, added only when its table is empty.
/// </summary>
internal sealed class DemoContentSeeder
{
    /// <summary>The call-to-action text per locale, in the order en, ru, hy, de, es.</summary>
    private static readonly string[] CallToAction = ["Get Started", "Начать", "Սկսել", "Jetzt starten", "Comenzar"];

    /// <summary>The locales every slide is translated into, in the order the tables below use.</summary>
    private static readonly string[] Locales = ["en", "ru", "hy", "de", "es"];

    /// <summary>The storefront folder the bundled slide covers are served from.</summary>
    private const string SlideImageFolder = "/demo/slides/";

    /// <summary>The data context the rows are added to and saved through.</summary>
    private readonly AppDbContext _db;

    /// <summary>The anchor dates are measured from.</summary>
    private readonly DateTimeOffset _today;

    /// <summary>Creates the seeder.</summary>
    /// <param name="db">The data context the rows are added to and saved through.</param>
    /// <param name="today">The anchor dates are measured from.</param>
    public DemoContentSeeder(AppDbContext db, DateTimeOffset today)
    {
        _db = db;
        _today = today;
    }

    /// <summary>The bundled cover address of a demo slide: its key under <see cref="SlideImageFolder"/>, as an SVG file.</summary>
    /// <param name="key">The slide's key, such as <c>cloud-vps</c>.</param>
    /// <returns>The site-relative address of the slide's cover picture.</returns>
    internal static string SlideImageUrl(string key)
    {
        return $"{SlideImageFolder}{key}.svg";
    }

    /// <summary>Seeds the slides and downloads that are not there yet.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    public async Task SeedAsync(CancellationToken ct)
    {
        if (!await _db.Slides.AnyAsync(ct))
        {
            await SeedSlidesAsync(ct);
        }

        if (!await _db.DownloadCategories.AnyAsync(ct))
        {
            await SeedDownloadsAsync(ct);
        }
    }

    /// <summary>Seeds seven slides, six live (one only for visitors who are not signed in, one with a visibility window) and one switched off.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedSlidesAsync(CancellationToken ct)
    {
        // Each slide carries an original illustration drawn for it (client/public/demo/slides), not a stock
        // photo that could be mistaken for a real customer's material; the storefront shows it as the slide's cover.
        (string Icon, string Color, string Image, string LearnMore, bool Active, SlideAudience Audience, bool Windowed, (string Title, string Tagline, string Description, string[] Features)[] Text)[] slides =
        [
            ("cloud-check", "#0ea5e9", SlideImageUrl("web-hosting"), "/hosting", true, SlideAudience.All, false,
            [
                ("Web Hosting", "Fast and reliable hosting", "Launch your website on high-performance servers with free SSL and daily backups.", ["Free SSL certificate", "99.9% uptime", "Daily backups", "24/7 support"]),
                ("Веб-хостинг", "Быстрый и надёжный хостинг", "Разместите сайт на производительных серверах с бесплатным SSL и ежедневными резервными копиями.", ["Бесплатный SSL", "99,9% аптайм", "Ежедневные копии", "Поддержка 24/7"]),
                ("Վեբ հոսթինգ", "Արագ և հուսալի հոսթինգ", "Տեղադրեք ձեր կայքը բարձր արտադրողականությամբ սերվերների վրա՝ անվճար SSL-ով և ամենօրյա պահուստավորմամբ։", ["Անվճար SSL", "99,9% աշխատանք", "Ամենօրյա պահուստ", "Աջակցություն 24/7"]),
                ("Webhosting", "Schnelles und zuverlässiges Hosting", "Starten Sie Ihre Website auf Hochleistungsservern mit kostenlosem SSL und täglichen Backups.", ["Kostenloses SSL-Zertifikat", "99,9 % Verfügbarkeit", "Tägliche Backups", "Support rund um die Uhr"]),
                ("Alojamiento web", "Hosting rápido y fiable", "Lance su sitio web en servidores de alto rendimiento con SSL gratuito y copias de seguridad diarias.", ["Certificado SSL gratuito", "99,9 % de disponibilidad", "Copias de seguridad diarias", "Soporte 24/7"]),
            ]),
            ("earth", "#3b82f6", SlideImageUrl("domains"), "/domains", true, SlideAudience.All, false,
            [
                ("Domain Registration", "Find and register your domain", "Search and register your domain name at competitive prices with instant activation.", ["Free DNS management", "WHOIS privacy", "Auto-renewal"]),
                ("Регистрация доменов", "Найдите и зарегистрируйте домен", "Ищите и регистрируйте домен по конкурентным ценам с мгновенной активацией.", ["Бесплатное управление DNS", "Скрытие данных WHOIS", "Автопродление"]),
                ("Դոմեյնների գրանցում", "Գտեք և գրանցեք ձեր դոմեյնը", "Որոնեք և գրանցեք ձեր դոմեյնը մրցունակ գներով՝ անմիջական ակտիվացմամբ։", ["Անվճար DNS կառավարում", "WHOIS գաղտնիություն", "Ավտոմատ երկարաձգում"]),
                ("Domain-Registrierung", "Finden und registrieren Sie Ihre Domain", "Suchen und registrieren Sie Ihren Domainnamen zu attraktiven Preisen mit sofortiger Aktivierung.", ["Kostenlose DNS-Verwaltung", "WHOIS-Datenschutz", "Automatische Verlängerung"]),
                ("Registro de dominios", "Encuentre y registre su dominio", "Busque y registre su nombre de dominio a precios competitivos con activación inmediata.", ["Gestión DNS gratuita", "Privacidad WHOIS", "Renovación automática"]),
            ]),
            ("server", "#6366f1", SlideImageUrl("cloud-vps"), "/products/cloud-vps", true, SlideAudience.All, false,
            [
                ("Cloud VPS", "Full control, instant setup", "Isolated virtual servers with root access and NVMe storage, ready in minutes.", ["Root access", "NVMe storage", "Instant setup", "Snapshots"]),
                ("Облачный VPS", "Полный контроль, мгновенный запуск", "Изолированные виртуальные серверы с root-доступом и NVMe-накопителями, готовые за минуты.", ["Root-доступ", "NVMe-диски", "Мгновенный запуск", "Снимки"]),
                ("Ամպային VPS", "Լիարժեք վերահսկողություն", "Մեկուսացված վիրտուալ սերվերներ root հասանելիությամբ և NVMe կրիչներով՝ պատրաստ րոպեների ընթացքում։", ["Root հասանելիություն", "NVMe կրիչ", "Արագ գործարկում", "Պատկերներ"]),
                ("Cloud-VPS", "Volle Kontrolle, sofort startklar", "Isolierte virtuelle Server mit Root-Zugriff und NVMe-Speicher, in wenigen Minuten bereit.", ["Root-Zugriff", "NVMe-Speicher", "Sofortige Bereitstellung", "Snapshots"]),
                ("VPS en la nube", "Control total, puesta en marcha inmediata", "Servidores virtuales aislados con acceso root y almacenamiento NVMe, listos en minutos.", ["Acceso root", "Almacenamiento NVMe", "Puesta en marcha inmediata", "Instantáneas"]),
            ]),
            ("shield-check", "#10b981", SlideImageUrl("ssl"), "/products/ssl-certificates", true, SlideAudience.Guest, false,
            [
                ("SSL Certificates", "Protect your site", "Certificates that encrypt the connection and build trust with your visitors.", ["Domain and organisation validation", "Wildcard option", "Warranty included"]),
                ("SSL-сертификаты", "Защитите свой сайт", "Сертификаты, которые шифруют соединение и повышают доверие посетителей.", ["Проверка домена и организации", "Wildcard-вариант", "Гарантия включена"]),
                ("SSL վկայագրեր", "Պաշտպանեք ձեր կայքը", "Վկայագրեր, որոնք գաղտնագրում են կապը և մեծացնում այցելուների վստահությունը։", ["Դոմեյնի և կազմակերպության ստուգում", "Wildcard տարբերակ", "Երաշխիք"]),
                ("SSL-Zertifikate", "Schützen Sie Ihre Website", "Zertifikate, die die Verbindung verschlüsseln und das Vertrauen Ihrer Besucher stärken.", ["Domain- und Organisationsvalidierung", "Wildcard-Option", "Garantie inklusive"]),
                ("Certificados SSL", "Proteja su sitio web", "Certificados que cifran la conexión y generan confianza en sus visitantes.", ["Validación de dominio y de organización", "Opción Wildcard", "Garantía incluida"]),
            ]),
            ("email", "#f59e0b", SlideImageUrl("email"), "/products/business-email", true, SlideAudience.All, true,
            [
                ("Business Email", "Mail on your own domain", "Professional mailboxes with spam protection and shared calendars.", ["Spam filter", "Shared calendars", "Mobile sync"]),
                ("Деловая почта", "Почта на вашем домене", "Профессиональные почтовые ящики с защитой от спама и общими календарями.", ["Защита от спама", "Общие календари", "Синхронизация с телефоном"]),
                ("Բիզնես փոստ", "Փոստ ձեր դոմեյնում", "Մասնագիտական փոստարկղեր սպամի պաշտպանությամբ և ընդհանուր օրացույցներով։", ["Սպամի զտիչ", "Ընդհանուր օրացույցներ", "Համաժամացում հեռախոսի հետ"]),
                ("Business-E-Mail", "E-Mail auf Ihrer eigenen Domain", "Professionelle Postfächer mit Spam-Schutz und gemeinsamen Kalendern.", ["Spam-Filter", "Gemeinsame Kalender", "Synchronisation mit dem Smartphone"]),
                ("Correo empresarial", "Correo en su propio dominio", "Buzones profesionales con protección antispam y calendarios compartidos.", ["Filtro antispam", "Calendarios compartidos", "Sincronización con el móvil"]),
            ]),
            ("server-network", "#ef4444", SlideImageUrl("dedicated"), "/products/dedicated-servers", true, SlideAudience.All, false,
            [
                ("Dedicated Servers", "Maximum power", "Single-tenant hardware for demanding workloads, with no noisy neighbours.", ["Up to 24 cores", "NVMe arrays", "Private network"]),
                ("Выделенные серверы", "Максимальная мощность", "Отдельное оборудование для требовательных проектов без соседей.", ["До 24 ядер", "Массивы NVMe", "Частная сеть"]),
                ("Նվիրված սերվերներ", "Առավելագույն հզորություն", "Առանձին սարքավորում պահանջկոտ ծրագրերի համար՝ առանց այլ օգտատերերի հետ կիսելու։", ["Մինչև 24 միջուկ", "NVMe զանգվածներ", "Մասնավոր ցանց"]),
                ("Dedizierte Server", "Maximale Leistung", "Exklusive Hardware für anspruchsvolle Projekte, ganz ohne störende Nachbarn.", ["Bis zu 24 Kerne", "NVMe-Arrays", "Privates Netzwerk"]),
                ("Servidores dedicados", "Máxima potencia", "Hardware exclusivo para cargas de trabajo exigentes, sin vecinos que molesten.", ["Hasta 24 núcleos", "Matrices NVMe", "Red privada"]),
            ]),
            ("tag", "#8b5cf6", SlideImageUrl("offers"), "/products", false, SlideAudience.All, false,
            [
                ("Seasonal Offer", "Switched off", "A seasonal offer that is prepared but not shown to visitors yet.", ["Prepared in advance"]),
                ("Сезонное предложение", "Отключено", "Сезонное предложение, которое подготовлено, но пока не показывается посетителям.", ["Подготовлено заранее"]),
                ("Սեզոնային առաջարկ", "Անջատված է", "Սեզոնային առաջարկ, որը պատրաստ է, բայց դեռ չի ցուցադրվում այցելուներին։", ["Նախապես պատրաստված"]),
                ("Saisonangebot", "Deaktiviert", "Ein Saisonangebot, das vorbereitet ist, aber Besuchern noch nicht angezeigt wird.", ["Im Voraus vorbereitet"]),
                ("Oferta de temporada", "Desactivada", "Una oferta de temporada que está preparada, pero que aún no se muestra a los visitantes.", ["Preparada con antelación"]),
            ]),
        ];

        for (var sortOrder = 0; sortOrder < slides.Length; sortOrder++)
        {
            var (icon, color, image, learnMore, active, audience, windowed, text) = slides[sortOrder];
            var slide = Slide.Create(new SlideSettings(
                icon, color, image, null, learnMore, null, sortOrder, active, audience,
                windowed ? _today.AddDays(-30) : null, windowed ? _today.AddDays(120) : null),
                _today);

            for (var l = 0; l < Locales.Length; l++)
            {
                var (title, tagline, description, features) = text[l];
                slide.SetTranslation(
                    Locales[l], title, tagline, description, features, CallToAction[l], learnMore);
            }

            _db.Slides.Add(slide);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Seeds download categories (one nested, one hidden) and twenty-one downloads with download counts.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedDownloadsAsync(CancellationToken ct)
    {
        var guides = DownloadCategory.Create("Guides", "Step-by-step manuals for our services.", false);
        var software = DownloadCategory.Create("Client Software", "Tools that make working with your services easier.", false);
        var internalDocs = DownloadCategory.Create("Internal Documents", "Documents kept for staff only.", true);
        _db.DownloadCategories.Add(guides);
        _db.DownloadCategories.Add(software);
        _db.DownloadCategories.Add(internalDocs);
        await _db.SaveChangesAsync(ct);

        var mail = DownloadCategory.Create("Mail Clients", "Ready-made settings for popular mail programs.", false, software.Id);
        _db.DownloadCategories.Add(mail);
        await _db.SaveChangesAsync(ct);

        (string Title, string Description, string Type, string File, int Category, bool ClientsOnly, bool Hidden, int Downloads)[] downloads =
        [
            ("Getting started guide", "A short tour of the client area and your first service.", "PDF Document", "getting-started.pdf", guides.Id, false, false, 128),
            ("Hosting control panel manual", "How to manage sites, databases and backups.", "PDF Document", "hosting-panel-manual.pdf", guides.Id, true, false, 76),
            ("DNS records cheat sheet", "What each record type does, with examples.", "PDF Document", "dns-cheat-sheet.pdf", guides.Id, false, false, 214),
            ("VPS security checklist", "Twelve steps to harden a new virtual server.", "PDF Document", "vps-security-checklist.pdf", guides.Id, true, false, 53),
            ("File transfer client settings", "Connection profile for the common transfer tools.", "ZIP File", "transfer-profile.zip", software.Id, true, false, 91),
            ("Mail client setup pack", "Account settings for desktop and mobile mail programs.", "ZIP File", "mail-setup-pack.zip", mail.Id, false, false, 167),
            ("Command line toolkit", "Scripts for backups and log rotation on your server.", "ZIP File", "cli-toolkit.zip", software.Id, true, false, 38),
            ("Service level agreement", "The uptime and support commitments in full.", "PDF Document", "service-level-agreement.pdf", guides.Id, false, false, 245),
            ("Email deliverability guide", "How SPF, DKIM and DMARC keep your mail out of spam folders.", "PDF Document", "deliverability-guide.pdf", guides.Id, false, false, 142),
            ("Domain transfer checklist", "Everything to prepare before moving a domain to us.", "PDF Document", "domain-transfer-checklist.pdf", guides.Id, false, false, 97),
            ("Backup and restore handbook", "Taking, keeping and restoring backups of sites and databases.", "PDF Document", "backup-handbook.pdf", guides.Id, true, false, 64),
            ("Reseller quick start", "Setting up packages and branding for your own clients.", "PDF Document", "reseller-quick-start.pdf", guides.Id, true, false, 29),
            ("SSL installation guide", "Installing and renewing certificates on every plan.", "PDF Document", "ssl-installation.pdf", guides.Id, false, false, 118),
            ("Mobile mail profiles", "Ready-made profiles for phones and tablets.", "ZIP File", "mobile-mail-profiles.zip", mail.Id, false, false, 83),
            ("Desktop mail settings", "Server settings for desktop mail programs.", "ZIP File", "desktop-mail-settings.zip", mail.Id, false, false, 71),
            ("Server monitoring agent", "A small agent that reports load and disk use from your VPS.", "ZIP File", "monitoring-agent.zip", software.Id, true, false, 46),
            ("Database export scripts", "Scripts that export and compress your databases on a schedule.", "ZIP File", "db-export-scripts.zip", software.Id, true, false, 33),
            ("Brand assets for resellers", "Neutral logos and banners resellers may use.", "ZIP File", "reseller-assets.zip", software.Id, true, false, 18),
            ("Acceptable use policy", "What may and may not be hosted on our services.", "PDF Document", "acceptable-use-policy.pdf", guides.Id, false, false, 156),
            ("Privacy notice", "How customer data is stored and processed.", "PDF Document", "privacy-notice.pdf", guides.Id, false, false, 132),
            ("Staff onboarding notes", "Internal notes, not shown to customers.", "PDF Document", "staff-onboarding.pdf", internalDocs.Id, true, true, 4),
        ];

        for (var n = 0; n < downloads.Length; n++)
        {
            var (title, description, type, file, category, clientsOnly, hidden, count) = downloads[n];
            var download = Download.Create(title, description, type, file, category, clientsOnly, false, hidden);
            for (var i = 0; i < count; i++)
            {
                download.IncrementDownloadCount();
            }

            // Published over the last year, each on its own day, not all at the moment of seeding.
            download.Backdate(_today.AddDays(-(20 + ((n * 17) % 330))));
            _db.Downloads.Add(download);
        }

        await _db.SaveChangesAsync(ct);
    }
}

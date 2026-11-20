using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Notifications;

namespace Innovayse.Hostpanel.Application.Notifications.Seeding;

/// <summary>
/// Seeds the Russian, Armenian, German and Spanish versions of the transactional templates whose English version the
/// schema seeds (welcome, invoice, payment, service and ticket mails), so a client who chose one of
/// those languages gets mail in it. Run at startup; a version already stored, edited or not, is
/// never touched.
/// </summary>
public static class TransactionalTemplatesSeeder
{
    /// <summary>Adds whichever translated version is missing.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once every version exists.</returns>
    public static Task EnsureSeededAsync(IHostpanelDbContext context, CancellationToken ct)
    {
        return EmailTemplateSeeding.AddMissingAsync(context, Versions(), ct);
    }

    /// <summary>The translated versions, with the staff-facing description of the English row.</summary>
    /// <returns>One template per slug and translated language.</returns>
    private static IReadOnlyList<EmailTemplate> Versions() =>
    [
        EmailTemplate.Create("welcome", "ru", "Добро пожаловать в {{ site.name }}!",
            "<h1>Добро пожаловать!</h1><p>Здравствуйте! Ваш аккаунт успешно создан.</p>",
            "Sent on client registration"),
        EmailTemplate.Create("welcome", "hy", "Բարի գալուստ {{ site.name }}",
            "<h1>Բարի գալուստ</h1><p>Բարև ձեզ, ձեր հաշիվը հաջողությամբ ստեղծվել է։</p>",
            "Sent on client registration"),
        EmailTemplate.Create("invoice-created", "ru", "Счёт №{{invoice.id}} создан",
            "<p>Ваш счёт на {{invoice.total}} {{invoice.currency}} готов.</p>",
            "Sent when invoice created"),
        EmailTemplate.Create("invoice-created", "hy", "№{{invoice.id}} հաշիվը ստեղծվել է",
            "<p>Ձեր {{invoice.total}} {{invoice.currency}} գումարի հաշիվը պատրաստ է։</p>",
            "Sent when invoice created"),
        EmailTemplate.Create("payment-received", "ru", "Платёж получен",
            "<p>Спасибо за оплату {{invoice.total}}.</p>",
            "Sent on payment"),
        EmailTemplate.Create("payment-received", "hy", "Վճարումը ստացվել է",
            "<p>Շնորհակալություն {{invoice.total}} վճարման համար։</p>",
            "Sent on payment"),
        EmailTemplate.Create("service-provisioned", "ru", "Ваша услуга готова",
            "<p>Ваша услуга хостинга подключена.</p>",
            "Sent when service provisioned"),
        EmailTemplate.Create("service-provisioned", "hy", "Ձեր ծառայությունը պատրաստ է",
            "<p>Ձեր հոսթինգ ծառայությունը միացված է։</p>",
            "Sent when service provisioned"),
        EmailTemplate.Create("ticket-created", "ru", "Обращение №{{ticket.id}} создано",
            "<p>Мы получили ваше обращение в поддержку.</p>",
            "Sent when ticket created"),
        EmailTemplate.Create("ticket-created", "hy", "№{{ticket.id}} հայտը ստեղծվել է",
            "<p>Մենք ստացել ենք ձեր հայտը աջակցության ծառայությանը։</p>",
            "Sent when ticket created"),
        EmailTemplate.Create("ticket-replied", "ru", "Новый ответ в обращении №{{ticket.id}}: {{ticket.subject}}",
            "<p>В вашем обращении в поддержку появился новый ответ.</p>",
            "Sent when a reply is added to a ticket"),
        EmailTemplate.Create("ticket-replied", "hy", "Նոր պատասխան №{{ticket.id}} հայտում՝ {{ticket.subject}}",
            "<p>Ձեր աջակցության հայտում նոր պատասխան կա։</p>",
            "Sent when a reply is added to a ticket"),
        EmailTemplate.Create("ticket-closed", "ru", "Обращение №{{ticket.id}} закрыто",
            "<p>Ваше обращение «{{ticket.subject}}» закрыто. Будем рады вашему отзыву.</p>",
            "Sent when a ticket is closed"),
        EmailTemplate.Create("ticket-closed", "hy", "№{{ticket.id}} հայտը փակվել է",
            "<p>Ձեր «{{ticket.subject}}» հայտը փակվել է։ Ուրախ կլինենք ստանալ ձեր կարծիքը։</p>",
            "Sent when a ticket is closed"),
        EmailTemplate.Create("welcome", "de", "Willkommen bei {{ site.name }}!",
            "<h1>Willkommen!</h1><p>Guten Tag! Ihr Konto wurde erfolgreich erstellt.</p>",
            "Sent on client registration"),
        EmailTemplate.Create("welcome", "es", "¡Bienvenido a {{ site.name }}!",
            "<h1>¡Bienvenido!</h1><p>Hola, su cuenta se ha creado correctamente.</p>",
            "Sent on client registration"),
        EmailTemplate.Create("invoice-created", "de", "Rechnung Nr. {{invoice.id}} wurde erstellt",
            "<p>Ihre Rechnung über {{invoice.total}} {{invoice.currency}} ist bereit.</p>",
            "Sent when invoice created"),
        EmailTemplate.Create("invoice-created", "es", "Factura n.º {{invoice.id}} creada",
            "<p>Su factura por {{invoice.total}} {{invoice.currency}} está lista.</p>",
            "Sent when invoice created"),
        EmailTemplate.Create("payment-received", "de", "Zahlung eingegangen",
            "<p>Vielen Dank für Ihre Zahlung von {{invoice.total}}.</p>",
            "Sent on payment"),
        EmailTemplate.Create("payment-received", "es", "Pago recibido",
            "<p>Gracias por su pago de {{invoice.total}}.</p>",
            "Sent on payment"),
        EmailTemplate.Create("service-provisioned", "de", "Ihr Dienst ist bereit",
            "<p>Ihr Hosting-Dienst wurde eingerichtet.</p>",
            "Sent when service provisioned"),
        EmailTemplate.Create("service-provisioned", "es", "Su servicio está listo",
            "<p>Su servicio de hosting ha sido activado.</p>",
            "Sent when service provisioned"),
        EmailTemplate.Create("ticket-created", "de", "Ticket Nr. {{ticket.id}} wurde erstellt",
            "<p>Wir haben Ihre Support-Anfrage erhalten.</p>",
            "Sent when ticket created"),
        EmailTemplate.Create("ticket-created", "es", "Ticket n.º {{ticket.id}} creado",
            "<p>Hemos recibido su solicitud de soporte.</p>",
            "Sent when ticket created"),
        EmailTemplate.Create("ticket-replied", "de", "Neue Antwort in Ticket Nr. {{ticket.id}}: {{ticket.subject}}",
            "<p>Es gibt eine neue Antwort in Ihrem Support-Ticket.</p>",
            "Sent when a reply is added to a ticket"),
        EmailTemplate.Create("ticket-replied", "es", "Nueva respuesta en el ticket n.º {{ticket.id}}: {{ticket.subject}}",
            "<p>Hay una nueva respuesta en su ticket de soporte.</p>",
            "Sent when a reply is added to a ticket"),
        EmailTemplate.Create("ticket-closed", "de", "Ticket Nr. {{ticket.id}} wurde geschlossen",
            "<p>Ihr Ticket „{{ticket.subject}}“ wurde geschlossen. Wir freuen uns über Ihr Feedback.</p>",
            "Sent when a ticket is closed"),
        EmailTemplate.Create("ticket-closed", "es", "Ticket n.º {{ticket.id}} cerrado",
            "<p>Su ticket «{{ticket.subject}}» ha sido cerrado. Agradeceremos sus comentarios.</p>",
            "Sent when a ticket is closed"),
    ];
}

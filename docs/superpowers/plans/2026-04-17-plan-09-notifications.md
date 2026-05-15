# Notifications Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Notifications module — EmailTemplate aggregate with Liquid template rendering, EmailLog entity for audit trail, IEmailSender interface with MailKit implementation, Wolverine event handlers for all domain events (welcome emails, invoice notifications, payment receipts, service credentials, expiry notices, ticket notifications), and admin CRUD for email templates.

**Architecture:** EmailTemplate stores templates with Liquid syntax; event handlers consume domain events from Wolverine outbox and dispatch emails via IEmailSender; MailKit sends emails with SMTP; EmailLog records all sent emails for compliance and debugging; templates support variables like `{{client.name}}`, `{{invoice.total}}`, etc.

**Tech Stack:** C# 12, ASP.NET Core 8, EF Core 8 + Npgsql, Wolverine event handlers + outbox, Fluid (Liquid template engine), MailKit + SMTP, FluentValidation, xUnit + FluentAssertions.

---

## File Map

```
src/Innovayse.Domain/Notifications/
  EmailTemplate.cs                                  ← AggregateRoot (slug, subject, body, variables)
  EmailLog.cs                                       ← Entity (to, subject, body, sentAt, success, error)
  EmailMessage.cs                                   ← record for sending emails
  Interfaces/IEmailTemplateRepository.cs
  Interfaces/IEmailLogRepository.cs
  Interfaces/IEmailSender.cs

src/Innovayse.Application/Notifications/
  DTOs/EmailTemplateDto.cs
  DTOs/EmailLogDto.cs
  Commands/CreateEmailTemplate/...
  Commands/UpdateEmailTemplate/...
  Commands/DeleteEmailTemplate/...
  Commands/SendEmail/
    SendEmailCommand.cs
    SendEmailHandler.cs
  Queries/GetEmailTemplate/...
  Queries/ListEmailTemplates/...
  Queries/ListEmailLogs/...
  Events/ClientRegisteredHandler.cs                ← welcome email
  Events/InvoiceCreatedHandler.cs                  ← invoice email
  Events/PaymentReceivedHandler.cs                 ← receipt email
  Events/ServiceProvisionedHandler.cs              ← credentials email
  Events/DomainExpiredHandler.cs                   ← expiry notice
  Events/DomainExpiringHandler.cs                  ← renewal reminder
  Events/TicketCreatedHandler.cs                   ← department notification
  Services/TemplateRenderer.cs                     ← Fluid-based rendering

src/Innovayse.Infrastructure/Notifications/
  Configurations/EmailTemplateConfiguration.cs
  Configurations/EmailLogConfiguration.cs
  EmailTemplateRepository.cs
  EmailLogRepository.cs
  MailKitEmailSender.cs                            ← IEmailSender impl
  SmtpSettings.cs

src/Innovayse.API/Notifications/
  EmailTemplatesController.cs                      ← Admin CRUD
  EmailLogsController.cs                           ← Admin view logs
```

---

## Task 1: Domain — EmailTemplate + EmailLog + Interface

Create EmailTemplate aggregate, EmailLog entity, EmailMessage record, and IEmailSender interface.

- [ ] **Step 1: Create EmailMessage record**

```csharp
namespace Innovayse.Domain.Notifications;

public record EmailMessage(
    string To,
    string Subject,
    string Body,
    bool IsHtml = true);
```

- [ ] **Step 2: Create IEmailSender interface**

```csharp
namespace Innovayse.Domain.Notifications.Interfaces;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}
```

- [ ] **Step 3: Create EmailTemplate aggregate**

```csharp
namespace Innovayse.Domain.Notifications;

public sealed class EmailTemplate : AggregateRoot
{
    public string Slug         { get; private set; } = string.Empty; // "welcome", "invoice-created"
    public string Subject      { get; private set; } = string.Empty;
    public string Body         { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive       { get; private set; }

    private EmailTemplate() { }

    public static EmailTemplate Create(string slug, string subject, string body, string? description)
    {
        return new EmailTemplate
        {
            Slug = slug,
            Subject = subject,
            Body = body,
            Description = description,
            IsActive = true
        };
    }

    public void Update(string subject, string body, string? description)
    {
        Subject = subject;
        Body = body;
        Description = description;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
```

- [ ] **Step 4: Create EmailLog entity**

```csharp
namespace Innovayse.Domain.Notifications;

public sealed class EmailLog : Entity
{
    public string To           { get; private set; } = string.Empty;
    public string Subject      { get; private set; } = string.Empty;
    public string Body         { get; private set; } = string.Empty;
    public DateTimeOffset SentAt { get; private set; }
    public bool Success        { get; private set; }
    public string? Error       { get; private set; }

    private EmailLog() { }

    public static EmailLog Create(string to, string subject, string body, bool success, string? error)
    {
        return new EmailLog
        {
            To = to,
            Subject = subject,
            Body = body,
            SentAt = DateTimeOffset.UtcNow,
            Success = success,
            Error = error
        };
    }
}
```

- [ ] **Step 5: Create repository interfaces**
- [ ] **Step 6: Run dotnet format and commit**

---

## Task 2: Application — DTOs

Create EmailTemplateDto and EmailLogDto.

---

## Task 3: Application — Template Commands

Create CreateEmailTemplate, UpdateEmailTemplate, DeleteEmailTemplate commands.

---

## Task 4: Application — Template Queries

Create GetEmailTemplate, ListEmailTemplates, ListEmailLogs queries.

---

## Task 5: Application — SendEmail Command + TemplateRenderer

- [ ] **Step 1: Create TemplateRenderer service**

```csharp
namespace Innovayse.Application.Notifications.Services;

using Fluid;

public sealed class TemplateRenderer
{
    private readonly FluidParser _parser = new();

    public async Task<string> RenderAsync(string template, object model)
    {
        if (_parser.TryParse(template, out var fluidTemplate, out var error))
        {
            var context = new TemplateContext(model);
            return await fluidTemplate.RenderAsync(context);
        }

        throw new InvalidOperationException($"Template parse error: {error}");
    }
}
```

- [ ] **Step 2: Create SendEmailCommand and handler**

```csharp
namespace Innovayse.Application.Notifications.Commands.SendEmail;

public record SendEmailCommand(
    string To,
    string TemplateSlug,
    object TemplateData);

public sealed class SendEmailHandler
{
    private readonly IEmailTemplateRepository _templateRepository;
    private readonly IEmailSender _emailSender;
    private readonly IEmailLogRepository _logRepository;
    private readonly TemplateRenderer _renderer;
    private readonly IUnitOfWork _unitOfWork;

    public async Task HandleAsync(SendEmailCommand cmd, CancellationToken ct)
    {
        var template = await _templateRepository.FindBySlugAsync(cmd.TemplateSlug, ct)
            ?? throw new InvalidOperationException($"Template '{cmd.TemplateSlug}' not found.");

        if (!template.IsActive)
        {
            return; // Skip inactive templates
        }

        var subject = await _renderer.RenderAsync(template.Subject, cmd.TemplateData);
        var body = await _renderer.RenderAsync(template.Body, cmd.TemplateData);

        var message = new EmailMessage(cmd.To, subject, body);

        try
        {
            await _emailSender.SendAsync(message, ct);
            _logRepository.Add(EmailLog.Create(cmd.To, subject, body, success: true, error: null));
        }
        catch (Exception ex)
        {
            _logRepository.Add(EmailLog.Create(cmd.To, subject, body, success: false, error: ex.Message));
            throw;
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }
}
```

---

## Task 6: Application — Event Handlers

Create event handlers for all domain events:

- [ ] **ClientRegisteredHandler → welcome email**
- [ ] **InvoiceCreatedHandler → invoice email**
- [ ] **PaymentReceivedHandler → receipt email**
- [ ] **ServiceProvisionedHandler → credentials email**
- [ ] **DomainExpiredHandler → expiry notice**
- [ ] **DomainExpiringHandler → renewal reminder**
- [ ] **TicketCreatedHandler → department notification**

Example:

```csharp
namespace Innovayse.Application.Notifications.Events;

public sealed class ClientRegisteredHandler
{
    private readonly IMessageBus _bus;

    public async Task HandleAsync(ClientRegisteredEvent evt, CancellationToken ct)
    {
        var data = new { client = new { name = evt.FirstName, email = evt.Email } };
        await _bus.InvokeAsync(new SendEmailCommand(evt.Email, "welcome", data), ct);
    }
}
```

---

## Task 7: Infrastructure — MailKit Implementation + Persistence

- [ ] **Step 1: Create SmtpSettings**

```csharp
namespace Innovayse.Infrastructure.Notifications;

public sealed class SmtpSettings
{
    public required string Host     { get; init; }
    public required int Port        { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
    public required string FromEmail { get; init; }
    public required string FromName  { get; init; }
    public bool UseSsl              { get; init; } = true;
}
```

- [ ] **Step 2: Create MailKitEmailSender**

```csharp
namespace Innovayse.Infrastructure.Notifications;

using MailKit.Net.Smtp;
using MimeKit;

public sealed class MailKitEmailSender : IEmailSender
{
    private readonly SmtpSettings _settings;

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        mimeMessage.To.Add(MailboxAddress.Parse(message.To));
        mimeMessage.Subject = message.Subject;
        mimeMessage.Body = new TextPart(message.IsHtml ? "html" : "plain") { Text = message.Body };

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.Host, _settings.Port, _settings.UseSsl, ct);
        await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);
        await client.SendAsync(mimeMessage, ct);
        await client.DisconnectAsync(true, ct);
    }
}
```

- [ ] **Step 3: Create EF Core configurations**
- [ ] **Step 4: Create repositories**
- [ ] **Step 5: Update AppDbContext and DependencyInjection.cs**
- [ ] **Step 6: Create and apply migration**

---

## Task 8: API — Controllers

Create EmailTemplatesController (admin CRUD) and EmailLogsController (admin view logs).

---

## Task 9: Seed Default Email Templates

Create a migration or startup seed to insert default email templates:
- welcome
- invoice-created
- payment-received
- service-provisioned
- domain-expired
- domain-expiring
- ticket-created

---

## Self-Review

- [x] EmailTemplate aggregate with Liquid rendering
- [x] EmailLog entity
- [x] IEmailSender + MailKit implementation
- [x] SendEmailCommand + TemplateRenderer
- [x] Event handlers for all domain events
- [x] Admin CRUD for templates
- [x] Email logs for audit trail
- [x] Default templates seeded

---

## Execution Handoff

Plan complete. Choose execution method.

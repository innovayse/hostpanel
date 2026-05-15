namespace Innovayse.Infrastructure.Persistence;

using Innovayse.Domain.Auth;
using Innovayse.Domain.Clients;
using Innovayse.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Seeds realistic test data for development. Idempotent — skips if clients already exist.
/// </summary>
public sealed class DevDataSeeder(
    AppDbContext db,
    UserManager<AppUser> userManager,
    ILogger<DevDataSeeder> logger)
{
    /// <summary>
    /// Seeds test clients with contacts and addresses.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task that completes when seeding is done.</returns>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Clients.AnyAsync(ct))
        {
            logger.LogInformation("Dev seed skipped — clients already exist");
            return;
        }

        logger.LogInformation("Seeding dev test data…");

        var clients = new List<(string Email, string First, string Last, string? Company, string? Phone, string? Street, string? City, string? State, string? PostCode, string? Country, ClientStatus Status)>
        {
            ("john.doe@example.com", "John", "Doe", "Doe Enterprises", "+1-555-0101", "123 Main St", "New York", "NY", "10001", "US", ClientStatus.Active),
            ("jane.smith@example.com", "Jane", "Smith", "Smith & Co", "+1-555-0102", "456 Oak Ave", "San Francisco", "CA", "94102", "US", ClientStatus.Active),
            ("alex.petrosyan@example.com", "Alex", "Petrosyan", "DigiTech AM", "+374-91-123456", "15 Tumanyan St", "Yerevan", null, "0001", "AM", ClientStatus.Active),
            ("maria.garcia@example.com", "Maria", "Garcia", null, "+34-612-345678", "Calle Mayor 22", "Madrid", null, "28013", "ES", ClientStatus.Active),
            ("oliver.brown@example.com", "Oliver", "Brown", "Brown Web Solutions", "+44-20-7946-0958", "10 Downing Rd", "London", null, "SW1A 2AA", "GB", ClientStatus.Active),
            ("emma.wilson@example.com", "Emma", "Wilson", null, "+1-555-0106", "789 Pine Ln", "Chicago", "IL", "60601", "US", ClientStatus.Suspended),
            ("liam.johnson@example.com", "Liam", "Johnson", "Johnson Hosting", "+1-555-0107", "321 Elm St", "Austin", "TX", "73301", "US", ClientStatus.Active),
            ("sophia.lee@example.com", "Sophia", "Lee", "Lee Digital", "+82-10-1234-5678", "Gangnam-gu", "Seoul", null, "06000", "KR", ClientStatus.Active),
            ("noah.martin@example.com", "Noah", "Martin", null, "+33-6-12-34-56-78", "8 Rue de Rivoli", "Paris", null, "75001", "FR", ClientStatus.Inactive),
            ("ava.taylor@example.com", "Ava", "Taylor", "Taylor Media", "+61-2-1234-5678", "42 George St", "Sydney", "NSW", "2000", "AU", ClientStatus.Active),
            ("ethan.davis@example.com", "Ethan", "Davis", null, null, null, null, null, null, null, ClientStatus.Active),
            ("mia.anderson@example.com", "Mia", "Anderson", "Anderson Corp", "+1-555-0112", "555 Broadway", "New York", "NY", "10012", "US", ClientStatus.Active),
            ("lucas.thomas@example.com", "Lucas", "Thomas", null, "+49-30-12345678", "Friedrichstr. 100", "Berlin", null, "10117", "DE", ClientStatus.Active),
            ("isabella.white@example.com", "Isabella", "White", "White Cloud Hosting", "+1-555-0114", "900 Cloud Dr", "Seattle", "WA", "98101", "US", ClientStatus.Closed),
            ("james.harris@example.com", "James", "Harris", null, "+1-555-0115", "77 Sunset Blvd", "Los Angeles", "CA", "90028", "US", ClientStatus.Active),
        };

        foreach (var (email, first, last, company, phone, street, city, state, postCode, country, status) in clients)
        {
            var user = new AppUser
            {
                UserName = email,
                Email = email,
                FirstName = first,
                LastName = last,
                EmailConfirmed = true,
            };

            var result = await userManager.CreateAsync(user, "Test@12345");
            if (!result.Succeeded)
            {
                logger.LogWarning("Failed to create user {Email}: {Errors}", email,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
                continue;
            }

            await userManager.AddToRoleAsync(user, Roles.Client);

            var client = Client.Create(user.Id, first, last, email, company);

            if (phone is not null)
                client.Update(first, last, company, phone);

            if (street is not null)
                client.UpdateAddress(street, null, city, state, postCode, country);

            if (status == ClientStatus.Suspended)
                client.Suspend();

            db.Clients.Add(client);
            await db.SaveChangesAsync(ct);

            // Add contacts to some clients
            if (company is not null)
            {
                client.AddContact(
                    first, last, company, email, phone, ContactType.Billing,
                    null, null, null, null, null, null,
                    true, true, true, true, true, true);
                client.AddContact(
                    "Support", $"at {company}", company, $"support@{email.Split('@')[1]}", null, ContactType.Technical,
                    null, null, null, null, null, null,
                    true, true, true, true, true, true);
                await db.SaveChangesAsync(ct);
            }
        }

        logger.LogInformation("Dev seed complete — {Count} clients created", clients.Count);
    }
}

using Innovayse.Hostpanel.Domain.Campaigns;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Plays the sender winning a race: right before a save that unsubscribes a campaign recipient, it commits the recipient as
/// sent through a connection of its own, so the save finds the row changed and fails the way two concurrent writers do. It
/// races a set number of saves and then lets every later one through.
/// </summary>
/// <param name="recipientId">The recipient the sender "mails" in the race.</param>
/// <param name="races">How many unsubscribing saves lose the race before saves are let through.</param>
internal sealed class RecipientRaceInterceptor(int recipientId, int races) : SaveChangesInterceptor
{
    /// <summary>How many saves have lost so far.</summary>
    private int _lost;

    /// <summary>Gets how many unsubscribing saves lost the race.</summary>
    public int Lost => _lost;

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var context = eventData.Context!;
        var unsubscribing = context.ChangeTracker.Entries<EmailCampaignRecipient>()
            .Any(e => e.State == EntityState.Modified && e.Entity.Id == recipientId);
        if (unsubscribing && _lost < races)
        {
            _lost++;
            await using var connection = new NpgsqlConnection(context.Database.GetConnectionString());
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                "UPDATE \"EmailCampaignRecipients\" SET \"Status\" = 'Sent', \"Attempts\" = 1, \"SentAt\" = now() WHERE \"Id\" = @id", connection);
            command.Parameters.AddWithValue("id", recipientId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return result;
    }
}

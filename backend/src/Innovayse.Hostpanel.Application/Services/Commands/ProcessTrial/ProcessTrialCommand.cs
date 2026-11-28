namespace Innovayse.Hostpanel.Application.Services.Commands.ProcessTrial;

/// <summary>
/// Takes one free trial's daily step: sends the reminder due today, converts or terminates a trial that has
/// ended, and terminates a converted trial whose invoice is still unpaid when the grace is over. Running it twice
/// on the same day changes nothing more.
/// </summary>
/// <param name="ServiceId">The trial service, as the daily trial job dispatches it.</param>
public sealed record ProcessTrialCommand(int ServiceId);

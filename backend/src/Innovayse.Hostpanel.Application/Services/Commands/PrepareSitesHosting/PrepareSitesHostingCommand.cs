namespace Innovayse.Hostpanel.Application.Services.Commands.PrepareSitesHosting;

/// <summary>
/// Prepares the hosting account of a service for Innovayse Sites deploys when its product asks for it
/// (product metadata <c>{"innovayseSites": {"prepare": true}}</c>). Idempotent; a service of any other product
/// is left alone and the command succeeds.
/// </summary>
/// <param name="ServiceId">The service whose hosting account is prepared.</param>
public sealed record PrepareSitesHostingCommand(int ServiceId);

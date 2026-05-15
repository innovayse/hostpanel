namespace Innovayse.Application.Services.Queries.GetService;

/// <summary>Returns a single client service by primary key (admin view).</summary>
/// <param name="ServiceId">The service primary key.</param>
public record GetServiceQuery(int ServiceId);

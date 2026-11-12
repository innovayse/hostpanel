namespace Innovayse.Hostpanel.Integration.Tests.Common;

/// <summary>The message the probe validator checks.</summary>
/// <param name="Id">An identifier.</param>
/// <param name="Page">A 1-based page.</param>
/// <param name="PageSize">A page size.</param>
/// <param name="DiscountPercent">A discount in percent.</param>
internal sealed record PagingProbe(int Id, int Page, int PageSize, decimal DiscountPercent = 0m);

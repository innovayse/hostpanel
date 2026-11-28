namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>A free trial as the trial emails read it.</summary>
/// <param name="ServiceId">The trial service, read as <c>service_id</c>.</param>
/// <param name="ProductName">The name of the product on trial, read as <c>product_name</c>.</param>
/// <param name="EndDate">The day the trial ends, <c>yyyy-MM-dd</c>, read as <c>end_date</c>.</param>
/// <param name="ServiceUrl">The absolute link to the service in the client area, read as <c>service_url</c>.</param>
public sealed record TrialTemplateData(int ServiceId, string ProductName, string EndDate, string ServiceUrl);

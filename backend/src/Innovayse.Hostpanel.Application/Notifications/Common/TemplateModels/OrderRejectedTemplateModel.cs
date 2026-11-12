namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>Data for the <c>order-rejected</c> email: staff refused an order after a review.</summary>
/// <param name="OrderNumber">The order's number, read as <c>{{ order_number }}</c>.</param>
/// <param name="Reason">The reason staff gave, read as <c>{{ reason }}</c>.</param>
public sealed record OrderRejectedTemplateModel(string OrderNumber, string Reason) : EmailTemplateModel;

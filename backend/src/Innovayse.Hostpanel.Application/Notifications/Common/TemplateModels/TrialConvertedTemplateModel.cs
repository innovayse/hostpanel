namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>Data for the <c>trial-converted</c> email: a free trial became a paid service awaiting its first payment.</summary>
/// <param name="Trial">The trial, read as <c>{{ trial.* }}</c>.</param>
/// <param name="PaymentDueDate">The day the first invoice must be paid by, <c>yyyy-MM-dd</c>, read as <c>payment_due_date</c>.</param>
public sealed record TrialConvertedTemplateModel(TrialTemplateData Trial, string PaymentDueDate) : EmailTemplateModel;

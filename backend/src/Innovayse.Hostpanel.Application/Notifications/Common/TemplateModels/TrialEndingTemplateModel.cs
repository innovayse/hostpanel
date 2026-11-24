namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>Data for the <c>trial-ending</c> email: a free trial is about to end.</summary>
/// <param name="Trial">The trial, read as <c>{{ trial.* }}</c>.</param>
/// <param name="DaysLeft">How many days are left, read as <c>days_left</c>.</param>
public sealed record TrialEndingTemplateModel(TrialTemplateData Trial, int DaysLeft) : EmailTemplateModel;

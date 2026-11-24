namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>Data for the <c>trial-ended</c> email: a free trial ended and the service was closed.</summary>
/// <param name="Trial">The trial, read as <c>{{ trial.* }}</c>.</param>
public sealed record TrialEndedTemplateModel(TrialTemplateData Trial) : EmailTemplateModel;

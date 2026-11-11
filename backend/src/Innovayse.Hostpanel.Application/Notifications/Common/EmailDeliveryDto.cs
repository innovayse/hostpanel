using Innovayse.Hostpanel.Domain.Notifications.Enums;

namespace Innovayse.Hostpanel.Application.Notifications.Common;

/// <summary>How the delivery of one keyed mail stands, as the email log recorded it.</summary>
/// <param name="State">Whether the mail is being sent, was delivered, or was refused.</param>
/// <param name="Failure">The classified reason the relay refused it, or <c>null</c> unless <paramref name="State"/> is failed.</param>
/// <param name="MayResend">Whether sending under the same key again would send a new mail: the last attempt was refused, or its claim has outlived its lease.</param>
public sealed record EmailDeliveryDto(EmailDeliveryState State, EmailSendFailure? Failure, bool MayResend);

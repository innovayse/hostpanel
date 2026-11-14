/**
 * Presentation helpers for the email log: which translated caption a send outcome gets, so every
 * screen that lists sent mail words a delivery failure the same way.
 */
import { EmailLogOutcome, EmailSendFailure } from '@/types/emaillog'
import type { EmailLog } from '@/types/emaillog'

/** Locale key of the caption per reason the relay did not accept a message. */
export const emailSendFailureLabelKey: Record<EmailSendFailure, string> = {
  [EmailSendFailure.Unknown]: 'clients.clientEmails.failure.unknown',
  [EmailSendFailure.RelayUnreachable]: 'clients.clientEmails.failure.relayUnreachable',
  [EmailSendFailure.Timeout]: 'clients.clientEmails.failure.timeout',
  [EmailSendFailure.SecureConnectionFailed]: 'clients.clientEmails.failure.secureConnectionFailed',
  [EmailSendFailure.AuthenticationRejected]: 'clients.clientEmails.failure.authenticationRejected',
  [EmailSendFailure.RecipientRejected]: 'clients.clientEmails.failure.recipientRejected',
  [EmailSendFailure.SenderRejected]: 'clients.clientEmails.failure.senderRejected',
  [EmailSendFailure.MessageRejected]: 'clients.clientEmails.failure.messageRejected',
}

/**
 * Gives the locale key of an entry's outcome caption.
 *
 * @param entry - The logged send.
 * @returns The "delivered" key, or the key of the reason it failed (unknown when none was recorded).
 */
export const emailOutcomeLabelKey = (entry: Pick<EmailLog, 'success' | 'failure'>): string =>
  entry.success ? 'clients.clientEmails.delivered' : emailSendFailureLabelKey[entry.failure ?? EmailSendFailure.Unknown]

/**
 * Reads the outcome filter of an email log as the API's `success` switch.
 *
 * @param outcome - The choice read from the address, undefined when none is set.
 * @returns True for delivered mail, false for failed mail, undefined for no narrowing.
 */
export const successOfOutcome = (outcome: EmailLogOutcome | undefined): boolean | undefined =>
  outcome === undefined ? undefined : outcome === EmailLogOutcome.Delivered

/**
 * Which tone a status badge wears for a given status word.
 *
 * Kept out of the badge component so the mapping is one table that a test can walk, and so a
 * new status is one line here rather than a new class string in a template.
 *
 * @module utils/clientStatus
 */

import { BadgeStatus } from '~/types/badgestatus'
import { UiTone } from '~/utils/ui/enums'

/** Tone per known status. */
const STATUS_TONES: Record<BadgeStatus, UiTone> = {
  [BadgeStatus.Active]: UiTone.Success,
  [BadgeStatus.Paid]: UiTone.Success,
  [BadgeStatus.Open]: UiTone.Info,
  [BadgeStatus.AwaitingReply]: UiTone.Info,
  [BadgeStatus.Answered]: UiTone.Info,
  [BadgeStatus.Pending]: UiTone.Warning,
  [BadgeStatus.PendingRegistration]: UiTone.Warning,
  [BadgeStatus.PendingTransfer]: UiTone.Warning,
  [BadgeStatus.PendingDns]: UiTone.Warning,
  [BadgeStatus.Unpaid]: UiTone.Warning,
  [BadgeStatus.PaymentPending]: UiTone.Warning,
  [BadgeStatus.Grace]: UiTone.Warning,
  [BadgeStatus.Overdue]: UiTone.Danger,
  [BadgeStatus.Collections]: UiTone.Danger,
  [BadgeStatus.Suspended]: UiTone.Danger,
  [BadgeStatus.Expired]: UiTone.Danger,
  [BadgeStatus.Redemption]: UiTone.Danger,
  [BadgeStatus.Refunded]: UiTone.Info,
  [BadgeStatus.PartiallyRefunded]: UiTone.Info,
  [BadgeStatus.Draft]: UiTone.Default,
  [BadgeStatus.Terminated]: UiTone.Default,
  [BadgeStatus.Transferred]: UiTone.Default,
  [BadgeStatus.Cancelled]: UiTone.Default,
  [BadgeStatus.Closed]: UiTone.Default
}

/**
 * Looks up the tone for a status word.
 *
 * @param status - The status exactly as the API sent it.
 * @returns The tone, or {@link UiTone.Default} for a word the table does not know.
 */
export const statusTone = (status: string): UiTone => STATUS_TONES[status as BadgeStatus] ?? UiTone.Default

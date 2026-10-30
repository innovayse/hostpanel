/**
 * Tests for `utils/clientStatus.ts`: every status word the badge knows has a tone, and an
 * unknown word falls back to the neutral one.
 *
 * @module tests/utils/clientStatus.test
 */

import { describe, expect, it } from 'vitest'
import { BadgeStatus } from '~/types/badgestatus'
import { statusTone } from '~/utils/clientStatus'
import { UiTone } from '~/utils/ui/enums'

describe('statusTone', () => {
  it.each([
    [BadgeStatus.Active, UiTone.Success],
    [BadgeStatus.Paid, UiTone.Success],
    [BadgeStatus.Open, UiTone.Info],
    [BadgeStatus.AwaitingReply, UiTone.Info],
    [BadgeStatus.Answered, UiTone.Info],
    [BadgeStatus.Refunded, UiTone.Info],
    [BadgeStatus.PartiallyRefunded, UiTone.Info],
    [BadgeStatus.Pending, UiTone.Warning],
    [BadgeStatus.PendingTransfer, UiTone.Warning],
    [BadgeStatus.Unpaid, UiTone.Warning],
    [BadgeStatus.Grace, UiTone.Warning],
    [BadgeStatus.Overdue, UiTone.Danger],
    [BadgeStatus.Suspended, UiTone.Danger],
    [BadgeStatus.Redemption, UiTone.Danger],
    [BadgeStatus.Cancelled, UiTone.Default],
    [BadgeStatus.Closed, UiTone.Default],
    [BadgeStatus.Terminated, UiTone.Default]
  ])('gives %s the %s tone', (status, tone) => {
    expect(statusTone(status)).toBe(tone)
  })

  it('gives every member of BadgeStatus a tone of its own choosing, never undefined', () => {
    for (const status of Object.values(BadgeStatus)) {
      expect(Object.values(UiTone)).toContain(statusTone(status))
    }
  })

  it('falls back to the neutral tone for a word the table does not know', () => {
    expect(statusTone('SomethingNew')).toBe(UiTone.Default)
  })
})

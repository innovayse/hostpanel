/**
 * Unit tests for the email log presentation helpers: every failure reason has a caption the
 * catalogue defines, and an entry's outcome picks "delivered" or its reason.
 */
import { describe, expect, it } from 'vitest'
import { emailOutcomeLabelKey, emailSendFailureLabelKey, successOfOutcome } from '@/lib/emailLogStatus'
import { EmailLogOutcome, EmailSendFailure } from '@/types/emaillog'
import { createTestI18n } from '../helpers/i18n'

describe('emailLogStatus', () => {
  it('gives every failure reason a caption key that the catalogue defines', () => {
    for (const failure of Object.values(EmailSendFailure)) {
      const key = emailSendFailureLabelKey[failure]
      expect(key).toMatch(/^clients\.clientEmails\.failure\./)
      expect(createTestI18n().global.te(key)).toBe(true)
    }
  })

  it('captions a delivered entry as delivered', () => {
    expect(emailOutcomeLabelKey({ success: true, failure: null })).toBe('clients.clientEmails.delivered')
  })

  it('captions a failed entry with its reason, or unknown when none was recorded', () => {
    expect(emailOutcomeLabelKey({ success: false, failure: EmailSendFailure.RecipientRejected })).toBe(
      'clients.clientEmails.failure.recipientRejected',
    )
    expect(emailOutcomeLabelKey({ success: false, failure: null })).toBe('clients.clientEmails.failure.unknown')
  })

  it('turns the outcome filter into the API switch: delivered true, failed false, none unnarrowed', () => {
    expect(successOfOutcome(EmailLogOutcome.Delivered)).toBe(true)
    expect(successOfOutcome(EmailLogOutcome.Failed)).toBe(false)
    expect(successOfOutcome(undefined)).toBeUndefined()
  })
})

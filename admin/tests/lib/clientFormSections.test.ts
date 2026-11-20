/**
 * Tests for the sections the client create form and the client profile form share: the switches match the payload
 * fields they drive, and every caption is a locale key that resolves.
 */
import { describe, expect, it } from 'vitest'
import { CLIENT_ADMIN_NOTES_SECTION, CLIENT_NOTIFICATIONS_SECTION, CLIENT_SETTINGS_SECTION } from '@/lib/clientFormSections'
import { SkeletonFieldKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import { createTestI18n } from '../helpers/i18n'

describe('clientFormSections', () => {
  it('lists one switch per email notification of the payload, in display order', () => {
    expect(CLIENT_NOTIFICATIONS_SECTION.fields.map(field => field.key)).toEqual([
      'notifyGeneral',
      'notifyInvoice',
      'notifySupport',
      'notifyProduct',
      'notifyDomain',
      'notifyAffiliate',
    ])
    expect(CLIENT_NOTIFICATIONS_SECTION.fields.every(field => field.kind === SkeletonFieldKind.Toggle)).toBe(true)
  })

  it('lists the account setting switches named as the payload fields, then the tax fields', () => {
    expect(CLIENT_SETTINGS_SECTION.fields.map(field => field.key)).toEqual([
      'overdueNotices',
      'taxExempt',
      'separateInvoices',
      'disableCcProcessing',
      'marketingOptIn',
      'statusUpdate',
      'allowSso',
      'taxId',
      'taxExemptionReason',
    ])
  })

  it('holds the internal notes as one full width text area', () => {
    expect(CLIENT_ADMIN_NOTES_SECTION.fields).toEqual([{ key: 'adminNotes', kind: SkeletonFieldKind.Textarea, span: SkeletonSpan.Full }])
  })

  it('names only locale keys that the English catalogue resolves', () => {
    const i18n = createTestI18n()
    const keys = [CLIENT_NOTIFICATIONS_SECTION, CLIENT_SETTINGS_SECTION, CLIENT_ADMIN_NOTES_SECTION]
      .flatMap(section => [section.title, ...section.fields.map(field => field.label)])
      .filter((key): key is string => key !== undefined)

    for (const key of keys) {
      expect(i18n.global.te(key), key).toBe(true)
    }
  })
})

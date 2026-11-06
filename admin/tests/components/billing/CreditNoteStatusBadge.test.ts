/**
 * Mount tests for CreditNoteStatusBadge: each state shows its own translated name and tone.
 */
import { describe, expect, it } from 'vitest'
import CreditNoteStatusBadge from '@/components/billing/CreditNoteStatusBadge.vue'
import { CreditNoteStatus } from '@/types/creditnote'
import { mountWithApp } from '../../helpers/mount'

describe('CreditNoteStatusBadge', () => {
  it.each([
    [CreditNoteStatus.Draft, 'Draft'],
    [CreditNoteStatus.Issued, 'Issued'],
    [CreditNoteStatus.Applied, 'Applied'],
    [CreditNoteStatus.Voided, 'Voided'],
  ])('shows %s as %s', (status, text) => {
    const wrapper = mountWithApp(CreditNoteStatusBadge, { props: { status } })

    expect(wrapper.get(`[data-testid="billing-credit-note-status-${status}"]`).text()).toBe(text)
  })
})

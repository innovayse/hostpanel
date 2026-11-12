/**
 * Unit tests for EmailsTab: loading, empty, search, paging and the view link.
 */
import { describe, expect, it, vi } from 'vitest'
import { mountAccount } from '~/tests/helpers/mountAccount'
import EmailsTab from '~/components/account/EmailsTab.vue'
import { useAccountStore } from '~/stores/account'
import type { ClientEmailSummary } from '~/types/clientemailsummary'

vi.mock('qrcode', () => ({
  default: {
    /**
     * Stands in for the QR renderer, a third-party package the account store imports.
     *
     * @returns An empty data URL.
     */
    toDataURL: async (): Promise<string> => ''
  }
}))

/**
 * Builds a list of sent emails.
 *
 * @param count - How many to build.
 * @returns The emails, subjects "Mail 1" to "Mail N".
 */
const emails = (count: number): ClientEmailSummary[] =>
  Array.from({ length: count }, (_, index) => ({
    id: index + 1, to: 'ada@example.test', subject: `Mail ${index + 1}`, sentAt: '2026-01-01T10:00:00Z', success: true
  }))

describe('EmailsTab', () => {
  it('shows a skeleton while loading', async () => {
    const wrapper = mountAccount(EmailsTab)
    useAccountStore().emailsLoading = true
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="account-emails-loading"]').exists()).toBe(true)
  })

  it('shows the empty state without mail', () => {
    const wrapper = mountAccount(EmailsTab)

    expect(wrapper.find('[data-testid="ui-empty-state"]').text()).toContain('client.emails.empty')
  })

  it('lists the mail with a link to each message', async () => {
    const wrapper = mountAccount(EmailsTab)
    useAccountStore().emails = emails(3)
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="account-email-2"]').text()).toContain('Mail 2')
    expect(wrapper.find('[data-testid="account-email-2"] a').attributes('href')).toBe('/client/emails/2')
  })

  it('shows only the first page of ten', async () => {
    const wrapper = mountAccount(EmailsTab)
    useAccountStore().emails = emails(25)
    await wrapper.vm.$nextTick()

    expect(wrapper.findAll('[data-testid^="account-email-"]')).toHaveLength(10)
  })

  it('filters by subject, case-insensitively', async () => {
    const wrapper = mountAccount(EmailsTab)
    useAccountStore().emails = emails(25)
    await wrapper.vm.$nextTick()

    await wrapper.find('[data-testid="account-emails-search"]').setValue('mail 2')

    expect(wrapper.findAll('[data-testid^="account-email-"]').length).toBeGreaterThan(0)
    expect(wrapper.text()).not.toContain('Mail 1 ')
  })

  it('says nothing matched when the search finds nothing', async () => {
    const wrapper = mountAccount(EmailsTab)
    useAccountStore().emails = emails(3)
    await wrapper.vm.$nextTick()

    await wrapper.find('[data-testid="account-emails-search"]').setValue('zzz')

    expect(wrapper.find('[data-testid="ui-empty-state"]').text()).toContain('client.emails.noResults')
  })
})

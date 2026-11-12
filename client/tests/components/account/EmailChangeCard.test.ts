/**
 * Unit tests for EmailChangeCard: the current address, the dialog it opens and the "check your
 * new inbox" state once a link has been sent. The change goes through the real dialog, account
 * store, API composable and transport against a stub BFF.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountAccount } from '~/tests/helpers/mountAccount'
import EmailChangeCard from '~/components/account/EmailChangeCard.vue'
import { AccountEndpoint } from '~/tests/helpers/accountPaths'
import { bodyOf } from '~/tests/helpers/requestBody'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'

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

/** The stub playing the BFF; it accepts every request. */
let stub: StubServer

beforeEach(async () => {
  stub = await startTransport(() => ({ body: {} }))
})

afterEach(async () => {
  await stub.close()
})

describe('EmailChangeCard', () => {
  it('shows the address the account signs in with', () => {
    const wrapper = mountAccount(EmailChangeCard, { props: { email: 'ada@example.com' } })

    expect(wrapper.find('[data-testid="account-email-change-current"]').text()).toBe('ada@example.com')
    expect(wrapper.find('[data-testid="account-email-change-sent"]').exists()).toBe(false)
  })

  it('opens the change dialog', async () => {
    const wrapper = mountAccount(EmailChangeCard, { props: { email: 'ada@example.com' } })

    await wrapper.find('[data-testid="account-email-change-open"]').trigger('click')

    expect(wrapper.find('[data-testid="account-email-change-dialog"]').exists()).toBe(true)
  })

  it('tells the person to check the new inbox once a link went out', async () => {
    const wrapper = mountAccount(EmailChangeCard, { props: { email: 'ada@example.com' } })
    await wrapper.find('[data-testid="account-email-change-open"]').trigger('click')

    await wrapper.find('[data-testid="account-email-change-new"]').setValue('new@example.com')
    await wrapper.find('[data-testid="account-email-change-password"]').setValue('secret-pw')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(wrapper.find('[data-testid="account-email-change-sent"]').text()).toContain('client.emailChange.sentTitle'))
    expect(stub.requests).toHaveLength(1)
    expect(stub.requests[0]?.url).toBe(AccountEndpoint.EmailChange)
    expect(bodyOf(stub.requests[0])).toEqual({ newEmail: 'new@example.com', currentPassword: 'secret-pw' })
  })
})

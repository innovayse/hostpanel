/**
 * Unit tests for EmailChangeDialog: the two fields, the local checks and the request through the
 * real account store, API composable and transport against a stub BFF.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountAccount } from '~/tests/helpers/mountAccount'
import EmailChangeDialog from '~/components/account/EmailChangeDialog.vue'
import { AccountEndpoint } from '~/tests/helpers/accountPaths'
import { bodyOf } from '~/tests/helpers/requestBody'
import type { StubReply, StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { HttpMethod } from '~/types/httpmethod'
import { waitUntil } from '~/tests/helpers/waitUntil'

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

/** The stub playing the BFF. */
let stub: StubServer

/** What the stub answers the address change with; a test changes it before it submits. */
let reply: StubReply

beforeEach(async () => {
  reply = { body: {} }
  stub = await startTransport(() => reply)
})

afterEach(async () => {
  await stub.close()
})

describe('EmailChangeDialog', () => {
  it('asks for the new address and the current password', () => {
    const wrapper = mountAccount(EmailChangeDialog, { props: { modelValue: true } })

    expect(wrapper.find('[data-testid="account-email-change-new"]').attributes('type')).toBe('email')
    expect(wrapper.find('[data-testid="account-email-change-password"]').attributes('type')).toBe('password')
  })

  it('sends the change and closes on success', async () => {
    const wrapper = mountAccount(EmailChangeDialog, { props: { modelValue: true } })

    await wrapper.find('[data-testid="account-email-change-new"]').setValue(' new@example.com ')
    await wrapper.find('[data-testid="account-email-change-password"]').setValue('secret-pw')
    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(wrapper.emitted('update:modelValue')?.at(-1)).toEqual([false]))
    expect(stub.requests).toHaveLength(1)
    expect(stub.requests[0]?.method).toBe(HttpMethod.Post)
    expect(stub.requests[0]?.url).toBe(AccountEndpoint.EmailChange)
    expect(bodyOf(stub.requests[0])).toEqual({ newEmail: 'new@example.com', currentPassword: 'secret-pw' })
  })

  it('refuses a malformed address and a missing password without a request', async () => {
    const wrapper = mountAccount(EmailChangeDialog, { props: { modelValue: true } })

    await wrapper.find('[data-testid="account-email-change-new"]').setValue('new.example.com')
    await wrapper.find('form').trigger('submit')
    await wrapper.vm.$nextTick()

    expect(stub.requests).toHaveLength(0)
    expect(wrapper.text()).toContain('client.profile.validation.emailInvalid')
    expect(wrapper.text()).toContain('client.emailChange.passwordRequired')
  })

  it('stays open and shows the API sentence when the change is refused', async () => {
    reply = { status: 400, body: { detail: 'The current password is not correct.' } }
    const wrapper = mountAccount(EmailChangeDialog, { props: { modelValue: true } })

    await wrapper.find('[data-testid="account-email-change-new"]').setValue('new@example.com')
    await wrapper.find('[data-testid="account-email-change-password"]').setValue('wrong')
    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(wrapper.text()).toContain('The current password is not correct.'))
    expect(stub.requests).toHaveLength(1)
    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
  })
})

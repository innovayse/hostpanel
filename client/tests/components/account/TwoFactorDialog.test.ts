/**
 * Unit tests for TwoFactorDialog: enable vs disable content and the code submission. The account
 * store, the auth API composable and the transport run for real against a stub BFF.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountAccount } from '~/tests/helpers/mountAccount'
import TwoFactorDialog from '~/components/account/TwoFactorDialog.vue'
import OtpInput from '~/components/ui/UiOtpInput.vue'
import { AccountEndpoint } from '~/tests/helpers/accountPaths'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable, refusal } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { HttpMethod } from '~/types/httpmethod'
import { TwoFactorMode } from '~/types/twofactormode'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies the stub gives, by `METHOD /path`; each test fills the ones it needs. */
let table: StubRouteTable

beforeEach(async () => {
  table = {}
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  vi.unstubAllGlobals()
  await stub.close()
})

describe('TwoFactorDialog', () => {
  it('renders nothing while closed', () => {
    const wrapper = mountAccount(TwoFactorDialog, { props: { modelValue: false, mode: TwoFactorMode.Enable } })

    expect(wrapper.find('[data-testid="account-2fa-dialog"]').exists()).toBe(false)
  })

  it('shows the QR and secret when enabling', () => {
    const wrapper = mountAccount(TwoFactorDialog, {
      props: { modelValue: true, mode: TwoFactorMode.Enable, secret: 'ABCD', qrDataUrl: 'data:image/png;base64,QR' }
    })

    expect(wrapper.find('[data-testid="account-2fa-qr"]').attributes('src')).toBe('data:image/png;base64,QR')
    expect(wrapper.find('[data-testid="account-2fa-secret"]').text()).toContain('ABCD')
    expect(wrapper.text()).toContain('client.twoFactor.setupTitle')
  })

  it('shows neither QR nor secret when disabling', () => {
    const wrapper = mountAccount(TwoFactorDialog, {
      props: { modelValue: true, mode: TwoFactorMode.Disable, secret: 'ABCD', qrDataUrl: 'data:image/png;base64,QR' }
    })

    expect(wrapper.find('[data-testid="account-2fa-qr"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="account-2fa-secret"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('client.twoFactor.disableTitle')
  })

  it('asks for the current password only when disabling', () => {
    const disabling = mountAccount(TwoFactorDialog, { props: { modelValue: true, mode: TwoFactorMode.Disable } })
    const enabling = mountAccount(TwoFactorDialog, { props: { modelValue: true, mode: TwoFactorMode.Enable } })

    expect(disabling.find('[data-testid="account-2fa-password"]').attributes('type')).toBe('password')
    expect(enabling.find('[data-testid="account-2fa-password"]').exists()).toBe(false)
  })

  it('puts the submit button in the pinned footer, tied to the form by id', () => {
    const wrapper = mountAccount(TwoFactorDialog, { props: { modelValue: true, mode: TwoFactorMode.Enable } })
    const button = wrapper.find('[data-testid="account-2fa-submit"]')

    expect(wrapper.find('[data-testid="ui-dialog-body"]').find('[data-testid="account-2fa-submit"]').exists()).toBe(false)
    expect(button.attributes('type')).toBe('submit')
    expect(button.attributes('form')).toBe(wrapper.find('[data-testid="two-factor-form"]').attributes('id'))
  })

  it('submits the typed code for the mode and closes on success', async () => {
    table[`${HttpMethod.Post} ${AccountEndpoint.TwoFactorDisable}`] = { body: {} }
    table[`${HttpMethod.Post} ${AccountEndpoint.Logout}`] = { body: {} }
    const wrapper = mountAccount(TwoFactorDialog, { props: { modelValue: true, mode: TwoFactorMode.Disable } })

    wrapper.findComponent(OtpInput).vm.$emit('update:modelValue', '123456')
    await wrapper.find('[data-testid="account-2fa-password"]').setValue('secret-pw')
    await wrapper.vm.$nextTick()
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(wrapper.emitted('update:modelValue')?.at(-1)).toEqual([false]))
    const sent = requestsTo(stub.requests, AccountEndpoint.TwoFactorDisable)
    expect(sent).toHaveLength(1)
    expect(sent[0]?.method).toBe(HttpMethod.Post)
    expect(bodyOf(sent[0])).toEqual({ code: '123456', currentPassword: 'secret-pw' })
    expect(requestsTo(stub.requests, AccountEndpoint.Logout)).toHaveLength(1)
  })

  it('stays open when the code is refused', async () => {
    table[`${HttpMethod.Post} ${AccountEndpoint.TwoFactorEnable}`] = refusal('Wrong code')
    const wrapper = mountAccount(TwoFactorDialog, { props: { modelValue: true, mode: TwoFactorMode.Enable } })

    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(wrapper.find('[data-testid="ui-form-error"]').exists()).toBe(true))
    expect(requestsTo(stub.requests, AccountEndpoint.TwoFactorEnable)).toHaveLength(1)
    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
  })

  it('shows the refusal sentence', async () => {
    table[`${HttpMethod.Post} ${AccountEndpoint.TwoFactorEnable}`] = refusal('Wrong code')
    const wrapper = mountAccount(TwoFactorDialog, { props: { modelValue: true, mode: TwoFactorMode.Enable } })

    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(wrapper.find('[data-testid="ui-form-error"]').text()).toBe('Wrong code'))
  })
})

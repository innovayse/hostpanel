/**
 * Tests for `pages/client/login.vue` against the real auth store, API composable and transport,
 * with a stub BFF answering on a real socket (nothing of ours is mocked): the local form, its
 * refusals, the redirect after sign-in and the single SSO button.
 *
 * @module tests/pages/client/login.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { APP_LOGO_STUB, mountAuthPage, stubAuthRuntime } from '~/tests/helpers/authScreens'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { mountPage } from '~/tests/helpers/mountClient'
import { bodyOf } from '~/tests/helpers/requestBody'
import { settle } from '~/tests/helpers/settle'
import { AccountEndpoint } from '~/tests/helpers/accountPaths'
import { CLIENT_DASHBOARD_PATH, CLIENT_FORGOT_PASSWORD_PATH, clientInvoicePath, CLIENT_REDIRECT_QUERY, CLIENT_REGISTER_PATH, SSO_AUTHORIZE_PATH } from '~/constants/clientRoutes'
import { heldReply, requestsTo, routeTable, writeOf } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import LoginPage from '~/pages/client/login.vue'
import { useDemoStore } from '~/stores/demo'
import { DemoAccountRole } from '~/types/demoaccountrole'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** What the password sign-in answers when the account has a second factor. */
const CHALLENGE = { twoFactorRequired: true, pendingToken: 'p' }

/** What the second-factor sign-in answers for a wrong code. */
const WRONG_CODE = { status: 401, body: { detail: 'Invalid code.' } }

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Types the credentials and submits the form.
 *
 * @param wrapper - The mounted page.
 * @param password - Password to type.
 */
const signIn = async (wrapper: Awaited<ReturnType<typeof mountAuthPage>>, password = 'secret'): Promise<void> => {
  await wrapper.find('[data-testid="login-email"]').setValue('ada@example.com')
  await wrapper.find('[data-testid="login-password"]').setValue(password)
  await wrapper.find('form').trigger('submit')
  await flushPromises()
}

/**
 * Signs in with a password, waiting until the code step is on screen.
 *
 * @param wrapper - The mounted page.
 */
const reachCodeStep = async (wrapper: Awaited<ReturnType<typeof mountAuthPage>>): Promise<void> => {
  await signIn(wrapper)
  await waitUntil(() => expect(wrapper.find('[data-testid="login-2fa-form"]').exists()).toBe(true))
}

/**
 * Types a six-digit code into the code step, one digit per box.
 *
 * @param wrapper - The mounted page.
 * @param code - The six digits.
 */
const typeCode = async (wrapper: Awaited<ReturnType<typeof mountAuthPage>>, code: string): Promise<void> => {
  for (const [index, digit] of [...code].entries()) {
    await wrapper.find(`[data-testid="otp-input-${index}"]`).setValue(digit)
  }
}

beforeEach(async () => {
  table = {}
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('client login page (local mode)', () => {
  it('shows both fields with placeholders and the submit button', async () => {
    stubAuthRuntime()
    const wrapper = await mountAuthPage(LoginPage)

    expect(wrapper.find('[data-testid="login-email"]').attributes('placeholder')).toBe('client.login.emailPlaceholder')
    expect(wrapper.find('[data-testid="login-password"]').attributes('placeholder')).toBe('client.login.passwordPlaceholder')
    expect(wrapper.find('[data-testid="login-submit"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="login-sso"]').exists()).toBe(false)
  })

  it('links to password recovery and registration', async () => {
    stubAuthRuntime()
    const wrapper = await mountAuthPage(LoginPage)

    expect(wrapper.find('[data-testid="login-forgot"]').attributes('href')).toBe(CLIENT_FORGOT_PASSWORD_PATH)
    expect(wrapper.find('[data-testid="login-register"]').attributes('href')).toBe(CLIENT_REGISTER_PATH)
  })

  it('signs in and goes to the dashboard', async () => {
    const { push } = stubAuthRuntime()
    table[writeOf(AccountEndpoint.Login)] = { body: makeClientUser({ email: 'ada@example.com' }) }
    const wrapper = await mountAuthPage(LoginPage)

    await signIn(wrapper)
    await waitUntil(() => expect(push).toHaveBeenCalledWith(CLIENT_DASHBOARD_PATH))

    expect(bodyOf(requestsTo(stub.requests, AccountEndpoint.Login)[0])).toEqual({ email: 'ada@example.com', password: 'secret' })
  })

  it('goes back to the page the visitor asked for', async () => {
    const { push } = stubAuthRuntime({ query: { [CLIENT_REDIRECT_QUERY]: clientInvoicePath(7) } })
    table[writeOf(AccountEndpoint.Login)] = { body: makeClientUser({ email: 'ada@example.com' }) }
    const wrapper = await mountAuthPage(LoginPage)

    await signIn(wrapper)

    await waitUntil(() => expect(push).toHaveBeenCalledWith(clientInvoicePath(7)))
  })

  it('ignores a redirect that leaves the app and goes to the dashboard', async () => {
    const { push } = stubAuthRuntime({ query: { [CLIENT_REDIRECT_QUERY]: 'https://evil.example/phish' } })
    table[writeOf(AccountEndpoint.Login)] = { body: makeClientUser({ email: 'ada@example.com' }) }
    const wrapper = await mountAuthPage(LoginPage)

    await signIn(wrapper)

    await waitUntil(() => expect(push).toHaveBeenCalledWith(CLIENT_DASHBOARD_PATH))
  })

  it('shows the API sentence and stays put when the credentials are refused', async () => {
    const { push } = stubAuthRuntime()
    table[writeOf(AccountEndpoint.Login)] = { status: 401, body: { detail: 'Wrong email or password.' } }
    const wrapper = await mountAuthPage(LoginPage)

    await signIn(wrapper, 'nope')
    await waitUntil(() => expect(wrapper.find('[data-testid="login-error"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="login-error"]').text()).toBe('Wrong email or password.')
    expect(push).not.toHaveBeenCalled()
  })

  it('shows a field message under the field it belongs to', async () => {
    stubAuthRuntime()
    table[writeOf(AccountEndpoint.Login)] = { status: 400, body: { errors: { email: ['Enter a valid email.'] } } }
    const wrapper = await mountAuthPage(LoginPage)

    await signIn(wrapper)

    await waitUntil(() => expect(wrapper.text()).toContain('Enter a valid email.'))
  })

  it('swaps the password form for the code step when a second factor is required', async () => {
    const { push } = stubAuthRuntime()
    table[writeOf(AccountEndpoint.Login)] = { body: CHALLENGE }
    const wrapper = await mountAuthPage(LoginPage)

    await reachCodeStep(wrapper)

    expect(wrapper.find('[data-testid="login-form"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="login-2fa-submit"]').attributes('disabled')).toBeDefined()
    expect(push).not.toHaveBeenCalled()
  })

  it('finishes the sign-in with the typed code and follows the return path', async () => {
    const { push } = stubAuthRuntime()
    table[writeOf(AccountEndpoint.Login)] = { body: CHALLENGE }
    table[writeOf(AccountEndpoint.TwoFactorLogin)] = { body: makeClientUser() }
    const wrapper = await mountAuthPage(LoginPage)
    await reachCodeStep(wrapper)

    await typeCode(wrapper, '123456')
    await waitUntil(() => expect(push).toHaveBeenCalledTimes(1))

    expect(bodyOf(requestsTo(stub.requests, AccountEndpoint.TwoFactorLogin)[0])).toEqual({ pendingToken: 'p', code: '123456' })
  })

  it('shows the refusal, clears the code and stays on the step when the code is wrong', async () => {
    const { push } = stubAuthRuntime()
    table[writeOf(AccountEndpoint.Login)] = { body: CHALLENGE }
    table[writeOf(AccountEndpoint.TwoFactorLogin)] = WRONG_CODE
    const wrapper = await mountAuthPage(LoginPage)
    await reachCodeStep(wrapper)

    await typeCode(wrapper, '000000')
    await waitUntil(() => expect(wrapper.find('[data-testid="login-error"]').exists()).toBe(true))
    await flushPromises()

    expect(wrapper.find('[data-testid="login-error"]').text()).toBe('Invalid code.')
    expect(wrapper.find('[data-testid="login-2fa-form"]').exists()).toBe(true)
    expect((wrapper.find('[data-testid="otp-input-0"]').element as HTMLInputElement).value).toBe('')
    expect(push).not.toHaveBeenCalled()
  })

  it('goes back to the password form from the code step', async () => {
    stubAuthRuntime()
    table[writeOf(AccountEndpoint.Login)] = { body: CHALLENGE }
    const wrapper = await mountAuthPage(LoginPage)
    await reachCodeStep(wrapper)

    await wrapper.find('[data-testid="login-2fa-cancel"]').trigger('click')

    expect(wrapper.find('[data-testid="login-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="login-2fa-form"]').exists()).toBe(false)
  })
  it('sends the login request again after a wrong code and a step back', async () => {
    stubAuthRuntime()
    table[writeOf(AccountEndpoint.Login)] = { body: CHALLENGE }
    table[writeOf(AccountEndpoint.TwoFactorLogin)] = WRONG_CODE
    const wrapper = await mountAuthPage(LoginPage)
    await reachCodeStep(wrapper)
    await typeCode(wrapper, '000000')
    await waitUntil(() => expect(wrapper.find('[data-testid="login-error"]').exists()).toBe(true))

    await wrapper.find('[data-testid="login-2fa-cancel"]').trigger('click')
    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(requestsTo(stub.requests, AccountEndpoint.Login)).toHaveLength(2))
  })

  it('does not leave the sign-in button disabled when the step is left while a code is in flight', async () => {
    stubAuthRuntime()
    const held = heldReply()
    table[writeOf(AccountEndpoint.Login)] = { body: CHALLENGE }
    table[writeOf(AccountEndpoint.TwoFactorLogin)] = held.reply
    const wrapper = await mountAuthPage(LoginPage)
    await reachCodeStep(wrapper)
    await typeCode(wrapper, '000000')
    await waitUntil(() => expect(held.waiting()).toBe(1))

    await wrapper.find('[data-testid="login-2fa-cancel"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="login-submit"]').attributes('disabled')).toBeUndefined()

    held.release(WRONG_CODE)
    await settle()
    expect(wrapper.find('[data-testid="login-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="login-submit"]').attributes('disabled')).toBeUndefined()
  })
})

describe('client login page (SSO mode)', () => {
  it('shows one button to the identity provider and no form', async () => {
    stubAuthRuntime({ isSso: true })
    const wrapper = await mountAuthPage(LoginPage)

    expect(wrapper.find('[data-testid="login-sso"]').attributes('href')).toBe(SSO_AUTHORIZE_PATH)
    expect(wrapper.find('form').exists()).toBe(false)
    expect(wrapper.find('[data-testid="login-register"]').exists()).toBe(false)
  })
})

describe('client login page (demo mode)', () => {
  /** The published customer account used by these tests. */
  const CLIENT = { role: DemoAccountRole.Client, email: 'client@demo.test', password: 'demo-pass' }

  /**
   * Mounts the page with the demo store already holding the given status.
   *
   * @param enabled - Whether the deployment is a demo.
   * @returns The mounted page.
   */
  const mountWithDemo = async (enabled: boolean) => {
    const pinia = createPinia()
    setActivePinia(pinia)
    const demo = useDemoStore()
    demo.enabled = enabled
    demo.accounts = [{ role: DemoAccountRole.Admin, email: 'admin@demo.test', password: 'x' }, CLIENT]
    return mountPage(LoginPage, { pinia, components: { Logo: APP_LOGO_STUB } })
  }

  it('offers no demo account outside a demo', async () => {
    stubAuthRuntime()
    const wrapper = await mountWithDemo(false)

    expect(wrapper.find('[data-testid="login-demo"]').exists()).toBe(false)
  })

  it('shows the client account, never the admin one, in a demo', async () => {
    stubAuthRuntime()
    const wrapper = await mountWithDemo(true)

    expect(wrapper.find('[data-testid="login-demo"]').exists()).toBe(true)
    expect(wrapper.html()).not.toContain('admin@demo.test')
  })

  it('keeps the sign-in form visible beside the demo hint', async () => {
    stubAuthRuntime()
    const wrapper = await mountWithDemo(true)

    expect(wrapper.find('[data-testid="login-form"]').exists()).toBe(true)
  })

  it('fills the form when the demo button is pressed', async () => {
    stubAuthRuntime()
    const wrapper = await mountWithDemo(true)

    await wrapper.find('[data-testid="login-demo-use"]').trigger('click')

    expect((wrapper.find('[data-testid="login-email"]').element as HTMLInputElement).value).toBe(CLIENT.email)
    expect((wrapper.find('[data-testid="login-password"]').element as HTMLInputElement).value).toBe(CLIENT.password)
  })
})

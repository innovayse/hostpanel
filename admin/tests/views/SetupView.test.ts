/**
 * Mount tests for SetupView: the first step asks for the account, a refusal shows the API's
 * sentence, and a created account moves on to the Admin claim.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import SetupView from '@/views/SetupView.vue'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { json, problem } from '../helpers/fetch'

/**
 * Types a complete account into the first step and submits it.
 *
 * @param wrapper - The mounted screen.
 */
const submitAccount = async (wrapper: Awaited<ReturnType<typeof mountRouted>>['wrapper']) => {
  await wrapper.get('[data-testid="setup-first-name"]').setValue('Ada')
  await wrapper.get('[data-testid="setup-last-name"]').setValue('Lovelace')
  await wrapper.get('[data-testid="setup-email"]').setValue('ada@example.com')
  await wrapper.get('[data-testid="setup-password"]').setValue('pw')
  await wrapper.get('[data-testid="setup-account-form"]').trigger('submit')
  await flushPromises()
}

describe('SetupView', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('starts with the account form', async () => {
    stubRoutes({})
    const { wrapper } = await mountRouted(SetupView, '/setup', '/setup')
    await flushPromises()

    expect(wrapper.find('[data-testid="setup-account-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="setup-first-name"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="setup-email"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="setup-password"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="setup-claim-form"]').exists()).toBe(false)
  })

  it('shows the API sentence when the registration is refused', async () => {
    stubRoutes({ 'POST /api/auth/register': () => problem('That address is taken.', 409) })
    const { wrapper } = await mountRouted(SetupView, '/setup', '/setup')

    await submitAccount(wrapper)

    expect(wrapper.get('[data-testid="setup-error"]').text()).toContain('That address is taken.')
    expect(wrapper.find('[data-testid="setup-claim-form"]').exists()).toBe(false)
  })

  it('moves on to the Admin claim once the account exists and is signed in', async () => {
    stubRoutes({
      'POST /api/auth/register': () => json({ userId: 'u1' }),
      'POST /api/auth/login': () => json({ accessToken: 'token', expiresIn: 60 }),
      'GET /api/auth/me': () => json({ email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace', roles: [], emailVerified: true }),
    })
    const { wrapper } = await mountRouted(SetupView, '/setup', '/setup')

    await submitAccount(wrapper)

    expect(wrapper.find('[data-testid="setup-claim-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="setup-claim-admin"]').exists()).toBe(true)
  })
})

/**
 * Tests for `pages/client/forgot-password.vue`: the shared reset-request panel in local mode and
 * a hand-off to the identity provider's own page under SSO.
 *
 * @module tests/pages/client/forgot-password.test
 */

import { describe, expect, it } from 'vitest'
import { mountPage } from '~/tests/helpers/mountClient'
import { stubAuthRuntime } from '~/tests/helpers/authScreens'
import ForgotPasswordPage from '~/pages/client/forgot-password.vue'

describe('client forgot-password page', () => {
  it('renders the reset-request form in local mode and does not navigate away', async () => {
    stubAuthRuntime({ isSso: false })

    const wrapper = await mountPage(ForgotPasswordPage)

    expect(wrapper.find('[data-testid="forgot-form"]').exists()).toBe(true)
    expect(navigateTo).not.toHaveBeenCalled()
  })

  it('sends the visitor to the identity provider reset page under SSO', async () => {
    stubAuthRuntime({ isSso: true, publicConfig: { ssoPublicUrl: 'https://sso.example.test' } })

    await mountPage(ForgotPasswordPage)

    expect(navigateTo).toHaveBeenCalledWith('https://sso.example.test/forgot-password', { external: true })
  })

  it('renders the local form under SSO when no SSO address is configured', async () => {
    stubAuthRuntime({ isSso: true, publicConfig: { ssoPublicUrl: '' } })

    const wrapper = await mountPage(ForgotPasswordPage)

    expect(navigateTo).not.toHaveBeenCalled()
    expect(wrapper.find('[data-testid="forgot-form"]').exists()).toBe(true)
  })
})

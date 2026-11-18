/**
 * Tests for `pages/client/forgot.vue`: the page is the shared reset-request panel.
 *
 * @module tests/pages/client/forgot.test
 */

import { describe, expect, it } from 'vitest'
import { mountPage } from '~/tests/helpers/mountClient'
import { stubAuthRuntime } from '~/tests/helpers/authScreens'
import ForgotPage from '~/pages/client/forgot.vue'

describe('client forgot page', () => {
  it('renders the reset-request form', async () => {
    stubAuthRuntime()

    const wrapper = await mountPage(ForgotPage)

    expect(wrapper.find('[data-testid="forgot-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="auth-shell-title"]').text()).toBe('client.forgot.title')
  })
})

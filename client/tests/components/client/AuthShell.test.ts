/**
 * Unit tests for ClientAuthShell: the frame around every signed-out client page.
 *
 * @module tests/components/client/AuthShell.test
 */

import { beforeEach, describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import { useTemplateGlobals } from '~/tests/helpers/templateGlobals'
import AuthShell from '~/components/client/AuthShell.vue'

/** The stub server and the loader of the operator's settings the real logo reads; the hooks start the stub before each test and close it after. */
const { loadGlobals } = useTemplateGlobals()

describe('ClientAuthShell', () => {
  beforeEach(async () => {
    await loadGlobals()
  })

  it('renders the title, the subtitle and the slot inside the card', () => {
    const wrapper = mountClient(AuthShell, {
      props: { title: 'Sign in', subtitle: 'Welcome back' },
      slots: { default: 'card body' }
    })

    expect(wrapper.find('[data-testid="auth-shell-title"]').text()).toBe('Sign in')
    expect(wrapper.find('[data-testid="auth-shell-subtitle"]').text()).toBe('Welcome back')
    expect(wrapper.text()).toContain('card body')
  })

  it('draws the aurora backdrop', () => {
    const wrapper = mountClient(AuthShell)

    expect(wrapper.find('[data-testid="ui-backdrop"]').attributes('data-variant')).toBe('aurora')
  })

  it('shows the logo by default and hides it on request', () => {
    expect(mountClient(AuthShell).find('[data-testid="auth-logo"] [data-testid="app-logo-light"]').exists()).toBe(true)
    expect(mountClient(AuthShell, { props: { showLogo: false } }).find('[data-testid="auth-logo"]').exists()).toBe(false)
  })

  it('omits the heading when there is no title', () => {
    const wrapper = mountClient(AuthShell)

    expect(wrapper.find('[data-testid="auth-shell-title"]').exists()).toBe(false)
  })

  it('renders the footer slot only when one is given', () => {
    const without = mountClient(AuthShell)
    const withFooter = mountClient(AuthShell, { slots: { footer: 'no account?' } })

    expect(without.text()).not.toContain('no account?')
    expect(withFooter.text()).toContain('no account?')
  })
})

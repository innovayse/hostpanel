/**
 * Unit tests for `LayoutSignInPrompt`: shown only when the address carries `?signin=required` and
 * nobody is signed in; its button starts an interactive sign-in that returns to the page with the
 * account hint, and dismissing drops the flag from the address.
 *
 * @module tests/components/layout/SignInPrompt.test
 */

import { describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import LayoutSignInPrompt from '~/components/layout/SignInPrompt.vue'

/** What the stubbed route carries. */
interface RouteState {
  /** The parsed query. */
  query: Record<string, string>
  /** The full path with its query. */
  fullPath: string
}

/**
 * Mounts the prompt on a route, with or without the signed-in hint.
 *
 * @param route - The current route.
 * @param authed - The value of the readable signed-in cookie.
 * @returns The wrapper and the router spy.
 */
const mountPrompt = (route: RouteState, authed: string | null = null) => {
  const replace = vi.fn()
  vi.stubGlobal('useRoute', () => ({ params: {}, path: route.fullPath.split('?')[0], meta: {}, ...route }))
  vi.stubGlobal('useRouter', () => ({ push: vi.fn(), replace, back: vi.fn() }))
  vi.stubGlobal('useCookie', () => ref<string | null>(authed))
  return { wrapper: mountWithKit(LayoutSignInPrompt), replace }
}

describe('LayoutSignInPrompt', () => {
  it('renders nothing without the flag', () => {
    const { wrapper } = mountPrompt({ query: {}, fullPath: '/hosting' })

    expect(wrapper.find('[data-testid="signin-prompt"]').exists()).toBe(false)
  })

  it('renders nothing for a signed-in visitor, even with the flag', () => {
    const { wrapper } = mountPrompt({ query: { signin: 'required' }, fullPath: '/hosting?signin=required' }, '1')

    expect(wrapper.find('[data-testid="signin-prompt"]').exists()).toBe(false)
  })

  it('shows the message and a sign-in that returns to the page with the account hint', () => {
    const { wrapper } = mountPrompt({
      query: { signin: 'required', authuser: 'person@example.test' },
      fullPath: '/hosting?authuser=person%40example.test&signin=required',
    })

    expect(wrapper.get('[data-testid="signin-prompt-text"]').text()).toBe('signInPrompt.message')
    expect(wrapper.get('[data-testid="signin-prompt-signin"]').text()).toBe('common.signIn')
    expect(wrapper.get('[data-testid="signin-prompt-dismiss"]').text()).toBe('ui.close')
    const href = wrapper.get('[data-testid="signin-prompt-signin"]').attributes('href') ?? ''
    const target = new URL(href, 'http://portal.test')
    expect(target.pathname).toBe('/api/portal/auth/sso/authorize')
    expect(target.searchParams.get('returnTo')).toBe('/hosting?authuser=person%40example.test')
    expect(target.searchParams.get('authuser')).toBe('person@example.test')
  })

  it('drops the flag from the address when dismissed', async () => {
    const { wrapper, replace } = mountPrompt({ query: { signin: 'required' }, fullPath: '/pricing?plan=pro&signin=required' })

    await wrapper.get('[data-testid="signin-prompt-dismiss"]').trigger('click')

    expect(replace).toHaveBeenCalledWith('/pricing?plan=pro')
  })
})

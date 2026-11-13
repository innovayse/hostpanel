/**
 * Mount tests for ForbiddenView: the access-denied screen explains itself and signing out returns to the sign-in screen.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import ForbiddenView from '@/views/ForbiddenView.vue'
import { useAuthStore } from '@/stores/auth'
import { AuthMode } from '@/types/auth'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

describe('ForbiddenView', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the access-denied title', async () => {
    stubRoutes({})
    const { wrapper } = await mountRouted(ForbiddenView, '/forbidden', '/forbidden')

    expect(wrapper.get('[data-testid="auth-title"]').text()).toBe('No access')
  })

  it('signs out and returns to the sign-in screen', async () => {
    stubRoutes({ 'POST /api/auth/logout': () => new Response(null, { status: 204 }) })
    const { wrapper, router } = await mountRouted(ForbiddenView, '/forbidden', '/forbidden')
    // The local sign-out is a plain request; the single sign-on one hands the browser to another address.
    useAuthStore().mode = AuthMode.Local

    await wrapper.get('[data-testid="forbidden-sign-out"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.path).toBe('/login')
  })
})

/**
 * Mount tests for NotFoundView: the catch-all screen explains itself and offers the way back to the panel.
 */
import { describe, expect, it } from 'vitest'
import NotFoundView from '@/views/NotFoundView.vue'
import { mountRouted } from '../helpers/mount'

describe('NotFoundView', () => {
  it('shows the not-found title and a link back to the panel', async () => {
    const { wrapper } = await mountRouted(NotFoundView, '/nowhere', '/nowhere')

    expect(wrapper.get('[data-testid="auth-title"]').text()).toBe('Page not found')
    expect(wrapper.get('[data-testid="not-found-back"]').attributes('href')).toBe('/dashboard')
  })
})

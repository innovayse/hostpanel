/**
 * Mount tests for AppLoading: the centred busy ring the page shows before the router has settled where it is going.
 */
import { describe, expect, it } from 'vitest'
import AppLoading from '@/components/layout/AppLoading.vue'
import { mountWithApp } from '../../helpers/mount'

describe('AppLoading', () => {
  it('fills the viewport and centres a busy status region', () => {
    const root = mountWithApp(AppLoading).get('[data-testid="app-loading"]')

    expect(root.classes()).toContain('min-h-dvh')
    expect(root.classes()).toContain('items-center')
    expect(root.classes()).toContain('justify-center')
    expect(root.get('[data-testid="ui-loader"]').attributes('role')).toBe('status')
    expect(root.get('[data-testid="ui-loader"]').attributes('aria-busy')).toBe('true')
  })

  it('writes the shared loading label beside the ring', () => {
    const label = mountWithApp(AppLoading).get('[data-testid="ui-loader-label"]')

    expect(label.text()).toBe('Loading')
    expect(label.classes()).not.toContain('sr-only')
  })
})

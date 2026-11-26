/**
 * Unit tests for BrandLogo: which file each theme shows, and that operator uploads win,
 * a single upload covering both themes.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import BrandLogo from '@/components/layout/BrandLogo.vue'
import { useBrandingStore } from '@/stores/branding'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the logo after seeding the branding store.
 *
 * @param logo - The light-background upload, or `''`.
 * @param logoDark - The dark-background upload, or `''`.
 * @returns The wrapper.
 */
const mountLogo = (logo: string, logoDark: string): VueWrapper => {
  const branding = useBrandingStore()
  branding.logoUrl = logo
  branding.logoDarkUrl = logoDark
  return mount(BrandLogo, { global: { plugins: [createTestI18n()] } })
}

/**
 * The image shown in the dark theme (the default): visible unless `.light` is on `<html>`.
 *
 * @param wrapper - The mounted logo.
 * @returns The dark-theme image.
 */
const darkImage = (wrapper: VueWrapper) => wrapper.get('[data-testid="brand-logo-dark"]')

/**
 * The image shown in the light theme: hidden unless `.light` is on `<html>`.
 *
 * @param wrapper - The mounted logo.
 * @returns The light-theme image.
 */
const lightImage = (wrapper: VueWrapper) => wrapper.get('[data-testid="brand-logo-light"]')

describe('BrandLogo', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('shows the light-text wordmark in the dark theme', () => {
    const wrapper = mountLogo('', '')

    expect(darkImage(wrapper).attributes('src')).toBe('/logo-dark.svg')
    expect(darkImage(wrapper).classes()).toContain('light:hidden')
  })

  it('shows the dark-text wordmark in the light theme', () => {
    const wrapper = mountLogo('', '')

    expect(lightImage(wrapper).attributes('src')).toBe('/logo.svg')
    expect(lightImage(wrapper).classes()).toEqual(expect.arrayContaining(['hidden', 'light:block']))
  })

  it('uses both operator uploads, each in its own theme', () => {
    const wrapper = mountLogo('/l.png', '/d.png')

    expect(darkImage(wrapper).attributes('src')).toBe('/d.png')
    expect(lightImage(wrapper).attributes('src')).toBe('/l.png')
  })

  it('uses a single upload in both themes', () => {
    const onlyLight = mountLogo('/l.png', '')
    expect(darkImage(onlyLight).attributes('src')).toBe('/l.png')
    expect(lightImage(onlyLight).attributes('src')).toBe('/l.png')

    const onlyDark = mountLogo('', '/d.png')
    expect(darkImage(onlyDark).attributes('src')).toBe('/d.png')
    expect(lightImage(onlyDark).attributes('src')).toBe('/d.png')
  })

  it('lays a light plate behind a lone light-background upload in the dark theme only', () => {
    const lone = mountLogo('/l.png', '')
    expect(darkImage(lone).classes()).toContain('bg-surface-paper/90')
    expect(lightImage(lone).classes()).not.toContain('bg-surface-paper/90')

    expect(darkImage(mountLogo('/l.png', '/d.png')).classes()).not.toContain('bg-surface-paper/90')
    expect(darkImage(mountLogo('', '')).classes()).not.toContain('bg-surface-paper/90')
  })

  it('names the brand for screen readers', () => {
    const wrapper = mountLogo('', '')

    expect(darkImage(wrapper).attributes('alt')).toBe('Hostpanel')
    expect(lightImage(wrapper).attributes('alt')).toBe('Hostpanel')
  })
})

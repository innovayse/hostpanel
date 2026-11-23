/**
 * Unit tests for UiRowLink: the root follows `to` / `href`, the look gives the shape, and only a live link lights up.
 */
import { describe, expect, it } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import UiRowLink from '@/components/ui/UiRowLink.vue'
import { UiRowLinkLook } from '@/utils/ui/rowLink'

/**
 * Mounts a row link with a content.
 *
 * @param props - Props of the link.
 * @returns The wrapper.
 */
const mountLink = (props: Record<string, unknown>) => mount(UiRowLink, {
  props,
  slots: { default: 'Body' },
  global: { stubs: { RouterLink: RouterLinkStub } },
})

describe('UiRowLink', () => {
  it('renders a router link for `to`', () => {
    const wrapper = mountLink({ to: '/invoices' })

    expect(wrapper.findComponent(RouterLinkStub).props('to')).toBe('/invoices')
    expect(wrapper.text()).toBe('Body')
  })

  it('renders an anchor for `href`, safe when external', () => {
    const wrapper = mountLink({ href: 'https://example.com', external: true })

    expect(wrapper.element.tagName).toBe('A')
    expect(wrapper.attributes('href')).toBe('https://example.com')
    expect(wrapper.attributes('target')).toBe('_blank')
    expect(wrapper.attributes('rel')).toBe('noopener noreferrer')
  })

  it('gives the shape of the look', () => {
    expect(mountLink({ to: '/a', look: UiRowLinkLook.Tile }).classes()).toContain('bg-surface-row')
    expect(mountLink({ to: '/a', look: UiRowLinkLook.Card }).classes()).toContain('rounded-card')
    expect(mountLink({ to: '/a', look: UiRowLinkLook.Divided }).classes()).toContain('border-b')
  })

  it('lights up and shows focus only when live', () => {
    const live = mountLink({ to: '/a' })
    const placeholder = mountLink({})

    expect(live.classes()).toContain('hover:bg-surface-hover')
    expect(live.classes()).toContain('focus-visible:ring-2')
    expect(placeholder.classes()).not.toContain('hover:bg-surface-hover')
    expect(placeholder.attributes('href')).toBeUndefined()
  })

  it('never lights up in the block look', () => {
    expect(mountLink({ to: '/a', look: UiRowLinkLook.Block }).classes()).not.toContain('hover:bg-surface-hover')
  })
})

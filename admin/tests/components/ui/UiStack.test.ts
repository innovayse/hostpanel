/**
 * Unit tests for UiStack: direction, gap, alignment and wrapping classes.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiStack from '@/components/ui/UiStack.vue'

describe('UiStack', () => {
  it('stacks vertically with a medium gap by default', () => {
    const wrapper = mount(UiStack, { slots: { default: 'x' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['flex', 'flex-col', 'gap-4']))
  })

  it('lays out a row with the chosen gap, alignment and distribution', () => {
    const wrapper = mount(UiStack, {
      props: { direction: 'row', gap: 'sm', align: 'center', justify: 'between', wrap: true },
    })

    expect(wrapper.classes()).toEqual(expect.arrayContaining([
      'flex-row', 'gap-2', 'items-center', 'justify-between', 'flex-wrap',
    ]))
  })

  it('turns a row into a full-width column below md when asked', () => {
    const wrapper = mount(UiStack, { props: { direction: 'row', reflowBelowMd: true } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['flex-row', 'max-md:flex-col', 'max-md:items-stretch']))
  })

  it('turns a column into a wrapping, centred line below md when asked', () => {
    const wrapper = mount(UiStack, { props: { reflowBelowMd: true } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['flex-col', 'max-md:flex-row', 'max-md:flex-wrap', 'max-md:items-center']))
  })

  it('does not reflow unless asked', () => {
    expect(mount(UiStack, { props: { direction: 'row' } }).classes().some(c => c.startsWith('max-md:'))).toBe(false)
  })

  it('shows itself only from sm up when visibleFrom is sm, replacing its plain flex', () => {
    const classes = mount(UiStack, { props: { visibleFrom: 'sm' } }).classes()

    expect(classes).toEqual(expect.arrayContaining(['hidden', 'sm:flex']))
    expect(classes).not.toContain('flex')
  })

  it('shows itself only from md up when visibleFrom is md', () => {
    expect(mount(UiStack, { props: { visibleFrom: 'md' } }).classes()).toEqual(expect.arrayContaining(['hidden', 'md:flex']))
  })

  it('shows itself only below sm when hiddenFrom is sm', () => {
    expect(mount(UiStack, { props: { hiddenFrom: 'sm' } }).classes()).toEqual(expect.arrayContaining(['flex', 'sm:hidden']))
  })

  it('is a plain flex box when no breakpoint is asked for', () => {
    const classes = mount(UiStack).classes()

    expect(classes).toContain('flex')
    expect(classes.some(c => c === 'hidden' || c.startsWith('sm:'))).toBe(false)
  })

  it('passes attributes through', () => {
    expect(mount(UiStack, { attrs: { 'data-testid': 'probe' } }).attributes('data-testid')).toBe('probe')
  })
})

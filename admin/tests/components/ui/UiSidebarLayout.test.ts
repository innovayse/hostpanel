/**
 * Tests for UiSidebarLayout: the sidebar slot beside the scrolling content slot.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiSidebarLayout from '@/components/ui/UiSidebarLayout.vue'

describe('UiSidebarLayout', () => {
  it('renders the sidebar and the content, content scrolling on its own', () => {
    const wrapper = mount(UiSidebarLayout, {
      slots: {
        sidebar: '<nav data-testid="side">side</nav>',
        default: '<p data-testid="content">content</p>',
      },
    })

    expect(wrapper.find('[data-testid="side"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="content"]').element.parentElement?.classList.contains('overflow-auto')).toBe(true)
  })

  it('puts the sidebar before the content', () => {
    const wrapper = mount(UiSidebarLayout, {
      slots: { sidebar: '<nav data-testid="side" />', default: '<p data-testid="content" />' },
    })

    expect(wrapper.element.firstElementChild?.getAttribute('data-testid')).toBe('side')
  })

  it('is exactly as tall as the scroller it sits in and lets both columns shrink, so the sidebar and the content scroll by themselves', () => {
    const wrapper = mount(UiSidebarLayout, { slots: { sidebar: '<nav />', default: '<p data-testid="content" />' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['h-[100cqh]', 'min-h-0', 'min-w-0']))
    expect(wrapper.classes()).not.toContain('h-full')
    expect(wrapper.get('[data-testid="content"]').element.parentElement?.classList.contains('min-w-0')).toBe(true)
  })

  it('cancels the shell gutter exactly, so the sidebar starts on the top bar line', () => {
    const wrapper = mount(UiSidebarLayout, { slots: { sidebar: '<nav />', default: '<p />' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['lg:-mx-8', 'lg:-my-7', 'sm:-my-6', '-my-4']))
  })

  it('keeps the sidebar on every width and draws no strip when it is given none', () => {
    const wrapper = mount(UiSidebarLayout, { slots: { sidebar: '<nav data-testid="side" />', default: '<p />' } })

    expect(wrapper.get('[data-testid="side"]').element.parentElement).toBe(wrapper.element)
    expect(wrapper.find('[role="tablist"]').exists()).toBe(false)
  })

  it('hides the sidebar below md and shows the strip there, marking the open entry and reporting a chosen one', async () => {
    const wrapper = mount(UiSidebarLayout, {
      props: { strip: [{ value: 'a', label: 'A', testid: 'tab-a' }, { value: 'b', label: 'B', testid: 'tab-b' }], stripValue: 'b', stripTestid: 'the-strip' },
      slots: { sidebar: '<nav data-testid="side" />', default: '<p />' },
    })

    expect(wrapper.get('[data-testid="side"]').element.parentElement?.classList.contains('hidden')).toBe(true)
    expect(wrapper.get('[data-testid="side"]').element.parentElement?.classList.contains('md:flex')).toBe(true)
    expect(wrapper.get('[data-testid="the-strip"]').classes()).toContain('md:hidden')
    expect(wrapper.get('[data-testid="tab-b"]').attributes('aria-selected')).toBe('true')

    await wrapper.get('[data-testid="tab-a"]').trigger('click')

    expect(wrapper.emitted('selectStrip')).toEqual([['a']])
  })

  it('draws the strip inert when asked, as a placeholder', () => {
    const wrapper = mount(UiSidebarLayout, { props: { strip: [{ value: 'a', label: 'A' }], stripTestid: 'the-strip', stripInert: true }, slots: { default: '<p />' } })

    expect(wrapper.get('[data-testid="the-strip"]').attributes('inert')).toBeDefined()
  })
})

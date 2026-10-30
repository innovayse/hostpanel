/**
 * Unit tests for PluginWidget: its title and Hide event, the body under its own skeleton, and the
 * failure boundary: a throwing body or one that never settles is replaced by a note inside this
 * card, and no timer outlives the widget.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import PluginWidget from '@/components/plugins/PluginWidget.vue'
import { mountWithApp } from '../../helpers/mount'

/** The widget under test. */
const widget = { pluginId: 'sites', widgetId: 'usage', label: 'Site usage', order: 1 }

/** A body that renders at once. */
const Healthy = defineComponent({ render: () => h('p', { 'data-testid': 'body' }, 'All good') })

/** A body that settles one tick after it starts, so the widget has a pending body to time. */
const Settling = defineComponent({
  setup: async () => {
    await Promise.resolve()
    return () => h('p', { 'data-testid': 'body' }, 'Settled')
  },
})

/** A body whose setup throws. */
const Throwing = defineComponent({
  setup: () => {
    throw new Error('plugin exploded')
  },
  render: () => null,
})

/** A body that never settles. */
const Never = defineComponent({
  setup: async () => {
    await new Promise<void>(() => undefined)
    return () => h('p', 'never shown')
  },
})

/**
 * Mounts the widget with the body in its default slot.
 *
 * @param body - The component rendered inside the widget.
 * @returns The wrapper.
 */
const mountWithBody = (body: ReturnType<typeof defineComponent>) =>
  mountWithApp(
    defineComponent({ render: () => h(PluginWidget, { widget }, { default: () => h(body) }) }),
  )

beforeEach(() => {
  vi.useFakeTimers()
})

afterEach(() => {
  vi.useRealTimers()
})

describe('PluginWidget', () => {
  it('shows the title and the body', async () => {
    const wrapper = mountWithBody(Healthy)
    await flushPromises()

    expect(wrapper.text()).toContain('Site usage')
    expect(wrapper.get('[data-testid="body"]').text()).toBe('All good')
    expect(wrapper.find('[data-testid="plugin-widget-note-sites-usage"]').exists()).toBe(false)
  })

  it('emits hide from the Hide button', async () => {
    const wrapper = mountWithApp(PluginWidget, { props: { widget } })

    await wrapper.get('[data-testid="plugin-widget-hide-sites-usage"]').trigger('click')

    expect(wrapper.emitted('hide')).toHaveLength(1)
  })

  it('shows its own skeleton while the body loads', async () => {
    const wrapper = mountWithBody(Never)
    await flushPromises()

    expect(wrapper.find('[data-testid="plugin-widget-loading-sites-usage"]').exists()).toBe(true)
  })

  it('replaces a throwing body with a note and keeps the title', async () => {
    const wrapper = mountWithBody(Throwing)
    await flushPromises()

    expect(wrapper.get('[data-testid="plugin-widget-note-sites-usage"]').text()).toBe('This widget could not be shown.')
    expect(wrapper.text()).toContain('Site usage')
  })

  it('gives up on a body that never settles', async () => {
    const wrapper = mountWithBody(Never)
    await flushPromises()

    await vi.advanceTimersByTimeAsync(10_000)

    expect(wrapper.get('[data-testid="plugin-widget-note-sites-usage"]').text()).toBe('This widget took too long to load.')
    expect(wrapper.find('[data-testid="plugin-widget-loading-sites-usage"]').exists()).toBe(false)
  })

  it('starts its timer for a pending body and clears it when the body settles', async () => {
    const start = vi.spyOn(globalThis, 'setTimeout')
    const wrapper = mountWithBody(Settling)

    // The body is still pending: the timeout is running.
    expect(start).toHaveBeenCalledWith(expect.any(Function), 10_000)
    expect(vi.getTimerCount()).toBe(1)

    await flushPromises()

    expect(wrapper.get('[data-testid="body"]').text()).toBe('Settled')
    expect(vi.getTimerCount()).toBe(0)
    await vi.advanceTimersByTimeAsync(10_000)
    expect(wrapper.find('[data-testid="plugin-widget-note-sites-usage"]').exists()).toBe(false)
    start.mockRestore()
  })

  it('clears its timer when destroyed while the body is pending', async () => {
    const wrapper = mountWithBody(Never)
    await flushPromises()
    expect(vi.getTimerCount()).toBe(1)

    wrapper.unmount()

    expect(vi.getTimerCount()).toBe(0)
  })
})

/**
 * Unit tests for PluginSlot: nothing renders without widgets, one card per widget, a throwing, a
 * pending and a healthy widget side by side leave each other alone, another record never shows
 * the previous record's widgets, and Hide goes through the slot's setting row.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import PluginSlot from '@/components/plugins/PluginSlot.vue'
import { PluginSlotId } from '@/types/pluginslot'
import type { PluginSlotWidgetDto } from '@/types/pluginslot'
import { json, problem } from '../../helpers/fetch'
import { mountWithApp } from '../../helpers/mount'
import { stubRoutes } from '../../helpers/routes'

/**
 * Builds a widget.
 *
 * @param widgetId - Id of the widget; also its label.
 * @param order - Position in the slot.
 * @returns The widget.
 */
const widget = (widgetId: string, order = 1): PluginSlotWidgetDto => ({ pluginId: 'sites', widgetId, label: widgetId, order })

/** A body that renders at once. */
const Healthy = defineComponent({ render: () => h('p', { 'data-testid': 'body-ok' }, 'fine') })

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

/** The page: a slot whose widgets get a body by their id. */
const Page = defineComponent({
  props: { clientId: { type: Number, required: true } },
  setup: (props) => () =>
    h(
      PluginSlot,
      { slotId: PluginSlotId.AdminClientPanels, context: { clientId: props.clientId } },
      {
        widget: ({ widget: entry }: { widget: PluginSlotWidgetDto }) =>
          h(entry.widgetId === 'boom' ? Throwing : entry.widgetId === 'slow' ? Never : Healthy),
      },
    ),
})

/** The hide setting row of the client panels slot. */
const hiddenRow = { id: 5, key: 'plugins.slots.admin.client.panels.hidden', value: null, isSecret: false, hasValue: false }

beforeEach(() => {
  vi.useFakeTimers()
})

afterEach(() => {
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

describe('PluginSlot', () => {
  it('renders nothing when no plugin offers a widget', async () => {
    stubRoutes({ 'GET /api/admin/plugins/slots': () => json([]) })
    const wrapper = mountWithApp(Page, { props: { clientId: 1 } })
    await flushPromises()

    expect(wrapper.find('[data-testid^="plugin-slot-"]').exists()).toBe(false)
    expect(wrapper.text()).toBe('')
  })

  it('renders nothing when the slot cannot be loaded', async () => {
    stubRoutes({ 'GET /api/admin/plugins/slots': () => problem('Client not found.', 404) })
    const wrapper = mountWithApp(Page, { props: { clientId: 1 } })
    await flushPromises()

    expect(wrapper.text()).toBe('')
  })

  it('keeps a throwing and a pending widget from breaking the healthy one', async () => {
    stubRoutes({ 'GET /api/admin/plugins/slots': () => json([widget('boom', 1), widget('slow', 2), widget('ok', 3)]) })
    const wrapper = mountWithApp(Page, { props: { clientId: 1 } })
    await flushPromises()

    expect(wrapper.get('[data-testid="plugin-widget-note-sites-boom"]').text()).toBe('This widget could not be shown.')
    expect(wrapper.find('[data-testid="plugin-widget-loading-sites-slow"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="plugin-widget-sites-ok"] [data-testid="body-ok"]').text()).toBe('fine')

    await vi.advanceTimersByTimeAsync(10_000)

    expect(wrapper.get('[data-testid="plugin-widget-note-sites-slow"]').text()).toBe('This widget took too long to load.')
    expect(wrapper.get('[data-testid="plugin-widget-sites-ok"] [data-testid="body-ok"]').text()).toBe('fine')
  })

  it('leaves its neighbours and every timer alone when it is destroyed', async () => {
    stubRoutes({ 'GET /api/admin/plugins/slots': () => json([widget('slow', 1), widget('ok', 2)]) })
    const wrapper = mountWithApp(Page, { props: { clientId: 1 } })
    await flushPromises()
    expect(vi.getTimerCount()).toBe(1)

    wrapper.unmount()

    expect(vi.getTimerCount()).toBe(0)
  })

  it('shows only the new record\'s widgets after the page moves on, even if the old answer is late', async () => {
    let releaseOld: (response: Response) => void = () => undefined
    const old = new Promise<Response>((resolve) => {
      releaseOld = resolve
    })
    const mock = vi.fn((url: string) =>
      url.includes('clientId=1') ? old : Promise.resolve(json([widget('two')])),
    )
    vi.stubGlobal('fetch', mock)
    const wrapper = mountWithApp(Page, { props: { clientId: 1 } })
    await flushPromises()

    await wrapper.setProps({ clientId: 2 })
    await flushPromises()
    releaseOld(json([widget('one')]))
    await flushPromises()

    expect(wrapper.find('[data-testid="plugin-widget-sites-two"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-widget-sites-one"]').exists()).toBe(false)
  })

  it('hides a widget through the slot setting', async () => {
    const routes = stubRoutes({
      'GET /api/admin/plugins/slots': () => json([widget('ok', 1), widget('other', 2)]),
      'GET /api/admin/settings': () => json([hiddenRow]),
      'PUT /api/admin/settings/5': () => new Response(null, { status: 204 }),
    })
    const wrapper = mountWithApp(Page, { props: { clientId: 1 } })
    await flushPromises()

    await wrapper.get('[data-testid="plugin-widget-hide-sites-ok"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="plugin-widget-sites-ok"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-widget-sites-other"]').exists()).toBe(true)
    const put = routes.mock.calls.find((call) => call[1]?.method === 'PUT')
    expect(JSON.parse(String(put?.[1]?.body))).toEqual({ value: 'sites:ok' })
  })

  it('shows the refusal and keeps the widget when the row was never seeded', async () => {
    stubRoutes({
      'GET /api/admin/plugins/slots': () => json([widget('ok', 1)]),
      'GET /api/admin/settings': () => json([]),
    })
    const wrapper = mountWithApp(Page, { props: { clientId: 1 } })
    await flushPromises()

    await wrapper.get('[data-testid="plugin-widget-hide-sites-ok"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="plugin-slot-error"]').text()).toContain('does not exist on this server')
    expect(wrapper.find('[data-testid="plugin-widget-sites-ok"]').exists()).toBe(true)
  })
})

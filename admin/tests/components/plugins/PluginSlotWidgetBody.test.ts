/**
 * Unit tests for PluginSlotWidgetBody: it reads the widget's view for the record, renders it with the
 * plugin view renderer, fills the view's routes from the record and sends the service with every call;
 * a refused read throws so the widget's own boundary can answer.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { defineComponent, h, Suspense } from 'vue'
import PluginSlotWidgetBody from '@/components/plugins/PluginSlotWidgetBody.vue'
import { PluginSlotId } from '@/types/pluginslot'
import type { PluginSlotContext, PluginSlotWidgetDto } from '@/types/pluginslot'
import { json, problem } from '../../helpers/fetch'
import { mountWithApp } from '../../helpers/mount'

afterEach(() => {
  vi.unstubAllGlobals()
})

/** The widget under test. */
const usage: PluginSlotWidgetDto = {
  pluginId: 'sites', id: 'usage', view: 'views/usage.json', title: 'Usage', order: 1, contextKeys: ['serviceId'],
}

/** A view with a stat block read from a route that names the service. */
const view = {
  pluginId: 'sites',
  id: 'usage',
  title: 'Usage',
  externalOrigins: [],
  view: { schema: 1, blocks: [{ kind: 'stat', label: 'Disk', source: { route: { method: 'GET', path: '/services/{serviceId}/usage' }, field: 'disk' } }] },
}

/**
 * Mounts the body under a `<Suspense>`, the way its widget does.
 *
 * @param context - The record the slot is opened for.
 * @returns The wrapper.
 */
const mountBody = (context: PluginSlotContext) =>
  mountWithApp(
    defineComponent({
      setup: () => () =>
        h(Suspense, null, {
          default: () => h(PluginSlotWidgetBody, { slotId: PluginSlotId.AdminServicePanels, context, widget: usage }),
        }),
    }),
  )

describe('PluginSlotWidgetBody', () => {
  it('renders the view with its route filled from the service and the service sent on the call', async () => {
    const mock = vi.fn((url: string) =>
      Promise.resolve(url.includes('/widgets/') ? json(view) : json({ disk: '12 GB' })),
    )
    vi.stubGlobal('fetch', mock)

    const wrapper = mountBody({ serviceId: 9 })
    await flushPromises()

    expect(wrapper.text()).toContain('12 GB')
    const urls = mock.mock.calls.map((call) => String(call[0]))
    expect(urls[0]).toBe('/api/admin/plugins/slots/admin.service.panels/widgets/sites/usage?serviceId=9')
    expect(urls[1]).toBe('/api/admin/plugins/sites/ext/services/9/usage?serviceId=9')
  })

  it('sends no service query for a client page', async () => {
    const mock = vi.fn((url: string) =>
      Promise.resolve(url.includes('/widgets/') ? json({ ...view, view: { schema: 1, blocks: [{ kind: 'stat', label: 'Disk', source: { route: { method: 'GET', path: '/clients/{clientId}/usage' } } }] } }) : json('3')),
    )
    vi.stubGlobal('fetch', mock)

    mountBody({ clientId: 4 })
    await flushPromises()

    expect(String(mock.mock.calls[1]?.[0])).toBe('/api/admin/plugins/sites/ext/clients/4/usage')
  })

  it('throws when the host refuses the view, for the widget to catch', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(problem('Not found.', 404))))
    const errors: unknown[] = []
    const wrapper = mountWithApp(
      defineComponent({
        setup: () => {
          // Stands in for PluginWidget's boundary: it stops the error and records it.
          return () => h(Suspense, null, { default: () => h(PluginSlotWidgetBody, { slotId: PluginSlotId.AdminServicePanels, context: { serviceId: 1 }, widget: usage }) })
        },
        errorCaptured: (error: unknown) => {
          errors.push(error)
          return false
        },
      }),
    )
    await flushPromises()

    expect(errors).toHaveLength(1)
    expect(wrapper.text()).toBe('')
  })
})

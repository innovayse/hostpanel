/**
 * Helpers of the plugin page tests: a sample plugin's address, a server-sent event answer, and a
 * mount of the real view renderer around a few blocks with the plugin's routes stubbed, so a block is
 * tested through the store and the transport it really uses.
 */
import { flushPromises } from '@vue/test-utils'
import { vi } from 'vitest'
import type { VueWrapper } from '@vue/test-utils'
import PluginView from '@/components/plugins/view/PluginView.vue'
import type { PluginBlock } from '@/types/pluginview'
import { mountWithApp } from './mount'
import { stubRouteTable } from './routes'
import type { RouteTable } from './routes'

/** The sample plugin every view test talks to. */
export const SAMPLE_PLUGIN = 'sites'

/**
 * The path of one of the sample plugin's admin routes, as the stubbed route table names it.
 *
 * @param path - The filled path below the plugin's prefix, without a leading `/`.
 * @returns `/admin/plugins/sites/ext/<path>`.
 */
export const ext = (path: string): string => `/admin/plugins/${SAMPLE_PLUGIN}/ext/${path}`

/** One event of a stubbed stream. */
export interface StubEvent {
  /** The event name; none for the default `message`. */
  event?: string
  /** The data, sent as JSON. */
  data: unknown
}

/**
 * Builds a `text/event-stream` answer.
 *
 * @param events - The events, in order.
 * @returns The response.
 */
export const eventStream = (events: readonly StubEvent[]): Response =>
  new Response(
    events.map((entry) => `${entry.event ? `event: ${entry.event}\n` : ''}data: ${JSON.stringify(entry.data)}\n\n`).join(''),
    { status: 200, headers: { 'Content-Type': 'text/event-stream' } },
  )

/** Options of {@link mountPluginView}. */
export interface MountPluginViewOptions {
  /** The plugin's routes by `METHOD /path`. */
  routes?: RouteTable
  /** The page values. */
  values?: Record<string, string>
  /** The plugin's allowed link origins. */
  externalOrigins?: string[]
}

/**
 * Mounts the view renderer around the given blocks and lets the first reads settle.
 *
 * @param blocks - The blocks of the view document.
 * @param options - The stubbed routes, values and origins.
 * @returns The wrapper and the fetch stub.
 */
export const mountPluginView = async (
  blocks: PluginBlock[],
  options: MountPluginViewOptions = {},
): Promise<{ wrapper: VueWrapper; fetch: ReturnType<typeof stubRouteTable> }> => {
  const fetch = stubRouteTable(options.routes ?? {})
  const wrapper = mountWithApp(PluginView, {
    props: {
      pluginId: SAMPLE_PLUGIN,
      view: { schema: 1, blocks },
      values: options.values ?? {},
      externalOrigins: options.externalOrigins ?? [],
    },
  })
  await flushPromises()
  await flushPromises()
  return { wrapper, fetch }
}

/**
 * Mounts the view renderer around the given blocks while every route they read is still in flight, so a
 * block's loading state can be asserted.
 *
 * @param blocks - The blocks of the view document.
 * @returns The wrapper.
 */
export const mountLoadingPluginView = async (blocks: PluginBlock[]): Promise<VueWrapper> => {
  vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
  const wrapper = mountWithApp(PluginView, {
    props: { pluginId: SAMPLE_PLUGIN, view: { schema: 1, blocks }, values: {}, externalOrigins: [] },
  })
  await flushPromises()
  return wrapper
}

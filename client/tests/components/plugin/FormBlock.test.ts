/**
 * Tests for PluginFormBlock: the body as typed (numbers and switches typed), the plugin's sentence, a refusal's
 * sentence and field sentences, refreshing after a success, emptying the fields when asked to, and the follow-up.
 *
 * @module tests/components/plugin/FormBlock.test
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { PLUGIN_FOLLOW_UP_INTERVAL_MS } from '~/constants/pluginFollowUp'
import type { StubServer } from '~/tests/helpers/stubServer'
import { mountBlocks, problem, ROUTES_URL, startPluginApi, TEST_ROUTES } from '~/tests/helpers/mountPlugin'
import { PluginBlockKind } from '~/types/pluginblockkind'
import { PluginFieldType } from '~/types/pluginfieldtype'
import { settle } from '~/tests/helpers/settle'

/** The stub API of the running test, closed after it. */
let api: StubServer | undefined

afterEach(async () => {
  vi.useRealTimers()
  await api?.close()
  api = undefined
  // Cleared before the globals are unstubbed, which takes the setup's `navigateTo` stand-in away with them.
  vi.mocked(navigateTo).mockClear()
  vi.unstubAllGlobals()
})

/** The settings form of the worked example. */
const form = {
  kind: PluginBlockKind.Form,
  title: 'Settings',
  fields: [
    { key: 'name', label: 'Name', type: PluginFieldType.Text, required: true },
    { key: 'pages', label: 'Pages', type: PluginFieldType.Number },
    { key: 'token', label: 'Token', type: PluginFieldType.Secret },
    { key: 'public', label: 'Public', type: PluginFieldType.Boolean },
  ],
  submit: { route: TEST_ROUTES.publish, label: 'Save' },
} as const

describe('PluginFormBlock', () => {
  it('sends every field under its key, a number as a number, a switch as a boolean', async () => {
    api = await startPluginApi({ [`POST ${ROUTES_URL}/sites/7/publish`]: { body: { message: 'Saved.' } } })
    const wrapper = mountBlocks([form])

    await wrapper.get('input[data-testid="plugin-form-name"]').setValue('Blog')
    await wrapper.get('input[data-testid="plugin-form-pages"]').setValue('4')
    await wrapper.get('[data-testid="plugin-form"] form').trigger('submit')
    await settle()

    expect(JSON.parse(api.requests.at(-1)!.body)).toEqual({ name: 'Blog', pages: 4, token: '', public: false })
    expect(wrapper.get('[data-testid="ui-form-success"]').text()).toBe('Saved.')
  })

  it('draws a secret as a password field', () => {
    const wrapper = mountBlocks([form])

    expect(wrapper.get('input[data-testid="plugin-form-token"]').attributes('type')).toBe('password')
  })

  it('shows the refusal\'s sentence, which the error handler takes from a field first, and the field sentence beside its field', async () => {
    api = await startPluginApi({ [`POST ${ROUTES_URL}/sites/7/publish`]: problem(400, 'Check the form.', { name: ['Name is taken.'] }) })
    const wrapper = mountBlocks([form])

    await wrapper.get('[data-testid="plugin-form"] form').trigger('submit')
    await settle()

    // `lib/errorHandler` orders `errors[field]` before `detail`, as every portal form reads a refusal.
    expect(wrapper.get('[data-testid="ui-form-error"]').text()).toBe('Name is taken.')
    expect(wrapper.get('[data-testid="plugin-form"]').text()).toContain('Name is taken.')
  })

  it('reads the named blocks again after a success', async () => {
    let reads = 0
    api = await startPluginApi({
      [`GET ${ROUTES_URL}/sites/7`]: () => ({ body: { name: `n${++reads}` } }),
      [`POST ${ROUTES_URL}/sites/7/publish`]: { status: 204 },
    })
    const wrapper = mountBlocks([
      { kind: PluginBlockKind.Stat, id: 'name', label: 'Name', source: { route: TEST_ROUTES.site, field: 'name' } },
      { ...form, submit: { ...form.submit, refresh: ['name'] } },
    ])
    await settle()

    await wrapper.get('[data-testid="plugin-form"] form').trigger('submit')
    await settle()

    expect(wrapper.get('[data-testid="plugin-stat"]').text()).toContain('n2')
  })

  it('empties every field after a success when the submit asks for it', async () => {
    api = await startPluginApi({ [`POST ${ROUTES_URL}/sites/7/publish`]: { status: 204 } })
    const wrapper = mountBlocks([{ ...form, submit: { ...form.submit, resetOnSuccess: true } }])

    await wrapper.get('input[data-testid="plugin-form-name"]').setValue('Blog')
    await wrapper.get('input[data-testid="plugin-form-pages"]').setValue('4')
    await wrapper.get('[data-testid="plugin-form"] form').trigger('submit')
    await settle()

    expect((wrapper.get('input[data-testid="plugin-form-name"]').element as HTMLInputElement).value).toBe('')
    expect((wrapper.get('input[data-testid="plugin-form-pages"]').element as HTMLInputElement).value).toBe('')
    await wrapper.get('[data-testid="plugin-form"] form').trigger('submit')
    await settle()
    expect(JSON.parse(api.requests.at(-1)!.body)).toEqual({ name: '', pages: '', token: '', public: false })
  })

  it('keeps the fields after a refusal, and after a success without the reset', async () => {
    let refuse = true
    api = await startPluginApi({ [`POST ${ROUTES_URL}/sites/7/publish`]: () => (refuse ? problem(409, 'Busy.') : { status: 204 }) })
    const reset = mountBlocks([{ ...form, submit: { ...form.submit, resetOnSuccess: true } }])
    await reset.get('input[data-testid="plugin-form-name"]').setValue('Blog')
    await reset.get('[data-testid="plugin-form"] form').trigger('submit')
    await settle()
    expect((reset.get('input[data-testid="plugin-form-name"]').element as HTMLInputElement).value).toBe('Blog')

    refuse = false
    const plain = mountBlocks([form])
    await plain.get('input[data-testid="plugin-form-name"]').setValue('Blog')
    await plain.get('[data-testid="plugin-form"] form').trigger('submit')
    await settle()
    expect((plain.get('input[data-testid="plugin-form-name"]').element as HTMLInputElement).value).toBe('Blog')
  })

  it('reads the follow-up blocks again at the host interval until the condition holds', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    let reads = 0
    api = await startPluginApi({
      [`GET ${ROUTES_URL}/sites/7`]: () => ({ body: { status: ++reads < 2 ? 'building' : 'live' } }),
      [`POST ${ROUTES_URL}/sites/7/publish`]: { status: 204 },
    })
    const wrapper = mountBlocks([
      { kind: PluginBlockKind.Badge, id: 'status', source: { route: TEST_ROUTES.site, field: 'status' } },
      { ...form, submit: { ...form.submit, followUp: { refresh: ['status'], until: { route: TEST_ROUTES.site, field: 'status', equals: 'live' } } } },
    ])
    await settle()

    await wrapper.get('[data-testid="plugin-form"] form').trigger('submit')
    await settle()
    vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    await settle()

    // The badge's first read saw `building`; the follow-up's read saw `live`, then the badge read it again.
    expect(wrapper.get('[data-testid="plugin-badge"]').text()).toBe('live')
    const settled = api.requests.length
    vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS * 3)
    await settle()
    expect(api.requests).toHaveLength(settled)
  })
})

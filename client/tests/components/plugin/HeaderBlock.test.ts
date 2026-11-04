/**
 * Tests for PluginHeaderBlock: the title as text, its actions beside it, and an action's own `visibleWhen`.
 *
 * @module tests/components/plugin/HeaderBlock.test
 */

import { afterEach, describe, expect, it } from 'vitest'
import type { StubServer } from '~/tests/helpers/stubServer'
import { mountBlocks, problem, ROUTES_URL, startPluginApi, TEST_ROUTES } from '~/tests/helpers/mountPlugin'
import { settle } from '~/tests/helpers/settle'
import { PluginBlockKind } from '~/types/pluginblockkind'

/** The stub API of the running test, closed after it. */
let api: StubServer | undefined

afterEach(async () => {
  await api?.close()
  api = undefined
})

describe('PluginHeaderBlock', () => {
  it('shows the title as text and draws each action', () => {
    const wrapper = mountBlocks([{
      kind: PluginBlockKind.Header,
      title: '<i>Site</i>',
      actions: [
        { kind: PluginBlockKind.Action, label: 'Publish', route: TEST_ROUTES.publish },
        { kind: PluginBlockKind.Action, label: 'Publish again', route: TEST_ROUTES.publish },
      ],
    }])

    const header = wrapper.get('[data-testid="plugin-header"]')
    expect(header.text()).toContain('<i>Site</i>')
    expect(header.find('i').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="plugin-action-button"]').map(b => b.text())).toEqual(['Publish', 'Publish again'])
  })

  it('hides an action whose own condition does not hold', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: { body: { status: 'building' } } })
    const wrapper = mountBlocks([{
      kind: PluginBlockKind.Header,
      title: 'Site',
      actions: [
        { kind: PluginBlockKind.Action, label: 'Publish', route: TEST_ROUTES.publish, visibleWhen: { route: TEST_ROUTES.site, field: 'status', equals: 'live' } },
        { kind: PluginBlockKind.Action, label: 'Refresh', route: TEST_ROUTES.publish },
      ],
    }])
    await settle()

    expect(wrapper.findAll('[data-testid="plugin-action-button"]').map(b => b.text())).toEqual(['Refresh'])
  })
})

describe('PluginHeaderBlock action messages', () => {
  it('shows an action\'s sentence once below the row, not under its button', async () => {
    api = await startPluginApi({ [`POST ${ROUTES_URL}/sites/7/publish`]: { body: { message: 'Published.' } } })
    const wrapper = mountBlocks([{
      kind: PluginBlockKind.Header,
      title: 'Site',
      actions: [
        { kind: PluginBlockKind.Action, label: 'Publish', route: TEST_ROUTES.publish },
        { kind: PluginBlockKind.Action, label: 'Other', route: TEST_ROUTES.publish },
      ],
    }])

    await wrapper.findAll('[data-testid="plugin-action-button"]')[0]!.trigger('click')
    await settle()

    const line = wrapper.get('[data-testid="plugin-header-message"]')
    expect(line.text()).toBe('Published.')
    expect(wrapper.get('[data-testid="plugin-header"]').find('[data-testid="plugin-header-message"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-action-message"]').exists()).toBe(false)
  })

  it('shows a refusal\'s sentence below the row in the danger tone', async () => {
    api = await startPluginApi({ [`POST ${ROUTES_URL}/sites/7/publish`]: problem(409, 'The site is building.') })
    const wrapper = mountBlocks([{
      kind: PluginBlockKind.Header,
      title: 'Site',
      actions: [{ kind: PluginBlockKind.Action, label: 'Publish', route: TEST_ROUTES.publish }],
    }])

    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()

    const line = wrapper.get('[data-testid="plugin-header-message"]')
    expect(line.text()).toBe('The site is building.')
    expect(line.classes().join(' ')).toMatch(/danger|red/)
  })
})

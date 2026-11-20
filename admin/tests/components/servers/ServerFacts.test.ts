/**
 * Mount tests for ServerFacts: the facts grid of a server card draws one fact per datum the server has, in order, leaves out
 * a fact without data, and draws placeholder facts while the server is still loading.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ServerFacts from '@/components/servers/ServerFacts.vue'
import { SERVER_FACT_SLOTS, ServerFactKey } from '@/lib/servers'
import { createTestI18n } from '../../helpers/i18n'
import { serverDto } from '../../helpers/serverFixtures'

/**
 * Mounts the grid.
 *
 * @param server - The server whose facts are shown; null while loading.
 * @returns The wrapper.
 */
const mountFacts = (server: ReturnType<typeof serverDto> | null) =>
  mount(ServerFacts, { props: { server }, global: { plugins: [createTestI18n()] } })

describe('ServerFacts', () => {
  it('draws the connection fact of a server and no fact the server has no data for', () => {
    const wrapper = mountFacts(serverDto({ id: 7 }))

    expect(wrapper.find('[data-testid="server-facts-7"]').exists()).toBe(true)
    expect(wrapper.find(`[data-testid="server-fact-${ServerFactKey.Connection}"]`).exists()).toBe(true)
  })

  it('draws one placeholder per fact slot while the server loads', () => {
    const wrapper = mountFacts(null)

    expect(wrapper.get('[data-testid="server-facts-loading"]').attributes('aria-hidden')).toBe('true')
    expect(wrapper.findAll('[data-testid="server-facts-loading"] > *')).toHaveLength(SERVER_FACT_SLOTS)
  })
})

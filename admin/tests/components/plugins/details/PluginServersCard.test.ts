/**
 * Mount tests for PluginServersCard: the modules the plugin serves, one row per server with its name, status, time of the last
 * connection test and a link to the Servers page, a server never tested, and the empty note with its link.
 */
import { describe, expect, it } from 'vitest'
import PluginServersCard from '@/components/plugins/details/PluginServersCard.vue'
import { formatDateTime } from '@/lib/format'
import { mountScreen } from '../../../helpers/mount'
import { serverOptionDto } from '../../../helpers/serverFixtures'

/** The module the fixture plugin declares. */
const MODULES = ['examplepanel', 'examplepanel-eu']

/**
 * Mounts the card.
 *
 * @param props - Props to change.
 * @returns The wrapper.
 */
const mountCard = async (props: Record<string, unknown> = {}) => {
  const { wrapper } = await mountScreen(PluginServersCard, { modules: MODULES, servers: [], ...props })
  return wrapper
}

describe('PluginServersCard', () => {
  it('names the modules the plugin serves, whatever they are', async () => {
    const wrapper = await mountCard()

    expect(wrapper.get('[data-testid="plugin-servers-modules"]').text()).toContain('examplepanel, examplepanel-eu')
  })

  it('lists a server with its name, status, the time of the last test and a link to the Servers page', async () => {
    const server = serverOptionDto({ id: 7, name: 'Frankfurt 1', hostname: 'fra1.example.test', isOnline: true, lastTestedAt: '2026-01-02T10:00:00Z' })
    const wrapper = await mountCard({ servers: [server] })

    const row = wrapper.get('[data-testid="plugin-server-7"]')
    expect(row.text()).toContain('Frankfurt 1')
    expect(row.text()).toContain('fra1.example.test')
    expect(wrapper.get('[data-testid="plugin-server-state-7"]').text()).toBe('Online')
    expect(wrapper.get('[data-testid="plugin-server-tested-7"]').text()).toContain('Last tested')
    expect(wrapper.get('[role="tooltip"]').text()).toBe(formatDateTime('2026-01-02T10:00:00Z'))
    expect(wrapper.get('[data-testid="plugin-server-link-7"]').attributes('href')).toBe('/servers?search=Frankfurt+1')
    expect(wrapper.find('[data-testid="plugin-servers-empty"]').exists()).toBe(false)
  })

  it('shows an offline server as offline', async () => {
    const wrapper = await mountCard({ servers: [serverOptionDto({ id: 2, isOnline: false, lastTestedAt: '2026-01-02T10:00:00Z' })] })

    expect(wrapper.get('[data-testid="plugin-server-state-2"]').text()).toBe('Offline')
  })

  it('says a server was never tested instead of showing an empty time', async () => {
    const wrapper = await mountCard({ servers: [serverOptionDto({ id: 3, isOnline: null, lastTestedAt: null })] })

    expect(wrapper.get('[data-testid="plugin-server-state-3"]').text()).toBe('Not tested')
    expect(wrapper.get('[data-testid="plugin-server-tested-3"]').text()).toBe('Never tested')
  })

  it('lists every server in the order it is given', async () => {
    const wrapper = await mountCard({ servers: [serverOptionDto({ id: 5, name: 'alpha' }), serverOptionDto({ id: 4, name: 'beta' })] })

    expect(wrapper.findAll('[data-testid^="plugin-server-link-"]').map(link => link.text())).toEqual(['alpha', 'beta'])
  })

  it('shows the empty note with a link to the Servers page when no server uses the plugin', async () => {
    const wrapper = await mountCard()

    expect(wrapper.get('[data-testid="plugin-servers-empty"]').text()).toContain('No server uses this plugin yet.')
    expect(wrapper.get('[data-testid="plugin-servers-open"]').attributes('href')).toBe('/servers')
    expect(wrapper.find('[data-testid="plugin-servers-list"]').exists()).toBe(false)
  })
})

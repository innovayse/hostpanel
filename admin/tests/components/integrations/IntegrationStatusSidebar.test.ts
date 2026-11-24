/**
 * Mount tests for IntegrationStatusSidebar: the three states (testing, result, idle) and the
 * optional rows and hint.
 */
import { describe, expect, it } from 'vitest'
import IntegrationStatusSidebar from '@/components/integrations/IntegrationStatusSidebar.vue'
import { mountWithApp } from '../../helpers/mount'
import { IntegrationConnectionState } from '@/types/integration'
import { serverOptionDto } from '../../helpers/serverFixtures'

/**
 * Mounts the sidebar.
 *
 * @param props - Props to change.
 * @returns The wrapper.
 */
const mountSidebar = (props: Record<string, unknown> = {}) =>
  mountWithApp(IntegrationStatusSidebar, { props: { testResult: null, ...props } })

describe('IntegrationStatusSidebar', () => {
  it('shows "not tested yet" when nothing was tested on the page', () => {
    const wrapper = mountSidebar()

    expect(wrapper.get('[data-testid="integration-status-idle"]').text()).toBe('Not tested yet')
  })

  it('shows the in-flight state first', () => {
    const wrapper = mountSidebar({ testing: true, testResult: { success: true, message: 'ok' } })

    expect(wrapper.find('[data-testid="integration-status-testing"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="integration-status-result"]').exists()).toBe(false)
  })

  it('shows the verdict, the message and the detail rows of a result', () => {
    const wrapper = mountSidebar({
      testResult: { success: false, message: 'Bad key.', testedAt: '2026-10-14T10:00:00Z', durationMs: 2400 },
      endpointHost: 'pg.example.com',
    })

    const result = wrapper.get('[data-testid="integration-status-result"]')
    expect(result.text()).toContain('Connection Failed')
    expect(result.text()).toContain('Bad key.')
    expect(result.text()).toContain('pg.example.com')
    expect(result.text()).toContain('2.4 s')
  })

  it('shows the hint callout only when there is one', () => {
    expect(mountSidebar().find('[data-testid="integration-status-hint"]').exists()).toBe(false)
    expect(mountSidebar({ hint: 'Add the webhook.' }).get('[data-testid="integration-status-hint"]').text()).toContain('Add the webhook.')
  })

  it('shows the state a plugin last reported while nothing was tested on the page', () => {
    const wrapper = mountSidebar({
      connection: { state: IntegrationConnectionState.Unavailable, message: 'No answer in time.', checkedAt: '2026-11-04T10:00:00Z' },
    })

    const connection = wrapper.get('[data-testid="integration-status-connection"]')
    expect(connection.text()).toContain('Unavailable')
    expect(connection.text()).toContain('No answer in time.')
    expect(wrapper.find('[data-testid="integration-status-idle"]').exists()).toBe(false)
  })

  it('prefers the result of a test run on the page to the reported state', () => {
    const wrapper = mountSidebar({
      testResult: { success: true, message: 'ok' },
      connection: { state: IntegrationConnectionState.Unavailable, message: 'old', checkedAt: '2026-11-04T10:00:00Z' },
    })

    expect(wrapper.find('[data-testid="integration-status-result"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="integration-status-connection"]').exists()).toBe(false)
  })

  it('reflects the servers of a panel plugin instead of "not tested yet"', () => {
    const wrapper = mountSidebar({
      servers: [serverOptionDto({ id: 1, isOnline: true }), serverOptionDto({ id: 2, isOnline: true }), serverOptionDto({ id: 3, isOnline: false })],
    })

    const servers = wrapper.get('[data-testid="integration-status-servers"]')
    expect(servers.get('[data-testid="integration-status-servers-state"]').text()).toBe('Offline')
    expect(servers.text()).toContain('2 of 3 servers online')
    expect(servers.text()).toContain('1 offline')
    expect(wrapper.find('[data-testid="integration-status-idle"]').exists()).toBe(false)
  })

  it('names the servers never tested', () => {
    const wrapper = mountSidebar({ servers: [serverOptionDto({ id: 1, isOnline: true }), serverOptionDto({ id: 2, isOnline: null })] })

    expect(wrapper.get('[data-testid="integration-status-servers-state"]').text()).toBe('Online')
    expect(wrapper.get('[data-testid="integration-status-servers"]').text()).toContain('1 not tested')
  })

  it('keeps "not tested yet" for a plugin with no servers, and prefers the plugin reported state to its servers', () => {
    expect(mountSidebar({ servers: [] }).find('[data-testid="integration-status-idle"]').exists()).toBe(true)

    const wrapper = mountSidebar({
      servers: [serverOptionDto({ isOnline: false })],
      connection: { state: IntegrationConnectionState.Connected, message: 'Fine.', checkedAt: '2026-11-04T10:00:00Z' },
    })
    expect(wrapper.find('[data-testid="integration-status-connection"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="integration-status-servers"]').exists()).toBe(false)
  })

  it('draws bars of the badge and the message line while a test is in flight', () => {
    const wrapper = mountSidebar({ testing: true })

    expect(wrapper.get('[data-testid="integration-status-testing"]').findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(2)
  })
})

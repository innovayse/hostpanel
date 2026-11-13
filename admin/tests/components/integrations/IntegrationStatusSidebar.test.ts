/**
 * Mount tests for IntegrationStatusSidebar: the three states (testing, result, idle) and the
 * optional rows and hint.
 */
import { describe, expect, it } from 'vitest'
import IntegrationStatusSidebar from '@/components/integrations/IntegrationStatusSidebar.vue'
import { mountWithApp } from '../../helpers/mount'
import { IntegrationConnectionState } from '@/types/integration'

/**
 * Mounts the sidebar.
 *
 * @param props - Props to change.
 * @returns The wrapper.
 */
const mountSidebar = (props: Record<string, unknown> = {}) =>
  mountWithApp(IntegrationStatusSidebar, { props: { testResult: null, ...props } })

describe('IntegrationStatusSidebar', () => {
  it('draws a placeholder instead of the connection state while loading', () => {
    const wrapper = mountSidebar({ loading: true })

    expect(wrapper.find('[data-testid="integration-status-loading"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="integration-status-idle"]').exists()).toBe(false)
  })

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
})

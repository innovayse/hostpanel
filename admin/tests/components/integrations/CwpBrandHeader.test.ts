/**
 * Mount tests for CwpBrandHeader: the title, the version badge and the connection state.
 */
import { describe, expect, it } from 'vitest'
import CwpBrandHeader from '@/components/integrations/CwpBrandHeader.vue'
import { mountWithApp } from '../../helpers/mount'

describe('CwpBrandHeader', () => {
  it('shows the title, the version and a connected state', () => {
    const wrapper = mountWithApp(CwpBrandHeader, { props: { version: '0.9.8', status: 'connected' } })

    expect(wrapper.text()).toContain('CentOS Web Panel')
    expect(wrapper.get('[data-testid="cwp-version"]').text()).toBe('v0.9.8')
    expect(wrapper.get('[data-testid="cwp-status"]').text()).toBe('Connected')
  })

  it('omits the version badge while it is unknown', () => {
    const wrapper = mountWithApp(CwpBrandHeader, { props: { version: null, status: 'unknown' } })

    expect(wrapper.find('[data-testid="cwp-version"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="cwp-status"]').text()).toBe('Unknown')
  })

  it('names a failed connection', () => {
    const wrapper = mountWithApp(CwpBrandHeader, { props: { version: null, status: 'error' } })

    expect(wrapper.get('[data-testid="cwp-status"]').text()).toBe('Connection Error')
  })
})

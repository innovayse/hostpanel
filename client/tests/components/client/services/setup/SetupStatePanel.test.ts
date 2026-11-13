/**
 * Unit tests for SetupStatePanel: the texts, the icon tile and the action slot.
 */
import { describe, expect, it } from 'vitest'
import { AlertCircle } from 'lucide-vue-next'
import { mountClient } from '~/tests/helpers/mountClient'
import SetupStatePanel from '~/components/client/services/setup/SetupStatePanel.vue'

describe('SetupStatePanel', () => {
  it('shows the headline and the sentence', () => {
    const wrapper = mountClient(SetupStatePanel, { props: { title: 'Not available', description: 'Try later.', icon: AlertCircle } })

    expect(wrapper.find('h2').text()).toBe('Not available')
    expect(wrapper.text()).toContain('Try later.')
  })

  it('shows the icon tile only when an icon is given', () => {
    const bare = mountClient(SetupStatePanel, { props: { title: 't', description: 'd' } })
    const withIcon = mountClient(SetupStatePanel, { props: { title: 't', description: 'd', icon: AlertCircle } })

    expect(bare.find('[data-testid="ui-tile"]').exists()).toBe(false)
    expect(withIcon.find('[data-testid="ui-tile"]').exists()).toBe(true)
  })

  it('renders the action slot', () => {
    const wrapper = mountClient(SetupStatePanel, { props: { title: 't', description: 'd' }, slots: { default: '<span data-testid="action">Go</span>' } })

    expect(wrapper.find('[data-testid="action"]').text()).toBe('Go')
  })
})

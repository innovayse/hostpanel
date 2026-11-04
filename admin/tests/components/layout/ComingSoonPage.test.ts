/**
 * Tests for ComingSoonPage and the three screens built on it: the header and the "coming soon" block
 * render with the supplied wording, and each screen shows its own title.
 */
import { describe, expect, it } from 'vitest'
import ComingSoonPage from '@/components/layout/ComingSoonPage.vue'
import DisputesView from '@/views/DisputesView.vue'
import ManageAffiliatesView from '@/views/ManageAffiliatesView.vue'
import ServiceAddonsView from '@/views/ServiceAddonsView.vue'
import { Icon } from '@/lib/icons'
import { mountWithApp } from '../../helpers/mount'

describe('ComingSoonPage', () => {
  it('renders the title, subtitle, heading and description it is given', () => {
    const wrapper = mountWithApp(ComingSoonPage, {
      props: { title: 'Title', subtitle: 'Sub', heading: 'Soon', description: 'Later', icon: Icon.Globe },
    })

    expect(wrapper.get('[data-testid="coming-soon-header"]').text()).toContain('Title')
    expect(wrapper.get('[data-testid="coming-soon-header"]').text()).toContain('Sub')
    expect(wrapper.get('[data-testid="coming-soon-empty"]').text()).toContain('Soon')
    expect(wrapper.get('[data-testid="coming-soon-empty"]').text()).toContain('Later')
  })
})

describe('screens built on ComingSoonPage', () => {
  it.each([
    [ManageAffiliatesView, 'Manage Affiliates'],
    [ServiceAddonsView, 'Service Addons'],
    [DisputesView, 'List Disputes'],
  ])('shows its own title and the coming soon block', (view, title) => {
    const wrapper = mountWithApp(view)

    expect(wrapper.get('[data-testid="coming-soon-header"]').text()).toContain(title)
    expect(wrapper.get('[data-testid="coming-soon-empty"]').text()).toContain('Coming soon')
  })
})

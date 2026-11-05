/**
 * Unit tests for PageHelpLink: hidden when url is null, shown with the right href and rel when set.
 *
 * @module tests/components/layout/PageHelpLink.test
 */
import { describe, expect, it } from 'vitest'
import { mountWithApp } from '../../helpers/mount'
import PageHelpLink from '@/components/layout/PageHelpLink.vue'

describe('PageHelpLink', () => {
  it('renders nothing when url is null', () => {
    const wrapper = mountWithApp(PageHelpLink, { props: { url: null } })

    expect(wrapper.find('[data-testid="page-help-link"]').exists()).toBe(false)
  })

  it('renders a link with the correct href and rel when url is set', () => {
    const wrapper = mountWithApp(PageHelpLink, { props: { url: 'https://docs.example.com/servers/servers' } })

    const link = wrapper.find('[data-testid="page-help-link"]')
    expect(link.exists()).toBe(true)
    expect(link.attributes('href')).toBe('https://docs.example.com/servers/servers')
    expect(link.attributes('rel')).toContain('noopener')
  })
})

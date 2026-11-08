/**
 * Unit tests for PluginDetailsLinks: only the links the registry gives, only web addresses, the
 * licence name, and nothing at all when there is nothing to show.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PluginDetailsLinks from '@/components/plugins/details/PluginDetailsLinks.vue'
import { createTestI18n } from '../../../helpers/i18n'
import { makeDetails } from '../../../helpers/pluginDetails'

/**
 * Mounts the links card.
 *
 * @param details - Fields of the details to change.
 * @returns The wrapper.
 */
const mountLinks = (details: Parameters<typeof makeDetails>[0] = {}) =>
  mount(PluginDetailsLinks, {
    props: { details: makeDetails(details) },
    global: { plugins: [createTestI18n()] },
  })

describe('PluginDetailsLinks', () => {
  it('shows the links the registry gives, opened in a new tab, and the licence name', () => {
    const wrapper = mountLinks({ buyUrl: 'https://shop.example.org/pay' })

    const docs = wrapper.get('[data-testid="plugin-details-link-documentation"]')
    expect(docs.attributes('href')).toBe('https://docs.example.org/pay')
    expect(docs.attributes('target')).toBe('_blank')
    expect(docs.attributes('rel')).toContain('noopener')
    expect(wrapper.find('[data-testid="plugin-details-link-support"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-details-link-buy"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-details-link-repository"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-details-licence-name"]').text()).toContain('MIT')
  })

  it('has the card padding', () => {
    expect(mountLinks().get('[data-testid="plugin-details-links"]').classes()).toContain('p-5')
  })

  it('drops an address that is not http or https', () => {
    const wrapper = mountLinks({ documentationUrl: 'javascript:alert(1)', supportUrl: 'not a url' })

    expect(wrapper.find('[data-testid="plugin-details-link-documentation"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-link-support"]').exists()).toBe(false)
  })

  it('renders nothing when there is no link and no licence name', () => {
    const wrapper = mountLinks({ documentationUrl: null, supportUrl: null, repositoryUrl: null, buyUrl: null, licence: null })

    expect(wrapper.find('[data-testid="plugin-details-links"]').exists()).toBe(false)
  })
})

/**
 * Unit tests for UiBackLink: the target, the label and the arrow.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiBackLink from '~/components/ui/UiBackLink.vue'
import { CLIENT_DOMAINS_PATH, CLIENT_LOGIN_PATH } from '~/constants/clientRoutes'

describe('UiBackLink', () => {
  it('links to the list with its label and an arrow', () => {
    const wrapper = mountWithKit(UiBackLink, { props: { to: CLIENT_DOMAINS_PATH, label: 'Back to Domains', testid: 'back' } })

    expect(wrapper.attributes('href')).toBe(CLIENT_DOMAINS_PATH)
    expect(wrapper.attributes('data-testid')).toBe('back')
    expect(wrapper.text()).toBe('Back to Domains')
    expect(wrapper.find('svg').exists()).toBe(true)
  })

  it('is not underlined', () => {
    const wrapper = mountWithKit(UiBackLink, { props: { to: '/x', label: 'Back' } })

    expect(wrapper.classes()).not.toContain('underline')
  })

  it('renders the slot in place of the label', () => {
    const wrapper = mountWithKit(UiBackLink, { props: { to: CLIENT_LOGIN_PATH, label: 'ignored' }, slots: { default: 'Back to sign in' } })

    expect(wrapper.text()).toBe('Back to sign in')
  })

  it('tightens the gap when compact', () => {
    const wrapper = mountWithKit(UiBackLink, { props: { to: '/x', label: 'Back', compact: true } })

    expect(wrapper.classes()).toContain('gap-1.5')
  })
})

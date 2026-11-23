/**
 * Mount tests for AccountAvatar: the initials it draws and its two sizes.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import AccountAvatar from '@/components/layout/AccountAvatar.vue'

describe('AccountAvatar', () => {
  it('draws the initials', () => {
    const wrapper = mount(AccountAvatar, { props: { initials: 'JA' } })

    expect(wrapper.get('[data-testid="account-avatar"]').text()).toBe('JA')
  })

  it('is small in the open card and larger in the folded rail', () => {
    const open = mount(AccountAvatar, { props: { initials: 'JA' } })
    const rail = mount(AccountAvatar, { props: { initials: 'JA', rail: true } })

    expect(open.classes()).toContain('size-7')
    expect(rail.classes()).toContain('size-8')
  })
})

/**
 * Mount tests for ClientGroupBadge: the group's name beside a dot in the group's colour.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ClientGroupBadge from '@/components/clients/ClientGroupBadge.vue'

describe('ClientGroupBadge', () => {
  it('shows the name and the colour dot', () => {
    const wrapper = mount(ClientGroupBadge, { props: { name: 'Resellers', color: '#EF4444' } })

    expect(wrapper.text()).toContain('Resellers')
    expect(wrapper.get('circle').attributes('fill')).toBe('#EF4444')
  })
})

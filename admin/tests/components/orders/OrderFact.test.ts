/**
 * Unit tests for OrderFact: the caption and the slotted value both render.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import OrderFact from '@/components/orders/OrderFact.vue'

describe('OrderFact', () => {
  it('renders the caption and the slotted value', () => {
    const wrapper = mount(OrderFact, {
      props: { label: 'Payment Method' },
      slots: { default: '<span data-testid="value">Example Gateway</span>' },
    })

    expect(wrapper.text()).toContain('Payment Method')
    expect(wrapper.get('[data-testid="value"]').text()).toBe('Example Gateway')
  })

  it('keeps the caption and draws a bar instead of the value while loading', () => {
    const wrapper = mount(OrderFact, {
      props: { label: 'Payment Method', loading: true },
      slots: { default: '<span data-testid="value">Example Gateway</span>' },
    })

    expect(wrapper.text()).toContain('Payment Method')
    expect(wrapper.find('[data-testid="value"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="order-fact-loading"]').exists()).toBe(true)
  })
})

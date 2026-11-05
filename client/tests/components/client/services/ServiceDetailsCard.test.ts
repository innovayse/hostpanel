/**
 * Unit tests for ServiceDetailsCard: the description list and its fallbacks.
 */
import { describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import ServiceDetailsCard from '~/components/client/services/ServiceDetailsCard.vue'
import { makeClientService as service } from '~/tests/helpers/clientServiceFixture'
import { BillingCycle } from '~/types/billingcycle'

describe('ServiceDetailsCard', () => {
  it('lists the plan, the domain and the billing cycle', () => {
    const wrapper = mountClient(ServiceDetailsCard, { props: { service: service(), currency: { code: 'USD' } } })

    expect(wrapper.text()).toContain('Starter')
    expect(wrapper.text()).toContain('example.com')
    expect(wrapper.text()).toContain('products.pricing.cycles.monthly')
  })

  it('names the yearly cycle with its locale key', () => {
    const wrapper = mountClient(ServiceDetailsCard, { props: { service: service({ billingCycle: BillingCycle.Annual }), currency: {} } })

    expect(wrapper.text()).toContain('products.pricing.cycles.annually')
  })

  it('formats both amounts in the account currency', () => {
    const wrapper = mountClient(ServiceDetailsCard, { props: { service: service(), currency: { code: 'USD' } } })

    expect(wrapper.text()).toContain('$10.00')
    expect(wrapper.text()).toContain('$12.00')
  })

  it('shows the payment method the backend sent', () => {
    const wrapper = mountClient(ServiceDetailsCard, { props: { service: service(), currency: {} } })

    expect(wrapper.text()).toContain('card_gateway')
  })

  it('shows a dash for a missing domain', () => {
    const wrapper = mountClient(ServiceDetailsCard, { props: { service: service({ domain: null }), currency: {} } })

    expect(wrapper.text()).toContain('—')
  })
})

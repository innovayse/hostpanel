/**
 * Tests for the service filters every service list shares: the address is read back as the typed status, product and
 * billing cycle the API takes, a value that is not one is ignored, and each applied filter is described for its chip.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h, ref } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { useListQuery } from '@/composables/useListQuery'
import { ServiceFilterKey, useServiceFilters } from '@/composables/useServiceFilters'
import type { ServiceFilters } from '@/composables/useServiceFilters'
import type { Product } from '@/types/product'
import { BillingCycle, ServiceStatus } from '@/types/service'
import { createTestI18n } from '../helpers/i18n'

/** The products the product chip is named from. */
const products = ref([
  { id: 1, name: 'Shared Starter' },
  { id: 2, name: 'VPS Small' },
] as Product[])

/**
 * Mounts a screen that uses the composables, at the given address.
 *
 * @param url - The address the router starts at.
 * @returns A reader of the service filters the screen holds.
 */
const setup = async (url: string) => {
  let state: ServiceFilters | null = null
  const Screen = defineComponent({
    setup() {
      const list = useListQuery(Object.values(ServiceFilterKey))
      state = useServiceFilters(list, products)
      return () => h('div')
    },
  })
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/services', component: Screen }] })
  await router.push(url)
  await router.isReady()
  mount({ render: () => h('div', [h(Screen)]) }, { global: { plugins: [router, createTestI18n()] } })
  await flushPromises()
  return () => state as unknown as ServiceFilters
}

describe('useServiceFilters', () => {
  it('reads the status, the product and the billing cycle from the address as the API takes them', async () => {
    const filters = await setup('/services?status=Suspended&productId=2&billingCycle=Annual')

    expect(filters().filters.value).toEqual({
      status: ServiceStatus.Suspended,
      productId: 2,
      billingCycle: BillingCycle.Annual,
    })
  })

  it('leaves out a filter that is not set or holds a value that is not a member', async () => {
    const filters = await setup('/services?status=Gone&productId=abc&billingCycle=Weekly')

    expect(filters().filters.value).toEqual({ status: undefined, productId: undefined, billingCycle: undefined })
  })

  it('describes each applied filter for its chip, naming the product', async () => {
    const filters = await setup('/services?status=Active&productId=2&billingCycle=Quarterly')

    expect(filters().chipSources()).toEqual([
      { key: 'status', label: 'Status', value: 'Active' },
      { key: 'productId', label: 'Product/Service', value: 'VPS Small' },
      { key: 'billingCycle', label: 'Billing Cycle', value: 'Quarterly' },
    ])
  })

  it('shows the product id on its chip while the products have not loaded or the product is gone', async () => {
    const filters = await setup('/services?productId=9')

    expect(filters().chipSources().find((source) => source.key === 'productId')?.value).toBe('9')
  })

  it('describes a filter that is not set without a value', async () => {
    const filters = await setup('/services')

    expect(filters().chipSources().every((source) => source.value === undefined)).toBe(true)
  })
})

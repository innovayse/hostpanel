/**
 * Unit tests for BundleDomainPicker: the fixed years, the search and the choice of an available
 * name, the move from a taken name to a transfer, the transfer form and the chosen-domain state.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import BundleDomainPicker from '~/components/bundle/BundleDomainPicker.vue'
import type { BundleDomainChoice } from '~/types/bundledomainchoice'
import { DomainOrderAction } from '~/types/domainorderaction'
import { DomainResultStatus } from '~/types/domainresultstatus'

/** One available and one taken name. */
const results = [
  { name: 'free.com', price: '$10.00', status: DomainResultStatus.Available },
  { name: 'taken.com', price: '—', status: DomainResultStatus.Taken },
]

/**
 * Mounts the picker.
 *
 * @param props Props that differ from the defaults.
 * @returns The wrapper.
 */
const mountPicker = (props: Record<string, unknown> = {}) =>
  mountWithKit(BundleDomainPicker, {
    props: { years: 2, results, pending: false, hasZones: true, selected: null, error: '', ...props },
  })

describe('BundleDomainPicker', () => {
  beforeEach(() => {
    stubPublicPageRuntime()
  })

  it('shows the years the bundle fixes', () => {
    expect(mountPicker().find('[data-testid="bundle-domain-years"]').text()).toBe('domains.years')
  })

  it('reports the typed term when the search form is submitted', async () => {
    const wrapper = mountPicker()

    await wrapper.find('[data-testid="bundle-domain-input"]').setValue('mysite')
    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('search')?.[0]).toEqual(['mysite'])
  })

  it('offers a name to choose when it is available and a transfer when it is taken', () => {
    const wrapper = mountPicker()

    expect(wrapper.find('[data-testid="bundle-domain-pick-free.com"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="bundle-domain-transfer-free.com"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="bundle-domain-transfer-taken.com"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="bundle-domain-pick-taken.com"]').exists()).toBe(false)
  })

  it('reports an available name as a registration', async () => {
    const wrapper = mountPicker()

    await wrapper.find('[data-testid="bundle-domain-pick-free.com"]').trigger('click')

    expect(wrapper.emitted('select')?.[0]).toEqual([{ action: DomainOrderAction.Register, name: 'free.com', eppCode: '' } satisfies BundleDomainChoice])
  })

  it('moves a taken name to the transfer form and reports it with its code', async () => {
    const wrapper = mountPicker()

    await wrapper.find('[data-testid="bundle-domain-transfer-taken.com"]').trigger('click')
    await wrapper.find('[data-testid="bundle-domain-transfer-epp"]').setValue(' abc ')
    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('select')?.[0]).toEqual([{ action: DomainOrderAction.Transfer, name: 'taken.com', eppCode: 'abc' }])
  })

  it('says so when the operator has priced no extension', () => {
    const wrapper = mountPicker({ hasZones: false })

    expect(wrapper.find('[data-testid="bundle-domain-no-zones"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="bundle-domain-search"]').attributes('disabled')).toBeDefined()
  })

  it('shows the refusal of the last choice', () => {
    expect(mountPicker({ error: 'Not offered.' }).find('[data-testid="bundle-domain-error"]').text()).toBe('Not offered.')
  })

  it('shows the chosen domain with a way to change it instead of the search', async () => {
    const wrapper = mountPicker({ selected: { action: DomainOrderAction.Register, name: 'free.com', eppCode: '' } })

    expect(wrapper.find('[data-testid="bundle-domain-selected"]').text()).toContain('free.com')
    expect(wrapper.find('[data-testid="bundle-domain-search"]').exists()).toBe(false)

    await wrapper.find('[data-testid="bundle-domain-change"]').trigger('click')

    expect(wrapper.emitted('clear')).toHaveLength(1)
  })
})

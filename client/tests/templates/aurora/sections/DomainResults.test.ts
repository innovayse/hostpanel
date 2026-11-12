/**
 * Unit tests for DomainResults: the three message states, the status of each row, the add button
 * and its disabled states, the transfer link for a taken name, and the add event.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import DomainResults from '~/templates/aurora/sections/DomainResults.vue'
import { DomainResultStatus } from '~/types/domainresultstatus'
import type { DomainResult } from '~/types/domainresult'
import { stubAuroraGlobals } from '../auroraGlobals'

/** One result per availability state. */
const RESULTS: DomainResult[] = [
  { name: 'free.com', price: '$10', status: DomainResultStatus.Available },
  { name: 'owned.com', price: '—', status: DomainResultStatus.Taken },
  { name: 'odd.com', price: '—', status: DomainResultStatus.Unknown },
]

describe('DomainResults', () => {
  it('explains why there is nothing to search when no extension is priced', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(DomainResults, { props: { hasZones: false } })

    expect(wrapper.find('[data-testid="domain-results-no-zones"]').text()).toBe('aurora.domainSearch.noZones')
  })

  it('shows the searching line while a lookup runs and the empty line after an empty one', () => {
    stubAuroraGlobals()

    expect(mountWithKit(DomainResults, { props: { pending: true } }).find('[data-testid="domain-results-pending"]').exists()).toBe(true)
    expect(mountWithKit(DomainResults, { props: { results: [] } }).find('[data-testid="domain-results-empty"]').exists()).toBe(true)
  })

  it('marks each row with its status for the e2e specs', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(DomainResults, { props: { results: RESULTS } })

    expect(wrapper.find('[data-testid="domain-result-free.com"]').attributes('data-status')).toBe('available')
    expect(wrapper.find('[data-testid="domain-result-owned.com"]').attributes('data-status')).toBe('taken')
    expect(wrapper.find('[data-testid="domain-result-odd.com"]').attributes('data-status')).toBe('unknown')
  })

  it('offers add only for an available name and transfer only for a taken one', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(DomainResults, { props: { results: RESULTS } })

    expect(wrapper.find('[data-testid="domain-add-free.com"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="domain-add-owned.com"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="domain-transfer-owned.com"]').attributes('href')).toBe('/domains/transfer?domain=owned.com')
    expect(wrapper.find('[data-testid="domain-transfer-odd.com"]').exists()).toBe(false)
  })

  it('emits add with the name when the button is pressed', async () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(DomainResults, { props: { results: RESULTS } })

    await wrapper.find('[data-testid="domain-add-free.com"]').trigger('click')

    expect(wrapper.emitted('add')).toEqual([['free.com']])
  })

  it('disables add for a name already in the cart and switches its label', () => {
    stubAuroraGlobals()
    const button = mountWithKit(DomainResults, { props: { results: RESULTS, inCart: ['free.com'] } })
      .find('[data-testid="domain-add-free.com"]')

    expect(button.attributes('disabled')).toBeDefined()
    expect(button.text()).toBe('aurora.domainSearch.inCart')
  })
})

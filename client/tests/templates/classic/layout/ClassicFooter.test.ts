/**
 * Unit tests for ClassicFooter: the classic template's footer slot renders the shared site
 * footer and nothing else.
 *
 * The real site footer runs with the real catalogue and currency stores, the pickers, the domain search and the
 * operator's settings composables against a stub server playing the BFF.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { AMD_CURRENCY, USD_CURRENCY, awaitCatalogue } from '~/tests/helpers/payerCatalogue'
import { readOf, requestsTo } from '~/tests/helpers/stubRoutes'
import { useTemplateGlobals } from '~/tests/helpers/templateGlobals'
import { waitUntil } from '~/tests/helpers/waitUntil'
import LayoutFooter from '~/components/layout/Footer.vue'
import SectionsDomainSearch from '~/components/sections/SectionsDomainSearch.vue'
import ClassicFooter from '~/templates/classic/layout/ClassicFooter.vue'

/** The stub server and the loader of the operator's settings; the hooks start the stub before each test and close it after. */
const { replies, stub, loadGlobals } = useTemplateGlobals()

describe('ClassicFooter', () => {
  beforeEach(async () => {
    replies[readOf(CatalogPath.ProductGroups)] = { body: [] }
    replies[readOf(CatalogPath.BillingCurrencies)] = { body: [USD_CURRENCY, AMD_CURRENCY] }
    await loadGlobals()
  })

  it('renders the shared site footer', async () => {
    const wrapper = mountWithKit(ClassicFooter, { components: { LayoutFooter, SectionsDomainSearch } })
    await waitUntil(() => expect(requestsTo(stub().requests, CatalogPath.ProductGroups).length).toBeGreaterThan(0))
    await awaitCatalogue()

    expect(wrapper.find('[data-testid="layout-footer"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="sections-domain-search"]').exists()).toBe(true)
  })
})

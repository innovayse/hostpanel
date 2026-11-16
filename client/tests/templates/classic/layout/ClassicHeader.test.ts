/**
 * Unit tests for ClassicHeader: the classic template's header slot renders the shared site
 * header and nothing else.
 *
 * The real site header runs with the real catalogue, currency and auth stores and the operator's settings composables
 * against a stub server playing the BFF.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { AMD_CURRENCY, USD_CURRENCY, awaitCatalogue } from '~/tests/helpers/payerCatalogue'
import { readOf, requestsTo } from '~/tests/helpers/stubRoutes'
import { useTemplateGlobals } from '~/tests/helpers/templateGlobals'
import { waitUntil } from '~/tests/helpers/waitUntil'
import LayoutHeader from '~/components/layout/Header.vue'
import ClassicHeader from '~/templates/classic/layout/ClassicHeader.vue'

/** The stub server and the loader of the operator's settings; the hooks start the stub before each test and close it after. */
const { replies, stub, loadGlobals } = useTemplateGlobals()

describe('ClassicHeader', () => {
  beforeEach(async () => {
    replies[readOf(CatalogPath.ProductGroups)] = { body: [] }
    replies[readOf(CatalogPath.Products)] = { body: [] }
    replies[readOf(CatalogPath.BillingCurrencies)] = { body: [USD_CURRENCY, AMD_CURRENCY] }
    await loadGlobals()
  })

  it('renders the shared site header', async () => {
    const wrapper = mountWithKit(ClassicHeader, { components: { LayoutHeader } })
    await waitUntil(() => expect(requestsTo(stub().requests, CatalogPath.ProductGroups).length).toBeGreaterThan(0))
    await awaitCatalogue()

    expect(wrapper.find('[data-testid="layout-header"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="layout-logo"]').attributes('href')).toBe('/')
  })
})

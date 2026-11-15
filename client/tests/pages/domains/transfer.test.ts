/**
 * Unit tests for the public domain-transfer page: it pre-fills the fields from the address and hands
 * the visitor on to the signed-in transfer flow with what they typed.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mountPage, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import type { PublicPageSpies } from '~/tests/helpers/publicPage'
import TransferPage from '~/pages/domains/transfer.vue'
import DomainsTransfer from '~/components/domains/DomainsTransfer.vue'
import { localized } from '~/tests/helpers/runtimeStubs'
import { CLIENT_DOMAIN_TRANSFER_PATH } from '~/constants/clientRoutes'

/** The `navigateTo` spy of the current test. */
let navigate: ReturnType<typeof vi.fn>

/** What the stood-in runtime recorded for the current test. */
let spies: PublicPageSpies

/**
 * Mounts the page.
 *
 * @param query The address query string.
 * @returns The wrapper.
 */
const mountTransfer = (query: Record<string, string> = {}) => {
  spies = stubPublicPageRuntime({ query })
  return mountPage(TransferPage, { components: { DomainsTransfer } })
}

/**
 * Reads what a field currently shows.
 *
 * @param wrapper The mounted page.
 * @param testid The field's test id.
 * @returns Its value.
 */
const valueOf = (wrapper: Awaited<ReturnType<typeof mountTransfer>>, testid: string): string =>
  (wrapper.find(`[data-testid="${testid}"]`).element as HTMLInputElement).value

describe('domain transfer page', () => {
  beforeEach(() => {
    navigate = vi.fn()
    vi.stubGlobal('navigateTo', navigate)
  })

  it('pre-fills the domain and the code from the address', async () => {
    const wrapper = await mountTransfer({ domain: 'example.com', eppcode: 'abc123' })

    expect(valueOf(wrapper, 'domains-transfer-domain')).toBe('example.com')
    expect(valueOf(wrapper, 'domains-transfer-epp')).toBe('abc123')
  })

  it('starts with empty fields when the address carries none', async () => {
    const wrapper = await mountTransfer()

    expect(valueOf(wrapper, 'domains-transfer-domain')).toBe('')
  })

  it('does nothing without a domain', async () => {
    const wrapper = await mountTransfer()

    await wrapper.find('[data-testid="domains-transfer-submit"]').trigger('click')

    expect(navigate).not.toHaveBeenCalled()
  })

  it('sends the visitor to the client transfer flow with the domain and the code', async () => {
    const wrapper = await mountTransfer()
    await wrapper.find('[data-testid="domains-transfer-domain"]').setValue('  shop.test  ')
    await wrapper.find('[data-testid="domains-transfer-epp"]').setValue('secret')

    await wrapper.find('[data-testid="domains-transfer-submit"]').trigger('click')

    expect(navigate).toHaveBeenCalledWith(`${localized(CLIENT_DOMAIN_TRANSFER_PATH)}?domain=shop.test&eppcode=secret`)
  })

  it('leaves the code out of the address when none was typed', async () => {
    const wrapper = await mountTransfer()
    await wrapper.find('[data-testid="domains-transfer-domain"]').setValue('shop.test')

    await wrapper.find('[data-testid="domains-transfer-submit"]').trigger('click')

    expect(navigate).toHaveBeenCalledWith(`${localized(CLIENT_DOMAIN_TRANSFER_PATH)}?domain=shop.test`)
  })

  it('sets the head for the page', async () => {
    await mountTransfer()

    expect(spies.useSeo).toHaveBeenCalledWith(expect.objectContaining({ path: '/domains/transfer' }))
  })
})

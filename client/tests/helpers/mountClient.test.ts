/**
 * Tests for `tests/helpers/mountClient.ts`: the client components resolve by the name Nuxt auto-imports them under.
 */
import { describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import ServiceCard from '~/components/client/services/ServiceCard.vue'
import SetupDomainStep from '~/components/client/services/setup/SetupDomainStep.vue'
import { makeClientService } from '~/tests/helpers/clientServiceFixture'

describe('mountClient', () => {
  it('resolves client components by their auto-import name', () => {
    const wrapper = mountClient(ServiceCard, { props: { service: makeClientService(), currency: {} } })

    expect(wrapper.find('[data-testid="status-badge"]').exists()).toBe(true)
  })

  it('resolves client components nested in sub-folders by their auto-import name', () => {
    const wrapper = mountClient(SetupDomainStep, { props: { domain: '', domainError: '', hasPurchasedDomain: false } })

    expect(wrapper.text()).toContain('client.setup.domainTitle')
  })
})

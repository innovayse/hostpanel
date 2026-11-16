/**
 * Unit tests for the checkout page: it owns only the route and renders whichever checkout
 * component the active template provides.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { stubTemplateSlots } from '~/tests/helpers/templateSlots'
import { useTemplate } from '~/composables/useTemplate'
import CheckoutPage from '~/pages/checkout/index.vue'
import { TemplateSlot } from '~/types/templateslot'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer({ useTemplate })

/** Stand-in for the template's checkout component. */
const TemplateCheckout = defineComponent({
  name: 'TemplateCheckout',
  setup: () => () => h('section', { 'data-testid': 'template-checkout' }),
})

describe('checkout page', () => {
  it('renders the checkout component of the active template', async () => {
    const requestedSlots = stubTemplateSlots({ [TemplateSlot.Checkout]: TemplateCheckout })
    await loadPortalSettings(replies)

    const wrapper = mountWithKit(CheckoutPage)

    expect(requestedSlots).toEqual([TemplateSlot.Checkout])
    expect(wrapper.find('[data-testid="template-checkout"]').exists()).toBe(true)
  })
})

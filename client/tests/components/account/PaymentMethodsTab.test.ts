/**
 * Unit tests for PaymentMethodsTab: loading, failed read vs empty and the actions on a saved method.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { h } from 'vue'
import { mountAccount } from '~/tests/helpers/mountAccount'
import { stubLocalePath, stubRoute } from '~/tests/helpers/runtimeStubs'
import PaymentMethodsTab from '~/components/account/PaymentMethodsTab.vue'
import { Permission } from '~/composables/usePermissions'
import { grantPermissions } from '~/tests/helpers/grantPermissions'
import { useAccountStore } from '~/stores/account'
import { ActionOutcome } from '~/types/actionoutcome'
import { CardSaveOutcome } from '~/types/cardsaveoutcome'
import { PaymentMethodType } from '~/types/payment'
import type { PaymentMethod } from '~/types/payment'
import { usePendingCardSave } from '~/composables/usePendingCardSave'

/** What the add-card dialog hands the browser to; the page itself is never left in a test. */
const external = vi.hoisted(() => ({ redirectToExternal: vi.fn() }))

vi.mock('~/utils/externalRedirect', () => external)

vi.mock('~/composables/apis/useClientApi', () => ({ useClientApi: () => ({}) }))
/** The billing API the store reads saved methods through; a test sets what the read answers. */
const billing = vi.hoisted(() => ({ fetchPaymentMethods: vi.fn() }))

vi.mock('~/composables/apis/useBillingApi', () => ({ useBillingApi: () => billing }))
vi.mock('~/composables/apis/useAuthApi', () => ({ useAuthApi: () => ({}) }))
vi.mock('qrcode', () => ({ default: { toDataURL: async (): Promise<string> => '' } }))

/** The built-in card module, as the API lists it. */
const STRIPE = { module: 'stripe', displayName: 'Card' }

/** A gateway module that saves through its own hosted page. */
const HOSTED = { module: 'inecobank', displayName: 'Inecobank' }

/** Stands in for the provider's card form, which needs the payment provider's script. */
const CardFormStub = { name: 'CheckoutStripeCardForm', render: () => h('div', { 'data-testid': 'stripe-card-field' }) }

/** One saved card. */
const method: PaymentMethod = { gatewayModuleId: 'stripe', id: '9', type: PaymentMethodType.Card, description: 'My card', gatewayName: 'stripe', cardLastFour: '4242' }

describe('PaymentMethodsTab', () => {
  beforeEach(() => {
    stubLocalePath('/en')
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    external.redirectToExternal.mockReset()
    sessionStorage.clear()
  })

  /**
   * Mounts the tab with the given modules offered.
   *
   * @param modules - What the API lists as able to save a method.
   * @returns The mounted tab.
   */
  const mountWithModules = (modules = [STRIPE]) => {
    const wrapper = mountAccount(PaymentMethodsTab, { components: { CheckoutStripeCardForm: CardFormStub } })
    useAccountStore().savedMethodModules = modules
    return wrapper
  }

  it('shows a skeleton while loading', async () => {
    const wrapper = mountAccount(PaymentMethodsTab)
    useAccountStore().paymentLoading = true
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="account-payment-loading"]').exists()).toBe(true)
  })

  it('shows the empty state when the read worked and nothing is saved', () => {
    const wrapper = mountAccount(PaymentMethodsTab)

    expect(wrapper.find('[data-testid="ui-empty-state"]').text()).toContain('client.payment.empty')
    expect(wrapper.find('[data-testid="ui-empty-state"]').text()).toContain('client.payment.emptyDesc')
    expect(wrapper.find('[data-testid="account-payment-load-error"]').exists()).toBe(false)
  })

  it('shows the empty state, not a failure, when the API answers an empty list because the deployment has no card gateway key', async () => {
    billing.fetchPaymentMethods.mockResolvedValue([])
    const wrapper = mountAccount(PaymentMethodsTab)
    const store = useAccountStore()

    await store.loadPaymentMethods()
    await wrapper.vm.$nextTick()

    expect(billing.fetchPaymentMethods).toHaveBeenCalledTimes(1)
    expect(wrapper.find('[data-testid="ui-empty-state"]').text()).toContain('client.payment.empty')
    expect(wrapper.find('[data-testid="account-payment-load-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="account-payment-loading"]').exists()).toBe(false)
  })

  it('links a customer without a saved card to the invoices, where the hosted gateway takes the payment', () => {
    const wrapper = mountAccount(PaymentMethodsTab)

    expect(wrapper.find('[data-testid="account-payment-invoices"]').attributes('href')).toBe('/en/client/invoices')
  })

  it('shows the failure, not the empty state, when the read failed', async () => {
    const wrapper = mountAccount(PaymentMethodsTab)
    useAccountStore().paymentLoadError = 'Billing is down'
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="account-payment-load-error"]').text()).toContain('Billing is down')
    expect(wrapper.find('[data-testid="ui-empty-state"]').exists()).toBe(false)
  })

  it('offers no "Add card" while no gateway module can save a card', async () => {
    const wrapper = mountAccount(PaymentMethodsTab)
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="account-payment-add"]').exists()).toBe(false)
  })

  it('opens the add-card dialog with the agreement unticked and the card form for the built-in module', async () => {
    const wrapper = mountWithModules()
    await wrapper.vm.$nextTick()

    await wrapper.find('[data-testid="account-payment-add"]').trigger('click')

    expect(wrapper.find('[data-testid="account-payment-add-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="account-payment-consent-input"]').element).toHaveProperty('checked', false)
    expect(wrapper.find('[data-testid="stripe-card-field"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="account-payment-module"]').exists()).toBe(false)
  })

  it('lets the payer choose the module when there are several, and mounts no card form for a hosted one', async () => {
    const wrapper = mountWithModules([HOSTED, STRIPE])
    await wrapper.vm.$nextTick()

    await wrapper.find('[data-testid="account-payment-add"]').trigger('click')

    expect(wrapper.find('[data-testid="account-payment-module"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="stripe-card-field"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="account-payment-add-submit"]').attributes('disabled')).toBeUndefined()
  })

  it('keeps the save button disabled while the provider card field is not ready', async () => {
    const wrapper = mountWithModules()
    await wrapper.vm.$nextTick()
    await wrapper.find('[data-testid="account-payment-add"]').trigger('click')

    expect(wrapper.find('[data-testid="account-payment-add-submit"]').attributes('disabled')).toBeDefined()
  })

  it('does not save a card nobody agreed to', async () => {
    const wrapper = mountWithModules([HOSTED])
    const add = vi.spyOn(useAccountStore(), 'addPaymentMethod')
    await wrapper.vm.$nextTick()
    await wrapper.find('[data-testid="account-payment-add"]').trigger('click')

    await wrapper.find('[data-testid="account-payment-add-form"]').trigger('submit')
    await wrapper.vm.$nextTick()

    expect(add).not.toHaveBeenCalled()
    expect(wrapper.find('[data-testid="account-payment-consent-required"]').exists()).toBe(true)
  })

  it('sends the payer to the gateway page when the store answers a redirect', async () => {
    const wrapper = mountWithModules([HOSTED])
    const add = vi.spyOn(useAccountStore(), 'addPaymentMethod').mockResolvedValue({ outcome: CardSaveOutcome.Redirect, redirectUrl: 'https://bank.example/save' })
    await wrapper.vm.$nextTick()
    await wrapper.find('[data-testid="account-payment-add"]').trigger('click')
    await wrapper.find('[data-testid="account-payment-consent"]').trigger('click')

    await wrapper.find('[data-testid="account-payment-add-form"]').trigger('submit')
    await Promise.resolve()

    expect(add).toHaveBeenCalledWith('inecobank', true, expect.any(Function))
    expect(external.redirectToExternal).toHaveBeenCalledWith('https://bank.example/save')
  })

  it('reports a saved card, closes the dialog and reads the list again', async () => {
    const wrapper = mountWithModules([HOSTED])
    const store = useAccountStore()
    vi.spyOn(store, 'addPaymentMethod').mockResolvedValue({ outcome: CardSaveOutcome.Saved, redirectUrl: null })
    const reload = vi.spyOn(store, 'loadPaymentMethods').mockResolvedValue()
    await wrapper.vm.$nextTick()
    await wrapper.find('[data-testid="account-payment-add"]').trigger('click')
    await wrapper.find('[data-testid="account-payment-consent"]').trigger('click')

    await wrapper.find('[data-testid="account-payment-add-form"]').trigger('submit')
    await Promise.resolve()
    await wrapper.vm.$nextTick()

    expect(reload).toHaveBeenCalled()
    expect(wrapper.find('[data-testid="account-payment-success"]').text()).toBe('client.payment.cardSaved')
    expect(wrapper.find('[data-testid="account-payment-add-form"]').exists()).toBe(false)
  })

  it('shows the store sentence in the dialog when the save is refused', async () => {
    const wrapper = mountWithModules([HOSTED])
    const store = useAccountStore()
    vi.spyOn(store, 'addPaymentMethod').mockImplementation(async () => {
      store.paymentActionError = 'Tick the agreement.'
      return { outcome: CardSaveOutcome.Failed, redirectUrl: null }
    })
    await wrapper.vm.$nextTick()
    await wrapper.find('[data-testid="account-payment-add"]').trigger('click')
    await wrapper.find('[data-testid="account-payment-consent"]').trigger('click')

    await wrapper.find('[data-testid="account-payment-add-form"]').trigger('submit')
    await Promise.resolve()
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="account-payment-add-error"]').text()).toContain('Tick the agreement.')
  })

  it('withdraws the missing-agreement warning once the agreement is ticked', async () => {
    const wrapper = mountWithModules([HOSTED])
    await wrapper.vm.$nextTick()
    await wrapper.find('[data-testid="account-payment-add"]').trigger('click')
    await wrapper.find('[data-testid="account-payment-add-form"]').trigger('submit')
    expect(wrapper.find('[data-testid="account-payment-consent-required"]').exists()).toBe(true)

    await wrapper.find('[data-testid="account-payment-consent"]').trigger('click')

    expect(wrapper.find('[data-testid="account-payment-consent-required"]').exists()).toBe(false)
  })

  it('marks a card charged automatically and offers to turn that off, only after confirmation', async () => {
    const wrapper = mountAccount(PaymentMethodsTab)
    const store = useAccountStore()
    store.paymentMethods = [{ ...method, autoCharge: true }]
    const turnOff = vi.spyOn(store, 'turnOffAutoCharge').mockResolvedValue(ActionOutcome.Succeeded)
    vi.spyOn(store, 'loadPaymentMethods').mockResolvedValue()
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="account-payment-auto-badge"]').exists()).toBe(true)
    await wrapper.find('[data-testid="account-payment-auto-off"]').trigger('click')
    expect(turnOff).not.toHaveBeenCalled()

    await wrapper.find('[data-testid="ui-confirm-confirm"]').trigger('click')
    await Promise.resolve()
    await wrapper.vm.$nextTick()

    expect(turnOff).toHaveBeenCalledTimes(1)
    expect(wrapper.find('[data-testid="account-payment-success"]').text()).toBe('client.payment.turnOffDone')
  })

  it('offers no turn-off button when no card is charged automatically', async () => {
    const wrapper = mountAccount(PaymentMethodsTab)
    useAccountStore().paymentMethods = [method]
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="account-payment-auto-off"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="account-payment-auto-badge"]').exists()).toBe(false)
  })

  it('makes a method the default and reports it', async () => {
    const wrapper = mountAccount(PaymentMethodsTab)
    const store = useAccountStore()
    store.paymentMethods = [method]
    vi.spyOn(store, 'setDefaultPaymentMethod').mockResolvedValue(ActionOutcome.Succeeded)
    const reload = vi.spyOn(store, 'loadPaymentMethods').mockResolvedValue()
    await wrapper.vm.$nextTick()

    await wrapper.find('[data-testid="account-payment-default-9"]').trigger('click')
    await Promise.resolve()
    await wrapper.vm.$nextTick()

    expect(reload).toHaveBeenCalled()
    expect(wrapper.find('[data-testid="account-payment-success"]').text()).toBe('client.payment.defaultSet')
  })

  it('removes a method only after confirmation', async () => {
    const wrapper = mountAccount(PaymentMethodsTab)
    const store = useAccountStore()
    store.paymentMethods = [method]
    const remove = vi.spyOn(store, 'removePaymentMethod').mockResolvedValue(ActionOutcome.Succeeded)
    vi.spyOn(store, 'loadPaymentMethods').mockResolvedValue()
    await wrapper.vm.$nextTick()

    await wrapper.find('[data-testid="account-payment-remove-9"]').trigger('click')
    expect(remove).not.toHaveBeenCalled()

    await wrapper.find('[data-testid="ui-confirm-confirm"]').trigger('click')
    await Promise.resolve()

    expect(remove).toHaveBeenCalledWith(method)
  })

  it('shows the API sentence of a refused action', async () => {
    const wrapper = mountAccount(PaymentMethodsTab)
    useAccountStore().paymentActionError = 'Cannot remove the last method'
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="account-payment-action-error"]').text()).toContain('Cannot remove the last method')
  })

  describe('back from a gateway page', () => {
    /**
     * Stands in for a route that carries the return parameters and a router that records the clean-up.
     *
     * @param query - The route query.
     * @returns The router's `replace` spy.
     */
    const returnWith = (query: Record<string, string>) => {
      const replace = vi.fn()
      stubRoute({ query })
      vi.stubGlobal('useRouter', () => ({ replace }))
      return replace
    }

    /**
     * Creates the Pinia the tab will read, so its store can be spied on before the tab mounts and returns.
     *
     * @returns The Pinia and the account store in it.
     */
    const freshStore = () => {
      const pinia = createPinia()
      setActivePinia(pinia)
      return { pinia, store: useAccountStore() }
    }

    it('completes the remembered session, reports the card and removes the return parameters', async () => {
      const replace = returnWith({ tab: 'payment', module: 'inecobank' })
      usePendingCardSave().remember('inecobank', 'sess-9')
      const { pinia, store } = freshStore()
      const complete = vi.spyOn(store, 'completeCardSave').mockResolvedValue(ActionOutcome.Succeeded)
      vi.spyOn(store, 'loadPaymentMethods').mockResolvedValue()

      const wrapper = mountAccount(PaymentMethodsTab, { pinia })
      await vi.waitFor(() => expect(complete).toHaveBeenCalledWith('inecobank', 'sess-9'))
      await wrapper.vm.$nextTick()

      expect(replace).toHaveBeenCalledWith({ query: { tab: 'payment', module: undefined, session: undefined } })
      expect(wrapper.find('[data-testid="account-payment-success"]').text()).toBe('client.payment.cardSaved')
    })

    it('prefers the session the gateway put in the address', async () => {
      returnWith({ tab: 'payment', module: 'inecobank', session: 'from-query' })
      const { pinia, store } = freshStore()
      const complete = vi.spyOn(store, 'completeCardSave').mockResolvedValue(ActionOutcome.Succeeded)
      vi.spyOn(store, 'loadPaymentMethods').mockResolvedValue()

      mountAccount(PaymentMethodsTab, { pinia })

      await vi.waitFor(() => expect(complete).toHaveBeenCalledWith('inecobank', 'from-query'))
    })

    it('shows the API sentence when completing is refused, and no success', async () => {
      returnWith({ tab: 'payment', module: 'inecobank' })
      usePendingCardSave().remember('inecobank', 'sess-9')
      const { pinia, store } = freshStore()
      vi.spyOn(store, 'completeCardSave').mockImplementation(async () => {
        store.paymentActionError = 'The card was not saved.'
        return ActionOutcome.Failed
      })

      const wrapper = mountAccount(PaymentMethodsTab, { pinia })
      await vi.waitFor(() => expect(wrapper.find('[data-testid="account-payment-action-error"]').exists()).toBe(true))

      expect(wrapper.find('[data-testid="account-payment-action-error"]').text()).toContain('The card was not saved.')
      expect(wrapper.find('[data-testid="account-payment-success"]').exists()).toBe(false)
    })

    it('does nothing for an address without the return parameters', async () => {
      const replace = returnWith({ tab: 'payment' })
      const { pinia, store } = freshStore()
      const complete = vi.spyOn(store, 'completeCardSave')

      mountAccount(PaymentMethodsTab, { pinia })
      await Promise.resolve()

      expect(complete).not.toHaveBeenCalled()
      expect(replace).not.toHaveBeenCalled()
    })
  })

  describe('without the permission to view and pay invoices', () => {
    it('shows only the reason, no list, no empty state and no skeleton', async () => {
      const wrapper = mountAccount(PaymentMethodsTab)
      useAccountStore().paymentMethods = [method]
      await grantPermissions(Permission.ViewEmails)

      expect(wrapper.find('[data-testid="account-payment-denied"]').text()).toContain('client.payment.permissionDenied')
      expect(wrapper.find('[data-testid="account-payment-default-9"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="account-payment-remove-9"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="ui-empty-state"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="account-payment-loading"]').exists()).toBe(false)
    })

    it('shows the saved methods when the person holds the permission', async () => {
      const wrapper = mountAccount(PaymentMethodsTab)
      useAccountStore().paymentMethods = [method]
      await grantPermissions(Permission.ViewPayInvoices)

      expect(wrapper.find('[data-testid="account-payment-denied"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="account-payment-default-9"]').exists()).toBe(true)
    })
  })
})

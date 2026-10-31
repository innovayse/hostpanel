/**
 * Unit tests for StripeCardForm: it mounts the provider's card element into its frame, reflects
 * readiness and validation errors, and confirms a payment through the element.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { ref } from 'vue'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import StripeCardForm from '~/components/checkout/StripeCardForm.vue'
import { buildStripeCardStyle } from '~/utils/stripeCardStyle'

/** Handlers the form registered on the fake card element, by event name. */
let handlers: Record<string, (event?: { error?: { message?: string } }) => void>

/** The fake card element the fake provider hands out. */
const cardElement = {
  mount: vi.fn(),
  on: vi.fn((name: string, handler: (event?: { error?: { message?: string } }) => void) => {
    handlers[name] = handler
  }),
  update: vi.fn(),
  destroy: vi.fn(),
}

/** The fake provider instance. */
const stripe = {
  elements: vi.fn(() => ({ create: vi.fn(() => cardElement) })),
  confirmCardPayment: vi.fn(),
  confirmCardSetup: vi.fn(),
}

/** The page's colour mode, flipped by tests. */
const isDark = ref(false)

/**
 * Mounts the form and waits for the provider to be set up.
 *
 * @param available - Whether the provider loads; false stands for a missing publishable key.
 * @returns The wrapper.
 */
const mountForm = async (available = true) => {
  vi.stubGlobal('useStripe', () => ({ getStripe: async () => (available ? stripe : null) }))
  const wrapper = mountWithKit(StripeCardForm, { attachToBody: true })
  await flushPromises()
  return wrapper
}

/**
 * Reads the exposed `confirmPayment` of a mounted form.
 *
 * @param wrapper - The mounted form.
 * @returns The exposed function.
 */
const confirmOf = (wrapper: Awaited<ReturnType<typeof mountForm>>) =>
  (wrapper.vm as unknown as { confirmPayment: (secret: string) => Promise<{ id: string }> }).confirmPayment

describe('StripeCardForm', () => {
  beforeEach(() => {
    handlers = {}
    isDark.value = false
    cardElement.mount.mockClear()
    cardElement.on.mockClear()
    cardElement.update.mockClear()
    cardElement.destroy.mockClear()
    stripe.confirmCardPayment.mockReset()
    stripe.confirmCardSetup.mockReset()
    vi.stubGlobal('useAppColorMode', () => ({ isDark }))
  })

  it('labels the card field', async () => {
    const wrapper = await mountForm()

    expect(wrapper.text()).toContain('checkout.cardDetails')
    wrapper.unmount()
  })

  it('mounts the card element into the frame', async () => {
    const wrapper = await mountForm()

    const frame = wrapper.find('[data-testid="stripe-card-frame"]')
    expect(cardElement.mount).toHaveBeenCalledWith(frame.element)
    wrapper.unmount()
  })

  it('does nothing when the provider is not available', async () => {
    const wrapper = await mountForm(false)

    expect(cardElement.mount).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('checkout.card.unavailable')
    wrapper.unmount()
  })

  it('tells its parent whether the card field is ready', async () => {
    const wrapper = await mountForm()
    const ready = () => (wrapper.vm as unknown as { ready: boolean }).ready
    expect(ready()).toBe(false)

    handlers.ready?.()
    await flushPromises()

    expect(ready()).toBe(true)
    wrapper.unmount()
  })

  it('marks the frame ready when the element reports it', async () => {
    const wrapper = await mountForm()

    handlers.ready?.()
    await flushPromises()

    expect(wrapper.find('[data-testid="stripe-card-frame"]').classes()).toContain('border-cyan-600/50')
    wrapper.unmount()
  })

  it('shows the validation message of the element and clears it', async () => {
    const wrapper = await mountForm()

    handlers.change?.({ error: { message: 'Your card number is incomplete.' } })
    await flushPromises()
    expect(wrapper.text()).toContain('Your card number is incomplete.')
    expect(wrapper.find('[data-testid="stripe-card-frame"]').classes()).toContain('border-red-500')

    handlers.change?.({})
    await flushPromises()
    expect(wrapper.text()).not.toContain('Your card number is incomplete.')
    wrapper.unmount()
  })

  it('restyles the element when the colour mode changes', async () => {
    const wrapper = await mountForm()

    isDark.value = true
    await flushPromises()

    expect(cardElement.update).toHaveBeenCalledWith({ style: buildStripeCardStyle(true) })
    wrapper.unmount()
  })

  it('destroys the element on unmount', async () => {
    const wrapper = await mountForm()

    wrapper.unmount()

    expect(cardElement.destroy).toHaveBeenCalledTimes(1)
  })

  it('confirms saving the card with the setup secret and returns the setup intent id', async () => {
    stripe.confirmCardSetup.mockResolvedValue({ setupIntent: { id: 'seti_1' } })
    const wrapper = await mountForm()
    const confirmSetup = (wrapper.vm as unknown as { confirmSetup: (secret: string) => Promise<{ id: string }> }).confirmSetup

    await expect(confirmSetup('seti_secret')).resolves.toEqual({ id: 'seti_1' })
    expect(stripe.confirmCardSetup).toHaveBeenCalledWith('seti_secret', { payment_method: { card: cardElement } })
    wrapper.unmount()
  })

  it('throws the provider sentence when the card is refused while saving', async () => {
    stripe.confirmCardSetup.mockResolvedValue({ error: { message: 'Your card was declined.' } })
    const wrapper = await mountForm()
    const confirmSetup = (wrapper.vm as unknown as { confirmSetup: (secret: string) => Promise<{ id: string }> }).confirmSetup

    await expect(confirmSetup('seti_secret')).rejects.toThrow('Your card was declined.')
    wrapper.unmount()
  })

  it('confirms the payment with the client secret and returns the intent id', async () => {
    stripe.confirmCardPayment.mockResolvedValue({ paymentIntent: { id: 'pi_1' } })
    const wrapper = await mountForm()

    await expect(confirmOf(wrapper)('secret_1')).resolves.toEqual({ id: 'pi_1' })
    expect(stripe.confirmCardPayment).toHaveBeenCalledWith('secret_1', { payment_method: { card: cardElement } })
    wrapper.unmount()
  })

  it('rejects with the provider message when the payment fails', async () => {
    stripe.confirmCardPayment.mockResolvedValue({ error: { message: 'Card declined' } })
    const wrapper = await mountForm()

    await expect(confirmOf(wrapper)('secret_1')).rejects.toThrow('Card declined')
    wrapper.unmount()
  })

  it('rejects with a worded fallback when the provider gives no message', async () => {
    stripe.confirmCardPayment.mockResolvedValue({ error: {} })
    const wrapper = await mountForm()

    await expect(confirmOf(wrapper)('secret_1')).rejects.toThrow('checkout.card.failed')
    wrapper.unmount()
  })

  it('rejects when the provider returns no intent', async () => {
    stripe.confirmCardPayment.mockResolvedValue({})
    const wrapper = await mountForm()

    await expect(confirmOf(wrapper)('secret_1')).rejects.toThrow('checkout.card.noIntent')
    wrapper.unmount()
  })

  it('rejects when the provider never loaded', async () => {
    const wrapper = await mountForm(false)

    await expect(confirmOf(wrapper)('secret_1')).rejects.toThrow('checkout.card.notInitialized')
    wrapper.unmount()
  })
})

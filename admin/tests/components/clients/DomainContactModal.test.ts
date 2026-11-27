/**
 * Mount tests for DomainContactModal: placeholders, trimmed payload, close, the prefill from what the registrar holds and the
 * registry's extra fields the registrar plugin asks for. Every key and zone is invented.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import DomainContactModal from '@/components/clients/DomainContactModal.vue'
import { json } from '../../helpers/fetch'
import { createTestI18n } from '../../helpers/i18n'
import { stubRoutes } from '../../helpers/routes'

/** The contact the registrar holds, with one extra value stored beside it. */
const HELD = {
  firstName: 'Ada',
  lastName: 'Lovelace',
  organization: null,
  email: 'ada@example.test',
  phone: '+10000000',
  address1: '1 Main St',
  address2: null,
  city: 'City',
  state: 'State',
  postalCode: '00000',
  country: 'XX',
  extendedAttributes: { 'holder-number': 'N-1' },
}

/** One required text field the registrar plugin declares. */
const FIELDS = [{
  key: 'holder-number',
  label: 'Holder number',
  type: 'Text',
  required: true,
  description: null,
  placeholder: null,
  pattern: null,
  minLength: null,
  maxLength: null,
  options: [],
}]

/**
 * Mounts the modal with the dialog teleport inlined.
 *
 * @param fieldErrors - The refusal per field key from the last failed save.
 * @returns The wrapper.
 */
const mountModal = (fieldErrors: Record<string, string> = {}) =>
  mount(DomainContactModal, {
    props: { saving: false, domainId: 3, domainName: 'name.alpha', fieldErrors },
    global: { plugins: [createTestI18n(), createPinia()], stubs: { teleport: true } },
  })

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('DomainContactModal', () => {
  it('renders every contact field with a placeholder', async () => {
    stubRoutes({})
    const wrapper = mountModal()
    await flushPromises()

    const inputs = wrapper.findAll('input')

    expect(inputs).toHaveLength(11)
    expect(inputs.every(i => (i.attributes('placeholder') ?? '') !== '')).toBe(true)
  })

  it('emits the trimmed payload and the extra values on submit', async () => {
    stubRoutes({})
    const wrapper = mountModal()
    await flushPromises()

    await wrapper.get('[data-testid="clients-domain-contact-firstName"]').setValue('  Ada ')
    await wrapper.get('[data-testid="clients-domain-contact-country"]').setValue('XX')
    await wrapper.get('form').trigger('submit')

    const saved = wrapper.emitted('save')?.[0] as [Record<string, string>, Record<string, string>]
    const [payload, attributes] = saved
    expect(payload.firstName).toBe('Ada')
    expect(payload.country).toBe('XX')
    expect(attributes).toEqual({})
  })

  it('emits close from cancel', async () => {
    stubRoutes({})
    const wrapper = mountModal()
    await flushPromises()

    await wrapper.get('[data-testid="clients-domain-contact-cancel"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  it('prefills the form and the extra values from what the registrar holds', async () => {
    stubRoutes({
      'GET /api/domains/3/contact': () => json(HELD),
      'GET /api/domains/fields': () => json(FIELDS),
    })
    const wrapper = mountModal()
    await flushPromises()

    expect((wrapper.get('[data-testid="clients-domain-contact-firstName"]').element as HTMLInputElement).value).toBe('Ada')
    expect((wrapper.get('[data-testid="clients-domain-fields-holder-number"]').element as HTMLInputElement).value).toBe('N-1')
  })

  it('asks for the fields of a contact change and sends the values that apply', async () => {
    const mock = stubRoutes({
      'GET /api/domains/3/contact': () => json(HELD),
      'GET /api/domains/fields': () => json(FIELDS),
    })
    const wrapper = mountModal()
    await flushPromises()
    await wrapper.get('[data-testid="clients-domain-fields-holder-number"]').setValue('N-2')

    await wrapper.get('form').trigger('submit')

    expect(String(mock.mock.calls.find(call => String(call[0]).startsWith('/api/domains/fields'))?.[0]))
      .toBe('/api/domains/fields?domain=name.alpha&operation=contactChange&holder=individual')
    const saved = wrapper.emitted('save')?.[0] as [Record<string, string>, Record<string, string>]
    const [, attributes] = saved
    expect(attributes).toEqual({ 'holder-number': 'N-2' })
  })

  it('shows the API refusal of an extra field next to it', async () => {
    stubRoutes({ 'GET /api/domains/fields': () => json(FIELDS) })
    const wrapper = mountModal({ 'holder-number': 'The registry refused it.' })
    await flushPromises()

    expect(wrapper.text()).toContain('The registry refused it.')
  })
})

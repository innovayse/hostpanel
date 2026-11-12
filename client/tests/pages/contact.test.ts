/**
 * Tests for the contact page against the real support store, the real support API composable and the
 * real transport, with a stub BFF answering on a real socket: the form posts through the store, the
 * consent guard runs before any request, the API's refusal and field messages are rendered as given,
 * and the contact details link to the right targets.
 *
 * @module tests/pages/contact.test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import { defineComponent, h, ref } from 'vue'
import { mountPage, registerPageComponents, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import type { PublicPageSpies } from '~/tests/helpers/publicPage'
import { until } from '~/tests/helpers/settle'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import UiDropdown from '~/components/ui/UiDropdown.vue'
import { ContactService } from '~/types/contactservice'

const { default: Page } = await import('~/pages/contact.vue')

/** The public contact endpoint the form posts to. */
const CONTACT_PATH = '/api/contact'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it submits. */
let table: StubRouteTable

/** Stands in for the FAQ preview section. */
const SectionsFaqStub = defineComponent({
  name: 'SectionsFAQ',
  props: { limit: { type: Number, default: undefined } },
  setup: props => () => h('div', { 'data-testid': 'sections-faq', 'data-limit': String(props.limit) }),
})

/** Spies of the stood-in runtime, renewed before each test. */
let spies: PublicPageSpies

/**
 * Fills the form with a valid enquiry. The consent box is left for the caller.
 *
 * @param wrapper - The mounted page.
 */
const fillForm = async (wrapper: VueWrapper): Promise<void> => {
  await wrapper.find('[data-testid="contact-name"] input').setValue('Ada Lovelace')
  await wrapper.find('[data-testid="contact-email"] input').setValue('ada@example.test')
  await wrapper.find('[data-testid="contact-phone"] input').setValue('+10000000')
  await wrapper.find('[data-testid="contact-message"] textarea').setValue('I need a website')
  wrapper.findComponent(UiDropdown).vm.$emit('update:modelValue', ContactService.Support)
}

/**
 * Ticks the consent box.
 *
 * @param wrapper - The mounted page.
 */
const tickConsent = async (wrapper: VueWrapper): Promise<void> => {
  await wrapper.find('[data-testid="contact-consent"] button').trigger('click')
}

/**
 * Submits the form and waits until it reports its outcome: the request crosses a real socket, so the
 * store finishes after the page's own promise flush.
 *
 * @param wrapper - The mounted page.
 */
const submit = async (wrapper: VueWrapper): Promise<void> => {
  await wrapper.find('[data-testid="contact-form"]').trigger('submit')
  await until(
    () => wrapper.find('[data-testid="contact-success"]').exists() || wrapper.find('[data-testid="contact-error"]').exists(),
    'the form to report its outcome',
  )
}

describe('pages/contact', () => {
  beforeEach(async () => {
    spies = stubPublicPageRuntime()
    registerPageComponents({ SectionsFAQ: SectionsFaqStub })
    table = { [`POST ${CONTACT_PATH}`]: { status: 204 } }
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
    await stub.close()
  })

  it('sets the page tags and publishes the contact-page structured data', async () => {
    await mountPage(Page)

    expect(spies.useSeo).toHaveBeenCalledWith(expect.objectContaining({ title: 'seo.contact.title', path: '/contact' }))
    const published = spies.injectSchema.mock.calls[0]?.[0] as Array<Record<string, unknown>>
    expect(published.some(entry => entry['@type'] === 'ContactPage')).toBe(true)
  })

  it('shows the hero and the FAQ preview', async () => {
    const wrapper = await mountPage(Page)

    expect(wrapper.find('h1').text()).toContain('contact.hero.title')
    expect(wrapper.find('h1').text()).toContain('contact.hero.titleHighlight')
    expect(wrapper.find('[data-testid="sections-faq"]').attributes('data-limit')).toBe('4')
  })

  it('gives every text field its placeholder', async () => {
    const wrapper = await mountPage(Page)

    expect(wrapper.find('[data-testid="contact-name"] input').attributes('placeholder')).toBe('contact.form.placeholders.name')
    expect(wrapper.find('[data-testid="contact-email"] input').attributes('placeholder')).toBe('contact.form.placeholders.email')
    expect(wrapper.find('[data-testid="contact-phone"] input').attributes('placeholder')).toBe('contact.form.placeholders.phone')
    expect(wrapper.find('[data-testid="contact-message"] textarea').attributes('placeholder')).toBe('contact.form.placeholders.message')
  })

  it('shows the working hours the operator set and hides the row when none are set', async () => {
    vi.stubGlobal('useContactHours', () => ({ workingHours: ref('08:00 - 17:00 UTC+2') }))
    const shown = await mountPage(Page)
    expect(shown.find('[data-testid="contact-hours"]').text()).toContain('08:00 - 17:00 UTC+2')

    vi.stubGlobal('useContactHours', () => ({ workingHours: ref('') }))
    const hidden = await mountPage(Page)
    expect(hidden.find('[data-testid="contact-hours"]').exists()).toBe(false)
  })

  it('links the operator-configured contact details to mail, phone and the two messengers', async () => {
    vi.stubGlobal('useContactDetails', () => ({
      email: ref('hello@acme.test'),
      phone: ref('+1 555 010 200'),
      phoneLink: ref('+1555010200'),
      telegramUrl: ref('https://t.me/acme'),
      whatsappUrl: ref('https://wa.me/1555010200'),
      address: ref('1 Example Street\nSpringfield'),
      mapUrl: ref('https://maps.example.test/embed?ll=1,2'),
    }))
    const wrapper = await mountPage(Page)

    expect(wrapper.find('[data-testid="contact-email-link"]').attributes('href')).toBe('mailto:hello@acme.test')
    expect(wrapper.find('[data-testid="contact-phone-link"]').attributes('href')).toBe('tel:+1555010200')
    expect(wrapper.find('[data-testid="contact-telegram"]').attributes('href')).toBe('https://t.me/acme')
    expect(wrapper.find('[data-testid="contact-telegram"]').attributes('target')).toBe('_blank')
    expect(wrapper.find('[data-testid="contact-whatsapp"]').attributes('href')).toBe('https://wa.me/1555010200')
    expect(wrapper.find('[data-testid="contact-address"]').text()).toContain('1 Example Street')
    expect(wrapper.find('[data-testid="contact-map"] iframe').attributes('src')).toContain('theme=light')
    expect(wrapper.find('[data-testid="contact-map"] iframe').attributes('title')).toBe('contact.map.title')
  })

  it('makes the map follow the colour mode', async () => {
    vi.stubGlobal('useContactDetails', () => ({
      email: ref(''), phone: ref(''), phoneLink: ref(''), telegramUrl: ref(''), whatsappUrl: ref(''),
      address: ref(''), mapUrl: ref('https://maps.example.test/embed?ll=1,2'),
    }))
    stubPublicPageRuntime({ dark: true })
    const dark = await mountPage(Page)

    expect(dark.find('[data-testid="contact-map"] iframe').attributes('src')).toContain('theme=dark')
  })

  it('hides every contact item the operator has not configured', async () => {
    const wrapper = await mountPage(Page)

    expect(wrapper.find('[data-testid="contact-email-link"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="contact-phone-link"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="contact-telegram"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="contact-whatsapp"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="contact-map"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="contact-office"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="contact-methods"]').exists()).toBe(false)
  })

  it('links the consent text to the localised privacy page', async () => {
    const wrapper = await mountPage(Page)

    expect(wrapper.find('[data-testid="contact-privacy-link"]').attributes('href')).toBe('/en/privacy')
  })

  it('refuses a submit without consent before any request', async () => {
    const wrapper = await mountPage(Page)
    await fillForm(wrapper)

    await submit(wrapper)

    expect(requestsTo(stub.requests, CONTACT_PATH)).toHaveLength(0)
    expect(wrapper.find('[data-testid="contact-error"]').text()).toContain('contact.form.error.consentRequired')
  })

  it('posts the enquiry, shows the success message and empties the form', async () => {
    const wrapper = await mountPage(Page)
    await fillForm(wrapper)
    await tickConsent(wrapper)

    await submit(wrapper)

    const [sent] = requestsTo(stub.requests, CONTACT_PATH)
    expect(sent?.method).toBe('POST')
    expect(JSON.parse(sent?.body ?? '')).toEqual(expect.objectContaining({
      name: 'Ada Lovelace',
      email: 'ada@example.test',
      phone: '+10000000',
      service: ContactService.Support,
      message: 'I need a website',
    }))
    expect(wrapper.find('[data-testid="contact-success"]').text()).toContain('contact.form.success.title')
    expect(wrapper.find('[data-testid="contact-error"]').exists()).toBe(false)
    expect((wrapper.find('[data-testid="contact-name"] input').element as HTMLInputElement).value).toBe('')
  })

  it('hides the success message after five seconds', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout'] })
    const wrapper = await mountPage(Page)
    await fillForm(wrapper)
    await tickConsent(wrapper)
    await submit(wrapper)
    expect(wrapper.find('[data-testid="contact-success"]').exists()).toBe(true)

    vi.advanceTimersByTime(5000)
    await flushPromises()

    expect(wrapper.find('[data-testid="contact-success"]').exists()).toBe(false)
  })

  it('shows the API sentence when the message is refused and keeps what was typed', async () => {
    table[`POST ${CONTACT_PATH}`] = { status: 502, body: { detail: 'The inbox is not configured.' } }
    const wrapper = await mountPage(Page)
    await fillForm(wrapper)
    await tickConsent(wrapper)

    await submit(wrapper)

    expect(wrapper.find('[data-testid="contact-error"]').text()).toContain('The inbox is not configured.')
    expect(wrapper.find('[data-testid="contact-success"]').exists()).toBe(false)
    expect((wrapper.find('[data-testid="contact-name"] input').element as HTMLInputElement).value).toBe('Ada Lovelace')
  })

  it('renders the API field message under its field', async () => {
    table[`POST ${CONTACT_PATH}`] = { status: 400, body: { errors: { email: ['Enter a valid address.'] } } }
    const wrapper = await mountPage(Page)
    await fillForm(wrapper)
    await tickConsent(wrapper)

    await submit(wrapper)

    expect(wrapper.find('[data-testid="contact-email"]').text()).toContain('Enter a valid address.')
    expect(wrapper.find('[data-testid="contact-name"]').text()).not.toContain('Enter a valid address.')
  })
})

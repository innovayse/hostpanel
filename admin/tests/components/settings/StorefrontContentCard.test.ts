/**
 * Tests for StorefrontContentCard: the figures, hero badge and contact fields, each saved on its
 * own, and the warning for a row the API never seeded.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import StorefrontContentCard from '@/components/settings/StorefrontContentCard.vue'
import type { Setting, SettingSave } from '@/types/setting'
import { mountSettingsCard } from '../../helpers/settingsPage'
import { selectOf, selectText } from '../../helpers/select'

/** The save callback the card is given; it answers that the value was stored. */
const save = vi.fn<SettingSave>(async () => ({ saved: true, error: null }))

beforeEach(() => {
  save.mockClear()
})

/** Every row the card edits, as the API seeds them. */
const rows: Setting[] = [
  { id: 1, key: 'portal.stats.clients', value: '5,000+' },
  { id: 2, key: 'portal.stats.uptime', value: '99.9%' },
  { id: 3, key: 'portal.stats.regions', value: '12' },
  { id: 4, key: 'portal.hero.badge', value: 'New' },
  { id: 5, key: 'portal.contact.email', value: 'hi@example.com' },
  { id: 6, key: 'portal.contact.phone', value: '+1 555' },
  { id: 7, key: 'portal.contact.whatsapp', value: '+1 556' },
  { id: 8, key: 'portal.contact.telegram', value: '@acme' },
  { id: 9, key: 'portal.contact.address', value: '1 Main St' },
  { id: 10, key: 'portal.contact.map_url', value: 'https://maps.example.com/x' },
  { id: 11, key: 'portal.contact.hours', value: 'Mon-Fri 09:00-18:00' },
  { id: 12, key: 'portal.contact.hours.ru', value: 'Пн-Пт 09:00-18:00' },
  { id: 13, key: 'portal.contact.hours.hy', value: 'Երկ-Ուրբ 09:00-18:00' },
  { id: 14, key: 'portal.contact.timezone', value: 'UTC+4' },
  { id: 15, key: 'portal.contact.address.ru', value: 'ул. Примерная, 1' },
  { id: 16, key: 'portal.contact.address.hy', value: 'Օրինակ փողոց 1' },
  { id: 17, key: 'portal.contact.address.de', value: 'Beispielstrasse 1' },
  { id: 18, key: 'portal.contact.address.es', value: 'Calle Ejemplo 1' },
  { id: 19, key: 'portal.contact.hours.de', value: 'Mo-Fr 09:00-18:00' },
  { id: 20, key: 'portal.contact.hours.es', value: 'Lu-Vi 09:00-18:00' },
]

/**
 * Reads the value of one field.
 *
 * @param wrapper - The mounted card.
 * @param testid - The field's test id.
 * @returns The input's value.
 */
const valueOf = (wrapper: Awaited<ReturnType<typeof mountSettingsCard>>['wrapper'], testid: string): string =>
  (wrapper.get(`[data-testid="${testid}"]`).element as HTMLInputElement).value

describe('StorefrontContentCard', () => {
  it('shows the stored figures, badge and contact details', async () => {
    const { wrapper } = await mountSettingsCard(StorefrontContentCard, rows)

    expect(valueOf(wrapper, 'storefront-stats-clients')).toBe('5,000+')
    expect(valueOf(wrapper, 'storefront-stats-uptime')).toBe('99.9%')
    expect(valueOf(wrapper, 'storefront-stats-regions')).toBe('12')
    expect(valueOf(wrapper, 'storefront-hero-badge')).toBe('New')
    expect(valueOf(wrapper, 'storefront-contact-email')).toBe('hi@example.com')
    expect(valueOf(wrapper, 'storefront-contact-phone')).toBe('+1 555')
    expect(valueOf(wrapper, 'storefront-contact-whatsapp')).toBe('+1 556')
    expect(valueOf(wrapper, 'storefront-contact-telegram')).toBe('@acme')
    expect(valueOf(wrapper, 'storefront-contact-address')).toBe('1 Main St')
    expect(valueOf(wrapper, 'storefront-contact-map-url')).toBe('https://maps.example.com/x')
    expect(valueOf(wrapper, 'storefront-contact-hours')).toBe('Mon-Fri 09:00-18:00')
    expect(selectText(wrapper, 'storefront-contact-timezone')).toBe('UTC+04:00')
  })

  it('groups the fields under Figures, Contacts, Messengers and Address and hours', async () => {
    const { wrapper } = await mountSettingsCard(StorefrontContentCard, rows)
    /**
     * Finds one headed group of the card.
     *
     * @param name - The group's test id suffix.
     * @returns The group's wrapper.
     */
    const group = (name: string) => wrapper.get(`[data-testid="storefront-group-${name}"]`)

    expect(wrapper.findAll('h3').map((heading) => heading.text())).toEqual(['Figures', 'Contacts', 'Messengers', 'Address and hours'])
    expect(group('figures').find('[data-testid="storefront-stats-clients"]').exists()).toBe(true)
    expect(group('figures').find('[data-testid="storefront-hero-badge"]').exists()).toBe(true)
    expect(group('contacts').find('[data-testid="storefront-contact-email"]').exists()).toBe(true)
    expect(group('contacts').find('[data-testid="storefront-contact-phone"]').exists()).toBe(true)
    expect(group('messengers').find('[data-testid="storefront-contact-whatsapp"]').exists()).toBe(true)
    expect(group('messengers').find('[data-testid="storefront-contact-telegram"]').exists()).toBe(true)
    expect(group('address').find('[data-testid="storefront-contact-address"]').exists()).toBe(true)
    expect(group('address').find('[data-testid="storefront-contact-hours"]').exists()).toBe(true)
    expect(group('address').find('[data-testid="storefront-contact-map-url"]').exists()).toBe(true)
    expect(group('address').find('[data-testid="storefront-contact-timezone"]').exists()).toBe(true)
  })

  it('lets every field fill its grid column instead of a fixed width', async () => {
    const { wrapper } = await mountSettingsCard(StorefrontContentCard, rows)

    for (const input of wrapper.findAll('input[data-testid^="storefront-"]')) {
      expect(input.classes().join(' ')).not.toMatch(/min-w-|\bflex-1\b/)
    }
  })

  it('saves a picked time zone as its IANA id', async () => {
    const { wrapper, registry } = await mountSettingsCard(StorefrontContentCard, rows)

    selectOf(wrapper, 'storefront-contact-timezone').vm.$emit('update:modelValue', 'Asia/Yerevan')
    await registry.saveAll(save)

    expect(save.mock.calls).toEqual([[14, 'Asia/Yerevan']])
  })

  it('shows the address and the hours once, with a tab per language holding that language row', async () => {
    const { wrapper } = await mountSettingsCard(StorefrontContentCard, rows)
    expect(wrapper.find('[data-testid="storefront-contact-address-ru"]').exists()).toBe(false)

    await wrapper.get('[data-testid="storefront-contact-hours-tab-ru"]').trigger('click')
    expect(valueOf(wrapper, 'storefront-contact-hours-ru')).toBe('Пн-Пт 09:00-18:00')
    await wrapper.get('[data-testid="storefront-contact-hours-tab-hy"]').trigger('click')
    expect(valueOf(wrapper, 'storefront-contact-hours-hy')).toBe('Երկ-Ուրբ 09:00-18:00')
    await wrapper.get('[data-testid="storefront-contact-address-tab-ru"]').trigger('click')
    expect(valueOf(wrapper, 'storefront-contact-address-ru')).toBe('ул. Примерная, 1')
    await wrapper.get('[data-testid="storefront-contact-address-tab-hy"]').trigger('click')
    expect(valueOf(wrapper, 'storefront-contact-address-hy')).toBe('Օրինակ փողոց 1')
    await wrapper.get('[data-testid="storefront-contact-address-tab-de"]').trigger('click')
    expect(valueOf(wrapper, 'storefront-contact-address-de')).toBe('Beispielstrasse 1')
    await wrapper.get('[data-testid="storefront-contact-address-tab-es"]').trigger('click')
    expect(valueOf(wrapper, 'storefront-contact-address-es')).toBe('Calle Ejemplo 1')
    await wrapper.get('[data-testid="storefront-contact-hours-tab-de"]').trigger('click')
    expect(valueOf(wrapper, 'storefront-contact-hours-de')).toBe('Mo-Fr 09:00-18:00')
  })

  it('draws a tab for every supported language, German and Spanish included', async () => {
    const { wrapper } = await mountSettingsCard(StorefrontContentCard, rows)

    expect(wrapper.findAll('[data-testid^="storefront-contact-address-tab-"][role="tab"]').map((tab) => tab.attributes('data-testid'))).toEqual([
      'storefront-contact-address-tab-en',
      'storefront-contact-address-tab-ru',
      'storefront-contact-address-tab-hy',
      'storefront-contact-address-tab-de',
      'storefront-contact-address-tab-es',
      'storefront-contact-address-tab-fr',
    ])
  })

  it('marks the language tabs whose row is empty', async () => {
    const emptied = rows.map((row) => (row.key === 'portal.contact.address.hy' ? { ...row, value: '' } : row))
    const { wrapper } = await mountSettingsCard(StorefrontContentCard, emptied)

    expect(wrapper.find('[data-testid="storefront-contact-address-tab-hy-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="storefront-contact-address-tab-ru-empty"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="storefront-contact-hours-tab-hy-empty"]').exists()).toBe(false)
  })

  it('draws no Save button of its own: the page bar saves the edited row only', async () => {
    const { wrapper, registry } = await mountSettingsCard(StorefrontContentCard, rows)
    expect(wrapper.find('[data-testid="storefront-save-stats-uptime"]').exists()).toBe(false)

    await wrapper.get('[data-testid="storefront-stats-uptime"]').setValue('99.99%')
    await registry.saveAll(save)

    expect(save.mock.calls).toEqual([[2, '99.99%']])
  })

  it('says which rows are missing instead of drawing dead fields', async () => {
    const { wrapper } = await mountSettingsCard(StorefrontContentCard, [rows[0] as Setting])

    expect(wrapper.find('[data-testid="storefront-hero-badge"]').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="setting-not-seeded"]')).toHaveLength(11)
    expect(wrapper.text()).toContain('portal.hero.badge')
  })

  it('saves the working hours and the Russian address as their own rows', async () => {
    const { wrapper, registry } = await mountSettingsCard(StorefrontContentCard, rows)

    await wrapper.get('[data-testid="storefront-contact-hours"]').setValue('Daily 08:00-20:00')
    await wrapper.get('[data-testid="storefront-contact-address-tab-ru"]').trigger('click')
    await wrapper.get('[data-testid="storefront-contact-address-ru"]').setValue('ул. Новая, 2')
    await registry.saveAll(save)

    // The registry saves in the order the card registered its drafts, which is not the order the operator typed in.
    expect(save.mock.calls).toHaveLength(2)
    expect(save.mock.calls).toEqual(expect.arrayContaining([[11, 'Daily 08:00-20:00'], [15, 'ул. Новая, 2']]))
  })

  it('flags a malformed email under the field', async () => {
    const { wrapper } = await mountSettingsCard(StorefrontContentCard, rows)

    await wrapper.get('[data-testid="storefront-contact-email"]').setValue('not-an-address')

    expect(wrapper.find('[data-testid="storefront-contact-email-invalid"]').exists()).toBe(true)
  })

  it('flags a map address that is not https', async () => {
    const { wrapper } = await mountSettingsCard(StorefrontContentCard, rows)

    await wrapper.get('[data-testid="storefront-contact-map-url"]').setValue('http://maps.example.com/x')

    expect(wrapper.find('[data-testid="storefront-contact-map-url-invalid"]').exists()).toBe(true)
  })

  it('accepts a valid email and lets an empty one clear the field', async () => {
    const { wrapper, registry } = await mountSettingsCard(StorefrontContentCard, rows)
    const email = wrapper.get('[data-testid="storefront-contact-email"]')

    await email.setValue('help@example.org')
    expect(wrapper.find('[data-testid="storefront-contact-email-invalid"]').exists()).toBe(false)
    await registry.saveAll(save)
    await email.setValue('')
    await registry.saveAll(save)

    expect(save.mock.calls).toEqual([[5, 'help@example.org'], [5, '']])
  })
})

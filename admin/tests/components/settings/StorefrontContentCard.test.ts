/**
 * Tests for StorefrontContentCard: the figures, hero badge and contact fields, each saved on its
 * own, and the warning for a row the API never seeded.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import StorefrontContentCard from '@/components/settings/StorefrontContentCard.vue'
import type { Setting, SettingSave } from '@/types/setting'
import { mountScreen } from '../../helpers/mount'

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
]

/**
 * Reads the value of one field.
 *
 * @param wrapper - The mounted card.
 * @param testid - The field's test id.
 * @returns The input's value.
 */
const valueOf = (wrapper: Awaited<ReturnType<typeof mountScreen>>['wrapper'], testid: string): string =>
  (wrapper.get(`[data-testid="${testid}"]`).element as HTMLInputElement).value

describe('StorefrontContentCard', () => {
  it('shows the stored figures, badge and contact details', async () => {
    const { wrapper } = await mountScreen(StorefrontContentCard, { save, settings: rows })

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
    expect(valueOf(wrapper, 'storefront-contact-hours-ru')).toBe('Пн-Пт 09:00-18:00')
    expect(valueOf(wrapper, 'storefront-contact-hours-hy')).toBe('Երկ-Ուրբ 09:00-18:00')
    expect(valueOf(wrapper, 'storefront-contact-timezone')).toBe('UTC+4')
    expect(valueOf(wrapper, 'storefront-contact-address-ru')).toBe('ул. Примерная, 1')
    expect(valueOf(wrapper, 'storefront-contact-address-hy')).toBe('Օրինակ փողոց 1')
  })

  it('keeps Save disabled until a field changes, then emits that row only', async () => {
    const { wrapper } = await mountScreen(StorefrontContentCard, { save, settings: rows })
    const saveButton = wrapper.get('[data-testid="storefront-save-stats-uptime"]')
    expect(saveButton.attributes('disabled')).toBeDefined()

    await wrapper.get('[data-testid="storefront-stats-uptime"]').setValue('99.99%')
    expect(saveButton.attributes('disabled')).toBeUndefined()
    await saveButton.trigger('click')

    expect(save.mock.calls).toEqual([[2, '99.99%']])
  })

  it('saves on Enter', async () => {
    const { wrapper } = await mountScreen(StorefrontContentCard, { save, settings: rows })
    const telegram = wrapper.get('[data-testid="storefront-contact-telegram"]')

    await telegram.setValue('@new')
    await telegram.trigger('keyup.enter')

    expect(save.mock.calls).toEqual([[8, '@new']])
  })

  it('says which rows are missing instead of drawing dead fields', async () => {
    const { wrapper } = await mountScreen(StorefrontContentCard, { save, settings: [rows[0] as Setting] })

    expect(wrapper.find('[data-testid="storefront-hero-badge"]').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="setting-not-seeded"]')).toHaveLength(15)
    expect(wrapper.text()).toContain('portal.hero.badge')
  })

  it('saves the working hours and the Russian address as their own rows', async () => {
    const { wrapper } = await mountScreen(StorefrontContentCard, { save, settings: rows })

    await wrapper.get('[data-testid="storefront-contact-hours"]').setValue('Daily 08:00-20:00')
    await wrapper.get('[data-testid="storefront-save-contact-hours"]').trigger('click')
    await wrapper.get('[data-testid="storefront-contact-address-ru"]').setValue('ул. Новая, 2')
    await wrapper.get('[data-testid="storefront-contact-address-ru"]').trigger('keyup.enter')

    expect(save.mock.calls).toEqual([[11, 'Daily 08:00-20:00'], [15, 'ул. Новая, 2']])
  })

  it('refuses a malformed email before any save and says why', async () => {
    const { wrapper } = await mountScreen(StorefrontContentCard, { save, settings: rows })
    const email = wrapper.get('[data-testid="storefront-contact-email"]')

    await email.setValue('not-an-address')
    await email.trigger('keyup.enter')
    await wrapper.get('[data-testid="storefront-save-contact-email"]').trigger('click')

    expect(save).not.toHaveBeenCalled()
    expect(wrapper.find('[data-testid="storefront-contact-email-invalid"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="storefront-save-contact-email"]').attributes('disabled')).toBeDefined()
  })

  it('refuses a map address that is not https', async () => {
    const { wrapper } = await mountScreen(StorefrontContentCard, { save, settings: rows })

    await wrapper.get('[data-testid="storefront-contact-map-url"]').setValue('http://maps.example.com/x')

    expect(wrapper.find('[data-testid="storefront-contact-map-url-invalid"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="storefront-save-contact-map-url"]').attributes('disabled')).toBeDefined()
  })

  it('accepts a valid email and lets an empty one clear the field', async () => {
    const { wrapper } = await mountScreen(StorefrontContentCard, { save, settings: rows })
    const email = wrapper.get('[data-testid="storefront-contact-email"]')

    await email.setValue('help@example.org')
    expect(wrapper.find('[data-testid="storefront-contact-email-invalid"]').exists()).toBe(false)
    await wrapper.get('[data-testid="storefront-save-contact-email"]').trigger('click')
    await email.setValue('')
    await wrapper.get('[data-testid="storefront-save-contact-email"]').trigger('click')

    expect(save.mock.calls).toEqual([[5, 'help@example.org'], [5, '']])
  })
})

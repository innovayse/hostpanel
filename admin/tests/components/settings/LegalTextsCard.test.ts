/**
 * Tests for LegalTextsCard: one text per legal page with a tab per language, each language its own settings row,
 * saved by the page's save bar, and the warning for rows the API never seeded.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import LegalTextsCard from '@/components/settings/LegalTextsCard.vue'
import type { Setting, SettingSave } from '@/types/setting'
import { mountSettingsCard } from '../../helpers/settingsPage'

/** The save callback the card is given; it answers that the value was stored. */
const save = vi.fn<SettingSave>(async () => ({ saved: true, error: null }))

beforeEach(() => {
  save.mockClear()
})

/** The pages of the card as the settings keys name them, in the order the card draws them. */
const PAGES = ['terms', 'privacy', 'refund', 'acceptable_use', 'cookies']

/** The suffix of each language's row. */
const SUFFIXES = ['', '.ru', '.hy']

/** Every row the card edits, as the API seeds them: empty, except the English terms and the Russian refund policy. */
const rows: Setting[] = PAGES.flatMap((page, pageIndex) => SUFFIXES.map((suffix, suffixIndex): Setting => {
  const key = `portal.legal.${page}${suffix}`
  const value = key === 'portal.legal.terms' ? '## Seller\n\nAcme Ltd.' : key === 'portal.legal.refund.ru' ? 'Возврат в течение 14 дней.' : ''
  return { id: pageIndex * 3 + suffixIndex + 1, key, value }
}))

/**
 * Reads the value of one text field.
 *
 * @param wrapper - The mounted card.
 * @param testid - The field's test id.
 * @returns The textarea's value.
 */
const valueOf = (wrapper: Awaited<ReturnType<typeof mountSettingsCard>>['wrapper'], testid: string): string =>
  (wrapper.get(`[data-testid="${testid}"]`).element as HTMLTextAreaElement).value

describe('LegalTextsCard', () => {
  it('draws one multi-line field per legal page, English first', async () => {
    const { wrapper } = await mountSettingsCard(LegalTextsCard, rows)

    for (const testid of ['terms', 'privacy', 'refund', 'acceptable-use', 'cookies']) {
      expect(wrapper.get(`[data-testid="legal-${testid}"]`).element.tagName).toBe('TEXTAREA')
    }
    expect(valueOf(wrapper, 'legal-terms')).toBe('## Seller\n\nAcme Ltd.')
    expect(wrapper.find('[data-testid="legal-terms-ru"]').exists()).toBe(false)
  })

  it('gives every field a placeholder and a length cap', async () => {
    const { wrapper } = await mountSettingsCard(LegalTextsCard, rows)

    for (const area of wrapper.findAll('textarea')) {
      expect(area.attributes('placeholder')).toBeTruthy()
      expect(area.attributes('maxlength')).toBe('12000')
    }
  })

  it('shows the Russian and Armenian rows on their own tabs', async () => {
    const { wrapper } = await mountSettingsCard(LegalTextsCard, rows)

    await wrapper.get('[data-testid="legal-refund-tab-ru"]').trigger('click')
    expect(valueOf(wrapper, 'legal-refund-ru')).toBe('Возврат в течение 14 дней.')
    await wrapper.get('[data-testid="legal-refund-tab-hy"]').trigger('click')
    expect(valueOf(wrapper, 'legal-refund-hy')).toBe('')
  })

  it('marks the tabs whose row is empty', async () => {
    const { wrapper } = await mountSettingsCard(LegalTextsCard, rows)

    expect(wrapper.find('[data-testid="legal-terms-tab-en-empty"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="legal-terms-tab-ru-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="legal-refund-tab-ru-empty"]').exists()).toBe(false)
  })

  it('saves only the edited rows, each as its own setting', async () => {
    const { wrapper, registry } = await mountSettingsCard(LegalTextsCard, rows)

    await wrapper.get('[data-testid="legal-privacy"]').setValue('## Controller\n\nAcme Ltd.')
    await wrapper.get('[data-testid="legal-cookies-tab-hy"]').trigger('click')
    await wrapper.get('[data-testid="legal-cookies-hy"]').setValue('Մենք օգտագործում ենք cookie։')
    await registry.saveAll(save)

    expect(save.mock.calls).toHaveLength(2)
    expect(save.mock.calls).toEqual(expect.arrayContaining([[4, '## Controller\n\nAcme Ltd.'], [15, 'Մենք օգտագործում ենք cookie։']]))
  })

  it('says which rows are missing instead of drawing dead fields', async () => {
    const { wrapper } = await mountSettingsCard(LegalTextsCard, [rows[0] as Setting])

    expect(wrapper.find('[data-testid="legal-terms"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="legal-privacy"]').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="setting-not-seeded"]')).toHaveLength(4)
    expect(wrapper.text()).toContain('portal.legal.privacy')
  })
})

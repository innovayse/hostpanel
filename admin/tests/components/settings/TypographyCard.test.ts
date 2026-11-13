/**
 * Tests for TypographyCard: the two face selects, the template-default option, the preview that
 * follows the picked face through a utility class, and the unseeded warning.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import TypographyCard from '@/components/settings/TypographyCard.vue'
import type { Setting, SettingSave } from '@/types/setting'
import { mountSettingsCard } from '../../helpers/settingsPage'
import { pickOption, selectText } from '../../helpers/select'

/** The save callback the card is given; it answers that the value was stored. */
const save = vi.fn<SettingSave>(async () => ({ saved: true, error: null }))

beforeEach(() => {
  save.mockClear()
})

/** Both face rows, as the API seeds them. */
const rows: Setting[] = [
  { id: 1, key: 'portal.brand.font_heading', value: '' },
  { id: 2, key: 'portal.brand.font_body', value: 'inter' },
]

describe('TypographyCard', () => {
  it('shows the template default for an empty value and the face name otherwise', async () => {
    const { wrapper } = await mountSettingsCard(TypographyCard, rows)

    expect(selectText(wrapper, 'typography-heading')).toBe('Template default')
    expect(selectText(wrapper, 'typography-body')).toBe('Inter')
  })

  it('saves the picked face for the heading only', async () => {
    const { wrapper, registry } = await mountSettingsCard(TypographyCard, rows)

    await pickOption(wrapper, 'typography-heading', 'Noto Serif Armenian')
    await registry.saveAll(save)

    expect(save.mock.calls).toEqual([[1, 'noto-serif-armenian']])
  })

  it('previews the sample lines in the picked face with a utility class, not an inline style', async () => {
    const { wrapper } = await mountSettingsCard(TypographyCard, rows)
    expect(wrapper.get('[data-testid="typography-preview"]').html()).toContain('font-brand-inter')

    await pickOption(wrapper, 'typography-body', "System (device's own)")

    const preview = wrapper.get('[data-testid="typography-preview"]')
    expect(preview.html()).toContain('font-brand-system')
    expect(preview.html()).not.toContain('style=')
  })

  it('shows the three language samples', async () => {
    const { wrapper } = await mountSettingsCard(TypographyCard, rows)
    const preview = wrapper.get('[data-testid="typography-preview"]').text()

    expect(preview).toContain('Your whole digital infrastructure')
    expect(preview).toContain('Вся ваша цифровая инфраструктура')
    expect(preview).toContain('Ձեր ամբողջ թվային ենթակառուցվածքը')
  })

  it('warns about an unseeded row', async () => {
    const { wrapper } = await mountSettingsCard(TypographyCard, [rows[1] as Setting])

    expect(wrapper.get('[data-testid="setting-not-seeded"]').text()).toContain('portal.brand.font_heading')
    expect(wrapper.find('[data-testid="typography-heading"]').exists()).toBe(false)
  })
})

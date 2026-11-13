/**
 * Tests for SiteIdentityCard: the name and tagline fields as drafts of the page save bar, and the warning
 * for a row the API never seeded.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import SiteIdentityCard from '@/components/settings/SiteIdentityCard.vue'
import type { Setting, SettingSave } from '@/types/setting'
import { mountSettingsCard } from '../../helpers/settingsPage'

/** The save callback the card is given; it answers that the value was stored. */
const save = vi.fn<SettingSave>(async () => ({ saved: true, error: null }))

beforeEach(() => {
  save.mockClear()
})

/** Both identity rows, as the API seeds them. */
const rows: Setting[] = [
  { id: 1, key: 'portal.site.name', value: 'Acme Hosting' },
  { id: 2, key: 'portal.site.tagline', value: 'Fast and calm' },
]

describe('SiteIdentityCard', () => {
  it('shows the stored name and tagline', async () => {
    const { wrapper } = await mountSettingsCard(SiteIdentityCard, rows)

    expect((wrapper.get('[data-testid="site-identity-name"]').element as HTMLInputElement).value).toBe('Acme Hosting')
    expect((wrapper.get('[data-testid="site-identity-tagline"]').element as HTMLInputElement).value).toBe('Fast and calm')
  })

  it('draws no Save button of its own: the page bar counts and saves the edited row only', async () => {
    const { wrapper, registry } = await mountSettingsCard(SiteIdentityCard, rows)
    expect(wrapper.find('[data-testid="site-identity-save-name"]').exists()).toBe(false)
    expect(registry.dirtyCount.value).toBe(0)

    await wrapper.get('[data-testid="site-identity-name"]').setValue('Acme Cloud')
    expect(registry.dirtyCount.value).toBe(1)
    await registry.saveAll(save)

    expect(save.mock.calls).toEqual([[1, 'Acme Cloud']])
  })

  it('says which row is missing instead of drawing a dead field', async () => {
    const { wrapper } = await mountSettingsCard(SiteIdentityCard, [rows[0] as Setting])

    expect(wrapper.find('[data-testid="site-identity-tagline"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="setting-not-seeded"]').text()).toContain('portal.site.tagline')
  })

  it('reports the page as saving while a row is in flight', async () => {
    save.mockReturnValueOnce(new Promise(() => undefined))
    const { wrapper, registry } = await mountSettingsCard(SiteIdentityCard, rows)
    await wrapper.get('[data-testid="site-identity-name"]').setValue('X')
    void registry.saveAll(save)

    expect(registry.saving.value).toBe(true)
  })

  it('shows the success sentence inside the card after a save, and clears it on the next edit', async () => {
    const { wrapper, registry } = await mountSettingsCard(SiteIdentityCard, rows)
    const name = wrapper.get('[data-testid="site-identity-name"]')

    await name.setValue('Acme Cloud')
    await registry.saveAll(save)
    await vi.waitFor(() => {
      expect(wrapper.text()).toContain('Changes saved successfully.')
    })

    await name.setValue('Acme Cloud 2')
    expect(wrapper.text()).not.toContain('Changes saved successfully.')
  })

  it('shows the API sentence next to the field when the save is refused', async () => {
    save.mockResolvedValueOnce({ saved: false, error: 'Name is too long.' })
    const { wrapper, registry } = await mountSettingsCard(SiteIdentityCard, rows)

    await wrapper.get('[data-testid="site-identity-name"]').setValue('Acme Cloud')
    await registry.saveAll(save)

    await vi.waitFor(() => {
      expect(wrapper.text()).toContain('Name is too long.')
    })
    expect((wrapper.get('[data-testid="site-identity-name"]').element as HTMLInputElement).value).toBe('Acme Cloud')
  })
})

/**
 * Tests for SiteIdentityCard: the name and tagline fields, each saved on its own, and the warning
 * for a row the API never seeded.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import SiteIdentityCard from '@/components/settings/SiteIdentityCard.vue'
import type { Setting, SettingSave } from '@/types/setting'
import { mountScreen } from '../../helpers/mount'

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
    const { wrapper } = await mountScreen(SiteIdentityCard, { save, settings: rows })

    expect((wrapper.get('[data-testid="site-identity-name"]').element as HTMLInputElement).value).toBe('Acme Hosting')
    expect((wrapper.get('[data-testid="site-identity-tagline"]').element as HTMLInputElement).value).toBe('Fast and calm')
  })

  it('keeps Save disabled until a field changes, then emits that row only', async () => {
    const { wrapper } = await mountScreen(SiteIdentityCard, { save, settings: rows })
    const saveButton = wrapper.get('[data-testid="site-identity-save-name"]')
    expect(saveButton.attributes('disabled')).toBeDefined()

    await wrapper.get('[data-testid="site-identity-name"]').setValue('Acme Cloud')
    expect(saveButton.attributes('disabled')).toBeUndefined()
    await saveButton.trigger('click')

    expect(save.mock.calls).toEqual([[1, 'Acme Cloud']])
  })

  it('saves on Enter', async () => {
    const { wrapper } = await mountScreen(SiteIdentityCard, { save, settings: rows })
    const tagline = wrapper.get('[data-testid="site-identity-tagline"]')

    await tagline.setValue('Faster')
    await tagline.trigger('keyup.enter')

    expect(save.mock.calls).toEqual([[2, 'Faster']])
  })

  it('says which row is missing instead of drawing a dead field', async () => {
    const { wrapper } = await mountScreen(SiteIdentityCard, { save, settings: [rows[0] as Setting] })

    expect(wrapper.find('[data-testid="site-identity-tagline"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="setting-not-seeded"]').text()).toContain('portal.site.tagline')
  })

  it('disables Save while that field is being saved', async () => {
    save.mockReturnValueOnce(new Promise(() => undefined))
    const { wrapper } = await mountScreen(SiteIdentityCard, { save, settings: rows })
    await wrapper.get('[data-testid="site-identity-name"]').setValue('X')
    await wrapper.get('[data-testid="site-identity-save-name"]').trigger('click')

    expect(wrapper.get('[data-testid="site-identity-save-name"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="site-identity-save-tagline"]').text()).toBe('Save')
  })

  it('shows the success sentence inside the card after a save, and clears it on the next edit', async () => {
    const { wrapper } = await mountScreen(SiteIdentityCard, { save, settings: rows })
    const name = wrapper.get('[data-testid="site-identity-name"]')

    await name.setValue('Acme Cloud')
    await wrapper.get('[data-testid="site-identity-save-name"]').trigger('click')
    await vi.waitFor(() => {
      expect(wrapper.text()).toContain('Changes saved successfully.')
    })

    await name.setValue('Acme Cloud 2')
    expect(wrapper.text()).not.toContain('Changes saved successfully.')
  })

  it('shows the API sentence next to the field when the save is refused', async () => {
    save.mockResolvedValueOnce({ saved: false, error: 'Name is too long.' })
    const { wrapper } = await mountScreen(SiteIdentityCard, { save, settings: rows })

    await wrapper.get('[data-testid="site-identity-name"]').setValue('Acme Cloud')
    await wrapper.get('[data-testid="site-identity-save-name"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.text()).toContain('Name is too long.')
    })
    expect((wrapper.get('[data-testid="site-identity-name"]').element as HTMLInputElement).value).toBe('Acme Cloud')
  })
})

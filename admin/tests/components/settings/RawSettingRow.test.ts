/**
 * Tests for RawSettingRow: the row shows its key, value input and description, an edit is a draft in
 * the page registry, and the save outcome shows next to the input while a refused save keeps the text.
 */
import { describe, expect, it, vi } from 'vitest'
import RawSettingRow from '@/components/settings/RawSettingRow.vue'
import type { Setting, SettingSave } from '@/types/setting'
import { mountSettingsCard } from '../../helpers/settingsPage'

/** The stored rows: one with a description and one without. */
const rows: Setting[] = [
  { id: 2, key: 'billing.tax.enabled', value: 'false', description: 'Charge tax on invoices.' },
  { id: 3, key: 'fraud.enabled', value: 'true' },
]

/**
 * Mounts the row for one key under the page registry.
 *
 * @param settingKey - The key the row edits.
 * @returns The wrapper and the registry.
 */
const mountRow = (settingKey: string) => mountSettingsCard(RawSettingRow, rows, { settingKey })

describe('RawSettingRow', () => {
  it('renders the key, the stored value in the input and the description', async () => {
    const { wrapper } = await mountRow('billing.tax.enabled')

    const row = wrapper.get('[data-testid="setting-row-2"]')
    expect(row.text()).toContain('billing.tax.enabled')
    expect(row.text()).toContain('Charge tax on invoices.')
    expect((wrapper.get('[data-testid="setting-value-2"]').element as HTMLInputElement).value).toBe('false')
  })

  it('renders nothing for a key that was never seeded', async () => {
    const { wrapper } = await mountRow('missing.key')

    expect(wrapper.find('[data-testid^="setting-row-"]').exists()).toBe(false)
  })

  it('counts an edit in the registry and saves it from the bar', async () => {
    const save = vi.fn<SettingSave>(async () => ({ saved: true, error: null }))
    const { wrapper, registry } = await mountRow('billing.tax.enabled')

    expect(registry.dirtyCount.value).toBe(0)
    await wrapper.get('[data-testid="setting-value-2"]').setValue('true')
    expect(registry.dirtyCount.value).toBe(1)

    await registry.saveAll(save)
    expect(save.mock.calls).toEqual([[2, 'true']])
    expect(wrapper.get('[data-testid="setting-outcome-2"]').text()).toContain('Changes saved successfully.')
  })

  it('keeps the draft and shows the refusal next to the input when the save is refused', async () => {
    const save = vi.fn<SettingSave>(async () => ({ saved: false, error: 'Not a valid value.' }))
    const { wrapper, registry } = await mountRow('fraud.enabled')

    await wrapper.get('[data-testid="setting-value-3"]').setValue('maybe')
    await registry.saveAll(save)

    expect((wrapper.get('[data-testid="setting-value-3"]').element as HTMLInputElement).value).toBe('maybe')
    expect(registry.dirtyCount.value).toBe(1)
    expect(wrapper.get('[data-testid="setting-outcome-3"]').text()).toContain('Not a valid value.')
  })
})

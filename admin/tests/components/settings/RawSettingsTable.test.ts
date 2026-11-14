/**
 * Tests for RawSettingsTable: only the keys no card owns are listed, edits count in the page
 * registry, and a refused save keeps the operator's text.
 */
import { describe, expect, it, vi } from 'vitest'
import RawSettingsTable from '@/components/settings/RawSettingsTable.vue'
import type { Setting, SettingSave } from '@/types/setting'
import { mountSettingsCard } from '../../helpers/settingsPage'

/** One card-owned key and two the table lists. */
const rows: Setting[] = [
  { id: 1, key: 'portal.site.name', value: 'Acme' },
  { id: 2, key: 'billing.tax.enabled', value: 'false', description: 'Charge tax on invoices.' },
  { id: 3, key: 'fraud.enabled', value: 'true' },
]

describe('RawSettingsTable', () => {
  it('lists only the keys no card owns, with the description as help text', async () => {
    const { wrapper } = await mountSettingsCard(RawSettingsTable, rows)

    expect(wrapper.find('[data-testid="setting-row-1"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="setting-row-2"]').text()).toContain('Charge tax on invoices.')
    expect(wrapper.find('[data-testid="setting-row-3"]').exists()).toBe(true)
  })

  it('shows the empty message when every key is owned by a card', async () => {
    const { wrapper } = await mountSettingsCard(RawSettingsTable, rows.filter((row) => row.id === 1))

    expect(wrapper.find('[data-testid="setting-row-1"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('No settings found.')
  })

  it('counts an edit in the registry and saves it from the bar', async () => {
    const save = vi.fn<SettingSave>(async () => ({ saved: true, error: null }))
    const { wrapper, registry } = await mountSettingsCard(RawSettingsTable, rows)

    await wrapper.get('[data-testid="setting-value-2"]').setValue('true')
    expect(registry.dirtyCount.value).toBe(1)
    expect(wrapper.find('[data-testid="setting-save-2"]').exists()).toBe(false)

    await registry.saveAll(save)
    expect(save.mock.calls).toEqual([[2, 'true']])
  })

  it('keeps the draft and shows the refusal when the save is refused', async () => {
    const save = vi.fn<SettingSave>(async () => ({ saved: false, error: 'Not a valid value.' }))
    const { wrapper, registry } = await mountSettingsCard(RawSettingsTable, rows)

    await wrapper.get('[data-testid="setting-value-3"]').setValue('maybe')
    await registry.saveAll(save)

    expect((wrapper.get('[data-testid="setting-value-3"]').element as HTMLInputElement).value).toBe('maybe')
    expect(registry.dirtyCount.value).toBe(1)
    expect(wrapper.get('[data-testid="setting-outcome-3"]').text()).toContain('Not a valid value.')
  })
})

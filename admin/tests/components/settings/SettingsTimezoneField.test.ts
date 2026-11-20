/**
 * Tests for SettingsTimezoneField: the searchable list of IANA zones, the stored id as the value, the old free-text offset
 * shown as the zone it means, and a pick written back as an id.
 */
import { defineComponent, h, toRef } from 'vue'
import { describe, expect, it, vi } from 'vitest'
import SettingsTimezoneField from '@/components/settings/SettingsTimezoneField.vue'
import { useSettingDraft } from '@/composables/useSettingDraft'
import type { Setting, SettingSave } from '@/types/setting'
import { mountSettingsCard } from '../../helpers/settingsPage'
import { selectOf, selectText } from '../../helpers/select'

/** The key the host edits. */
const KEY = 'portal.contact.timezone'

/** A card with the field, the draft built from the settings it is handed. */
const Host = defineComponent({
  props: { settings: { type: Array as () => Setting[], required: true } },
  setup: (props) => {
    const draft = useSettingDraft(toRef(props, 'settings'), KEY)
    return () => h(SettingsTimezoneField, {
      draft,
      settingKey: KEY,
      envName: 'ENV_TZ',
      label: 'Time zone',
      hint: 'Shown beside the hours',
      testid: 'tz',
    })
  },
})

/**
 * The one row the field edits.
 *
 * @param value - What the row holds.
 * @returns The settings list.
 */
const rowWith = (value: string): Setting[] => [{ id: 3, key: KEY, value }]

describe('SettingsTimezoneField', () => {
  it('shows the stored IANA id as its city and offset, under its caption and help line', async () => {
    const { wrapper } = await mountSettingsCard(Host, rowWith('Asia/Yerevan'))

    expect(selectText(wrapper, 'tz')).toMatch(/^Yerevan \(UTC\+04:00\)$/)
    expect(wrapper.text()).toContain('Time zone')
    expect(wrapper.text()).toContain('Shown beside the hours')
  })

  it('shows the old free-text offset as the fixed-offset zone it means, without rewriting the row', async () => {
    const { wrapper, registry } = await mountSettingsCard(Host, rowWith('UTC+4'))

    expect(selectText(wrapper, 'tz')).toBe('UTC+04:00')
    expect(registry.dirtyCount.value).toBe(0)
  })

  it('shows text that is no zone as it is', async () => {
    const { wrapper } = await mountSettingsCard(Host, rowWith('somewhere'))

    expect(selectText(wrapper, 'tz')).toBe('somewhere')
  })

  it('shows the placeholder for an empty row', async () => {
    const { wrapper } = await mountSettingsCard(Host, rowWith(''))

    expect(selectText(wrapper, 'tz')).toBe('No time zone')
  })

  it('offers the zones grouped by region with a search field', async () => {
    const { wrapper } = await mountSettingsCard(Host, rowWith('Asia/Yerevan'))

    await wrapper.get('[data-testid="tz"] button').trigger('click')

    expect(wrapper.find('[data-testid="ui-select-search"]').exists()).toBe(true)
    expect(wrapper.findAll('[data-testid="ui-select-group"]').length).toBeGreaterThan(5)
  })

  it('saves a pick as the IANA id, and an empty pick as an empty value', async () => {
    const save = vi.fn<SettingSave>(async () => ({ saved: true, error: null }))
    const { wrapper, registry } = await mountSettingsCard(Host, rowWith('UTC+4'))

    selectOf(wrapper, 'tz').vm.$emit('update:modelValue', 'Europe/Berlin')
    await registry.saveAll(save)
    selectOf(wrapper, 'tz').vm.$emit('update:modelValue', '')
    await registry.saveAll(save)

    expect(save.mock.calls).toEqual([[3, 'Europe/Berlin'], [3, '']])
  })

  it('says which row is missing instead of drawing a dead field', async () => {
    const { wrapper } = await mountSettingsCard(Host, [])

    expect(wrapper.find('[data-testid="tz"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="setting-not-seeded"]').text()).toContain(KEY)
  })
})
